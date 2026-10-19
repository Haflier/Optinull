using Optinull.Application.Jobs;
using Optinull.Application.MapSessions;
using Optinull.Application.Optimization;
using Optinull.Problems.Vrp;

namespace Optinull.Application.Tests.MapSessions;

public sealed class MapSessionServiceTests
{
    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly MapSettings Settings = new(SolverKind.Greedy, null, 42, null);

    private static MapSessionOptions Options() => new()
    {
        PublicBaseUrl = "https://maps.example.test",
        UnusedLifetime = TimeSpan.FromMinutes(10),
        ResultLifetime = TimeSpan.FromMinutes(20)
    };

    private static string TokenOf(MapLink link) => link.Url[(link.Url.IndexOf("#s=", StringComparison.Ordinal) + 3)..];

    private static VrpProblem Problem() =>
        new(0, 0, [new VrpCustomer(1, 1, 1), new VrpCustomer(2, 2, 1)], vehicles: 1, capacity: 5);

    private static (MapSessionService Service, ManualTime Time, JobQueue Queue) Create(int queueCapacity = 10)
    {
        var time = new ManualTime();
        var queue = new JobQueue(queueCapacity);

        return (new MapSessionService(queue, Options(), time), time, queue);
    }

    [Fact]
    public void Open_GivesAnUnguessableLink_ThatAuthorizesItsOwner()
    {
        var (service, _, _) = Create();

        var link = service.Open(7, 7, new FakeReplies(), Settings);

        Assert.StartsWith("https://maps.example.test/#s=", link.Url);
        Assert.Equal(TimeSpan.FromMinutes(10), link.ValidFor);

        var token = TokenOf(link);

        Assert.Equal(43, token.Length);
        Assert.DoesNotMatch("[+/=]", token);
        Assert.True(service.TryAuthorize(token, out var session));
        Assert.Equal(7, session!.OwnerId);
    }

