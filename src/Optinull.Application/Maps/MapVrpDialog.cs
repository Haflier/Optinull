using System.Globalization;
using Optinull.Application.Commands;
using Optinull.Application.Routing;
using Optinull.Problems.Vrp;

namespace Optinull.Application.Maps;

internal enum MapPhase
{
    Depot,
    Customers,
    Fleet,
    Ready
}

/// <summary>What the bot says next, and the buttons under it.</summary>
internal sealed record MapReply(string Text, IReadOnlyList<IReadOnlyList<Choice>> Buttons);

/// <summary>A complete map problem: places in degrees, whole demands, and the fleet.</summary>
internal sealed record MapInput(
    GeoPoint Depot,
    IReadOnlyList<GeoPoint> Customers,
    IReadOnlyList<int> Demands,
    int Vehicles,
    int Capacity);

/// <summary>
/// Collects a routing problem from Telegram location messages (or typed "lat, lon" lines):
/// the depot, the customers with optional demands, then trucks and capacity.
/// A dialog belongs to one chat.
/// </summary>
internal sealed class MapVrpDialog
{
    public const string DoneData = "m:done";
    public const string UndoData = "m:undo";
    public const string SolveData = "m:solve";

    private const int MaxEchoes = 4;

    private readonly object _gate = new();
    private readonly VrpInputLimits _limits = VrpInputLimits.Default;
    private readonly int _maxCustomers;
    private readonly List<GeoPoint> _customers = [];
    private readonly List<int> _demands = [];

    private GeoPoint? _depot;
    private int _vehicles;
    private int _capacity;
    private MapPhase _phase = MapPhase.Depot;

    /// <param name="maxLocations">The routing service's limit, depot included.</param>
    public MapVrpDialog(int maxLocations)
    {
        _maxCustomers = Math.Max(1, Math.Min(_limits.MaxCustomers, maxLocations - 1));
    }

    public MapPhase Phase
    {
        get
        {
            lock (_gate)
                return _phase;
        }
    }

    public string Start() =>
        "Let's put your delivery problem on the map.\n\n" +
        "First send the depot, where the trucks start and end: tap the paperclip, choose Location, " +
        "then pick any point or search for a place. You can also type coordinates like 52.5156, 13.3897.";

    public MapReply AddLocation(double latitude, double longitude)
    {
        lock (_gate)
        {
            if (!IsCoordinate(latitude, longitude))
                return Reply("That is not a valid latitude and longitude.");

            // A new place after the fleet was set means the user is still adding customers.
            if (_phase is MapPhase.Fleet or MapPhase.Ready)
                _phase = MapPhase.Customers;

            var error = AcceptPoint(new GeoPoint(latitude, longitude), null, out var echo);

            return Reply(error ?? echo);
        }
    }