    [Fact]
    public void Tokens_AreDifferentForEverySession()
    {
        var (service, _, _) = Create();

        var tokens = Enumerable.Range(1, 20)
            .Select(owner => TokenOf(service.Open(owner, owner, new FakeReplies(), Settings)))
            .ToList();

        Assert.Equal(20, tokens.Distinct().Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public void UnknownTokens_AreRejected(string? token)
    {
        var (service, _, _) = Create();

        service.Open(1, 1, new FakeReplies(), Settings);

        Assert.False(service.TryAuthorize(token, out _));
    }

    [Fact]
    public void AnotherUsersToken_OnlyOpensThatUsersSession()
    {
        var (service, _, _) = Create();

        var alice = TokenOf(service.Open(1, 1, new FakeReplies(), Settings));
        var bob = TokenOf(service.Open(2, 2, new FakeReplies(), Settings));

        Assert.True(service.TryAuthorize(alice, out var aliceSession));
        Assert.True(service.TryAuthorize(bob, out var bobSession));
        Assert.Equal(1, aliceSession!.OwnerId);
        Assert.Equal(2, bobSession!.OwnerId);
    }

    [Fact]
    public void OpeningANewSession_ReplacesTheOldOne()
    {
        var (service, _, _) = Create();

        var first = TokenOf(service.Open(1, 1, new FakeReplies(), Settings));
        var second = TokenOf(service.Open(1, 1, new FakeReplies(), Settings));

        Assert.False(service.TryAuthorize(first, out _));
        Assert.True(service.TryAuthorize(second, out _));
    }

    [Fact]
    public void Close_EndsTheSession()
    {
        var (service, _, _) = Create();
        var token = TokenOf(service.Open(1, 1, new FakeReplies(), Settings));

        Assert.True(service.Close(1));
        Assert.False(service.TryAuthorize(token, out _));
        Assert.False(service.Close(1));
    }

    [Fact]
    public void AnUnusedSession_Expires()
    {
        var (service, time, _) = Create();
        var token = TokenOf(service.Open(1, 1, new FakeReplies(), Settings));

        time.Now += TimeSpan.FromMinutes(9);
        Assert.True(service.TryAuthorize(token, out _));

        time.Now += TimeSpan.FromMinutes(2);
        Assert.False(service.TryAuthorize(token, out _));
    }

    [Fact]
    public void AnExpiredSession_CannotSubmitAJob()
    {
        var (service, time, queue) = Create();
        var token = TokenOf(service.Open(1, 1, new FakeReplies(), Settings));

        Assert.True(service.TryAuthorize(token, out var session));

        time.Now += TimeSpan.FromMinutes(11);

        // The web layer authorizes first, so an expired token never reaches Submit.
        Assert.False(service.TryAuthorize(token, out _));
        Assert.False(queue.Reader.TryRead(out _));
        Assert.NotNull(session);
    }

    [Fact]
    public void Submit_QueuesAJobInTheOwnersChat_WithTheChosenAlgorithm()
    {
        var (service, _, queue) = Create();
        service.TryAuthorize(TokenOf(service.Open(5, 5, new FakeReplies(), Settings)), out var session);

        var result = service.Submit(session!, Problem());

        Assert.Equal(MapSubmitOutcome.Started, result.Outcome);
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal(5, job!.ChatId);
        Assert.Equal(SolverKind.Greedy, job.Command.Solver);
        Assert.Equal(42, job.Command.Seed);
        Assert.Equal(MapPhase.Queued, service.GetStatus(session!).Phase);
    }

    [Fact]
    public void Submit_WhileAJobIsActive_IsBusy()
    {
        var (service, _, _) = Create();
        service.TryAuthorize(TokenOf(service.Open(5, 5, new FakeReplies(), Settings)), out var session);

        Assert.Equal(MapSubmitOutcome.Started, service.Submit(session!, Problem()).Outcome);
        Assert.Equal(MapSubmitOutcome.Busy, service.Submit(session!, Problem()).Outcome);
        Assert.True(service.IsBusy(session!));
    }

    [Fact]
    public void Submit_WhenTheChatHasAnotherActiveJob_IsBusy()
    {
        var (service, _, queue) = Create();
        service.TryAuthorize(TokenOf(service.Open(5, 5, new FakeReplies(), Settings)), out var session);

        // A job from a normal /solve command in the same chat.
        queue.TryEnqueue(5, new Optinull.Application.Commands.SolveCommand("x", SolverKind.Greedy), new FakeReplies(), new FakeProblemInstance());

        Assert.Equal(MapSubmitOutcome.Busy, service.Submit(session!, Problem()).Outcome);
    }

    [Fact]
    public void Submit_ExtendsTheSession_ToTheResultLifetime()
    {
        var (service, time, _) = Create();
        var token = TokenOf(service.Open(5, 5, new FakeReplies(), Settings));
        service.TryAuthorize(token, out var session);

        time.Now += TimeSpan.FromMinutes(5);
        service.Submit(session!, Problem());

        time.Now += TimeSpan.FromMinutes(15);
        Assert.True(service.TryAuthorize(token, out _));

        time.Now += TimeSpan.FromMinutes(6);
        Assert.False(service.TryAuthorize(token, out _));
    }

    [Fact]
    public async Task ARunThroughTheExecutor_ProducesAFeasibleSolution_AndKeepsItForTheResultLifetime()
    {
        var (service, time, queue) = Create();
        var chat = new FakeReplies();
        var token = TokenOf(service.Open(5, 5, chat, Settings));
        service.TryAuthorize(token, out var session);

        service.Submit(session!, Problem());
        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job!);

        var status = service.GetStatus(session!);

        Assert.Equal(MapPhase.Completed, status.Phase);
        Assert.NotNull(status.Solution);
        Assert.Equal(new[] { 0, 1 }, status.Solution!.Routes.SelectMany(route => route.Customers).Order().ToArray());
        Assert.All(status.Solution.Routes, route => Assert.True(route.Load <= route.Capacity));
        Assert.Contains(chat.Texts, text => text.StartsWith("Optimization completed"));

        // Completion was seen now, so the link lasts a full result lifetime from here.
        time.Now += TimeSpan.FromMinutes(19);
        Assert.True(service.TryAuthorize(token, out _));
    }

    [Fact]
    public void CancelJob_StopsTheSessionsQueuedJob_AndFreesTheChat()
    {
        var (service, _, queue) = Create();
        service.TryAuthorize(TokenOf(service.Open(5, 5, new FakeReplies(), Settings)), out var session);

        service.Submit(session!, Problem());

        Assert.True(service.CancelJob(session!));
        Assert.Equal(MapPhase.Cancelled, service.GetStatus(session!).Phase);
        Assert.False(service.IsBusy(session!));
        Assert.False(service.CancelJob(session!));

        // The chat is free again, so the user can submit once more.
        Assert.Equal(MapSubmitOutcome.Started, service.Submit(session!, Problem()).Outcome);
        Assert.True(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task RoutePlan_IsOnlyAvailableOnceTheRunHasCompleted()
    {
        var (service, _, queue) = Create();
        service.TryAuthorize(TokenOf(service.Open(5, 5, new FakeReplies(), Settings)), out var session);

        Assert.Null(service.GetRoutePlan(session!));

        service.Submit(session!, Problem());
        Assert.Null(service.GetRoutePlan(session!));

        Assert.True(queue.Reader.TryRead(out var job));
        await new JobExecutor().ExecuteAsync(job!);

        Assert.NotNull(service.GetRoutePlan(session!));
    }

    [Fact]
    public async Task RoutePlan_StartsAndEndsAtTheDepot_AndFollowsTheStoredSolution()
    {
        var (service, _, queue) = Create();
        service.TryAuthorize(TokenOf(service.Open(5, 5, new FakeReplies(), Settings)), out var session);

        // Longitude is x and latitude is y: the depot sits at latitude 50, longitude 10.
        var problem = new VrpProblem(
            10, 50,
            [new VrpCustomer(11, 51, 1), new VrpCustomer(12, 52, 1)],
            vehicles: 1,
            capacity: 5);

        service.Submit(session!, problem);
        Assert.True(queue.Reader.TryRead(out var job));
        await new JobExecutor().ExecuteAsync(job!);

        var status = service.GetStatus(session!);
        var plan = service.GetRoutePlan(session!)!;

        Assert.Equal(status.JobId, plan.JobId);
        Assert.Equal(status.Solution!.Routes.Count, plan.Routes.Count);

        for (var index = 0; index < plan.Routes.Count; index++)
        {
            var stops = plan.Routes[index];
            var customers = status.Solution.Routes[index].Customers;

            Assert.Equal(customers.Count + 2, stops.Count);
            Assert.Equal(new MapStop(50, 10), stops[0]);
            Assert.Equal(new MapStop(50, 10), stops[^1]);

            for (var stop = 0; stop < customers.Count; stop++)
            {
                var customer = problem.Customers[customers[stop]];

                Assert.Equal(new MapStop(customer.Y, customer.X), stops[stop + 1]);
            }
        }
    }

    [Fact]
    public void Options_RejectABadBaseUrl_AndLifetimes()
    {
        var queue = new JobQueue();

        Assert.Throws<ArgumentException>(() =>
            new MapSessionService(queue, new MapSessionOptions { PublicBaseUrl = "not a url" }));
        Assert.Throws<ArgumentException>(() =>
            new MapSessionService(queue, new MapSessionOptions { UnusedLifetime = TimeSpan.Zero }));
    }
}