    public MapReply AddText(string text)
    {
        lock (_gate)
        {
            var echoes = new List<string>();
            var accepted = 0;
            string? problem = null;

            var lines = (text ?? string.Empty)
                .Replace("\r", string.Empty)
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0);

            foreach (var line in lines)
            {
                if (line.Equals("undo", StringComparison.OrdinalIgnoreCase))
                {
                    echoes.Add(UndoCore());
                    accepted++;
                    continue;
                }

                if (line.Equals("done", StringComparison.OrdinalIgnoreCase))
                {
                    var doneError = DoneCore(out var doneEcho);

                    if (doneError is not null)
                    {
                        problem = doneError;
                        break;
                    }

                    echoes.Add(doneEcho);
                    accepted++;
                    continue;
                }

                var error = ParseLine(line, out var echo);

                if (error is not null)
                {
                    var shown = line.Length > 40 ? line[..40] + "..." : line;

                    problem = $"\"{shown}\": {error}";
                    break;
                }

                echoes.Add(echo);
                accepted++;
            }

            // A long paste would make a message too long, so recap it briefly.
            if (echoes.Count > MaxEchoes)
                echoes = [$"Accepted {accepted} lines."];

            if (problem is not null)
                echoes.Add($"That line was not used - {problem}");

            return Reply(string.Join("\n", echoes));
        }
    }

    public MapReply Done()
    {
        lock (_gate)
        {
            var error = DoneCore(out var echo);

            return Reply(error ?? echo);
        }
    }

    public MapReply Undo()
    {
        lock (_gate)
            return Reply(UndoCore());
    }

    /// <summary>The finished problem. Fails with a message when something is still missing.</summary>
    public bool TryBuild(out MapInput? input, out string? error)
    {
        lock (_gate)
        {
            input = null;

            if (_phase != MapPhase.Ready || _depot is not { } depot)
            {
                error = "The problem is not complete yet. " + Prompt();
                return false;
            }

            input = new MapInput(depot, _customers.ToArray(), _demands.ToArray(), _vehicles, _capacity);
            error = null;
            return true;
        }
    }

    // ---- Lines ---------------------------------------------------------------------------------

    private string? ParseLine(string line, out string echo)
    {
        echo = string.Empty;

        var tokens = line.Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        switch (_phase)
        {
            case MapPhase.Depot:
                if (tokens.Length != 2)
                    return "send the depot as a Location, or as \"latitude, longitude\".";

                return TryPoint(tokens[0], tokens[1], out var depot)
                    ? AcceptPoint(depot, null, out echo)
                    : "latitude must be -90 to 90 and longitude -180 to 180.";

            case MapPhase.Customers:
                if (tokens.Length == 1)
                    return SetLastDemand(tokens[0], out echo);

                if (tokens.Length is 2 or 3)
                {
                    int? demand = null;

                    if (tokens.Length == 3)
                    {
                        if (!int.TryParse(tokens[2], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                            return "the demand must be a whole number.";

                        demand = value;
                    }

                    if (!TryPoint(tokens[0], tokens[1], out var point))
                        return "latitude must be -90 to 90 and longitude -180 to 180.";

                    return AcceptPoint(point, demand, out echo);
                }

                return "send a Location, \"latitude, longitude\", or a number for the last customer's demand.";

            default:
                return SetFleet(tokens, out echo);
        }
    }

    // Accepts the depot, or a customer with a demand (1 when none was given).
    private string? AcceptPoint(GeoPoint point, int? demand, out string echo)
    {
        echo = string.Empty;

        if (_phase == MapPhase.Depot)
        {
            _depot = point;
            _phase = MapPhase.Customers;
            echo = $"Depot set ({Format(point)}).";
            return null;
        }

        if (_customers.Count >= _maxCustomers)
            return $"that is the maximum of {_maxCustomers} customers.";

        var value = demand ?? 1;

        if (value < 1 || value > _limits.MaxCapacity)
            return $"the demand must be a whole number from 1 to {_limits.MaxCapacity}.";

        _customers.Add(point);
        _demands.Add(value);
        echo = $"Customer {_customers.Count} added, demand {value} ({Format(point)}).";
        return null;
    }

    private string? SetLastDemand(string token, out string echo)
    {
        echo = string.Empty;

        if (_customers.Count == 0)
            return "send a customer location first.";

        if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var demand) ||
            demand < 1 || demand > _limits.MaxCapacity)
        {
            return $"the demand must be a whole number from 1 to {_limits.MaxCapacity}.";
        }

        _demands[^1] = demand;
        echo = $"Customer {_customers.Count}: demand set to {demand}.";
        return null;
    }

    private string? SetFleet(string[] tokens, out string echo)
    {
        echo = string.Empty;

        if (tokens.Length != 2 ||
            !int.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out var vehicles) ||
            !int.TryParse(tokens[1], NumberStyles.None, CultureInfo.InvariantCulture, out var capacity))
        {
            return "send the number of trucks and their capacity, for example \"3 20\".";
        }

        if (vehicles < 1 || vehicles > _limits.MaxVehicles)
            return $"the number of trucks must be from 1 to {_limits.MaxVehicles}.";

        if (capacity < 1 || capacity > _limits.MaxCapacity)
            return $"the capacity must be from 1 to {_limits.MaxCapacity}.";

        var largest = _demands.Max();

        if (largest > capacity)
            return $"a customer needs {largest}, which is more than the capacity {capacity}.";

        var required = VrpProblem.RequiredVehicles(_demands, capacity);

        if (required > vehicles)
            return $"these loads need at least {required} trucks of capacity {capacity}, but you gave {vehicles}.";

        _vehicles = vehicles;
        _capacity = capacity;
        _phase = MapPhase.Ready;
        echo = $"{vehicles} trucks, capacity {capacity}.";
        return null;
    }

    private string? DoneCore(out string echo)
    {
        echo = string.Empty;

        switch (_phase)
        {
            case MapPhase.Depot:
                return "send the depot first.";

            case MapPhase.Customers when _customers.Count == 0:
                return "add at least one customer first.";

            case MapPhase.Customers:
                _phase = MapPhase.Fleet;
                echo = $"{_customers.Count} customers, total demand {_demands.Sum()}.";
                return null;

            default:
                return "the customers are already finished.";
        }
    }

    private string UndoCore()
    {
        switch (_phase)
        {
            case MapPhase.Fleet:
            case MapPhase.Ready:
                _phase = MapPhase.Customers;
                return "Back to the customers.";

            case MapPhase.Customers when _customers.Count > 0:
                var number = _customers.Count;

                _customers.RemoveAt(number - 1);
                _demands.RemoveAt(number - 1);
                return $"Removed customer {number}.";

            case MapPhase.Customers when _depot is not null:
                _depot = null;
                _phase = MapPhase.Depot;
                return "Removed the depot.";

            default:
                return "There is nothing to undo yet.";
        }
    }

    // ---- Replies -------------------------------------------------------------------------------

    private MapReply Reply(string note)
    {
        var text = note.Length == 0 ? Prompt() : $"{note}\n\n{Prompt()}";

        return new MapReply(text, Buttons());
    }

    private string Prompt() =>
        _phase switch
        {
            MapPhase.Depot =>
                "Send the depot as a Location (paperclip, Location) or as \"latitude, longitude\".",

            MapPhase.Customers when _customers.Count == 0 =>
                $"Now send the customers, one Location per message (up to {_maxCustomers}). " +
                "Send a number right after one to set its demand (otherwise it is 1). " +
                "You can also paste several lines like \"52.5156, 13.3897 4\".",

            MapPhase.Customers =>
                $"{_customers.Count} of at most {_maxCustomers} customers so far. Send a number to set the " +
                "last one's demand, another Location to add the next, or press Done.",

            MapPhase.Fleet =>
                $"Total demand is {_demands.Sum()}. Send the number of trucks and the capacity of one truck, " +
                "for example \"3 20\".",

            _ =>
                $"Ready: {_customers.Count} customers, total demand {_demands.Sum()}, " +
                $"{_vehicles} trucks of capacity {_capacity}. Press Solve."
        };

    private IReadOnlyList<IReadOnlyList<Choice>> Buttons() =>
        _phase switch
        {
            MapPhase.Customers when _customers.Count > 0 =>
                [[new Choice("Done", DoneData), new Choice("Undo", UndoData)]],

            MapPhase.Fleet => [[new Choice("Back to customers", UndoData)]],

            MapPhase.Ready =>
                [[new Choice("Solve", SolveData)], [new Choice("Back to customers", UndoData)]],

            _ => []
        };

    // ---- Coordinates ---------------------------------------------------------------------------

    private static bool TryPoint(string latitude, string longitude, out GeoPoint point)
    {
        point = default;

        if (!double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
            !IsCoordinate(lat, lon))
        {
            return false;
        }

        point = new GeoPoint(lat, lon);
        return true;
    }

    private static bool IsCoordinate(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude) &&
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static string Format(GeoPoint point) =>
        $"{point.Latitude.ToString("0.#####", CultureInfo.InvariantCulture)}, " +
        point.Longitude.ToString("0.#####", CultureInfo.InvariantCulture);
}
