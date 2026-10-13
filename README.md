<p align="center">
  <img src="docs/optinull-logo.png" alt="Optinull" width="600">
</p>

<p align="center">
  <strong>An optimization engine you talk to.</strong><br/>
  Solve, visualize and compare optimization algorithms on classic problems, straight from a Telegram chat.
</p>

<p align="center">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" />
  <img alt="C#" src="https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white" />
  <img alt="Telegram" src="https://img.shields.io/badge/interface-Telegram-26A5E4?logo=telegram&logoColor=white" />
  <img alt="Status" src="https://img.shields.io/badge/status-work%20in%20progress-orange" />
</p>

---

## What is Optinull?

Optinull is a small, clean-architecture **optimization engine written in C#** with a **Telegram bot** as its front end.

You send a problem (a built-in benchmark, a random instance, or your own data) and pick an algorithm. Optinull solves it in the background and sends back the score plus charts: a Gantt chart for a schedule, a route map for a tour, a convergence curve showing how the search progressed.

Its main purpose is to make heuristic solvers **easy to try and easy to compare**. Every solver runs against the same problem, with the same seed and the same evaluation budget, so the comparison is fair and reproducible.

```text
You:   /compare jobshop ft10
Bot:   Running Greedy, Hill Climbing, Simulated Annealing, Genetic Algorithm ...
Bot:   [comparison chart]  [convergence chart]  [Gantt chart]
```

## Features

| | |
|---|---|
| **Five solvers** | Greedy baseline, Hill Climbing, Iterated Local Search, Simulated Annealing, Genetic Algorithm |
| **Three problem types** | Job Shop Scheduling, Traveling Salesman, 0/1 Knapsack |
| **Built-in benchmarks** | `ft06` and `ft10` (with known optima 55 and 930), plus TSP and knapsack instances |
| **Bring your own data** | Paste an instance in the message, attach a `.txt` file, or step through a guided dialog |
| **Random instances** | Generate a problem of a given size from a seed |
| **Reproducible** | Same `seed` gives the same result; a default seed of `42` is used otherwise |
| **Side-by-side comparison** | `/compare` runs every solver on identical conditions |
| **Charts** | Convergence curves, solver comparison, Gantt, TSP route, knapsack selection (rendered with ScottPlot) |
| **Safe by design** | One active job per chat, bounded job queue, 60 s time limit per job, input size limits, `/cancel` |
| **Extensible** | New problem types plug in through a single module interface |

## Supported problems

| Id | Problem | Score | Built-in instances | Random-instance parameters |
|---|---|---|---|---|
| `jobshop` | Job shop scheduling | Makespan (minimize) | `ft06`, `ft10` | `jobs`, `machines` |
| `tsp` | Traveling salesman | Tour length (minimize) | `circle20`, `grid8`, `random50` | `cities` |
| `knapsack` | 0/1 knapsack | Total value (maximize) | `knap20`, `knap100` | `items` |

## Supported algorithms

| Keyword | Algorithm | Notes |
|---|---|---|
| `greedy` | Greedy baseline | Fast constructive heuristic. Also the reference for the "improvement over greedy" figure in results |
| `hc` | Hill Climbing | Local search over a neighborhood |
| `ils` | Iterated Local Search | Hill climbing, then perturb and climb again. Stops starting new restarts once the evaluation budget is spent |
| `sa` | Simulated Annealing | **Default.** Cooling rate is derived from the evaluation budget |
| `ga` | Genetic Algorithm | Tournament selection, elitism, mutation, and a local-search step. Job shop uses precedence-preserving crossover and insertion mutation |
| `all` | Compare all of the above | Same as `/compare` |

## Quick start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Telegram bot token from [@BotFather](https://t.me/BotFather)

### Run

```bash
git clone <your-repo-url> Optinull
cd Optinull

# Store the token outside the repository
dotnet user-secrets set "Telegram:BotToken" "<your-token>" --project src/Optinull.Bot

dotnet run --project src/Optinull.Bot
```

In production, use an environment variable instead of user secrets:

```bash
export Telegram__BotToken="<your-token>"
```

The bot refuses to start without a token. On startup it publishes its command menu to Telegram and begins long polling, so no public URL or webhook is needed.

### Test

```bash
dotnet test
```

## Using the bot

Open your bot in Telegram and send `/start`.

| Command | What it does |
|---|---|
| `/optimize` | Guided flow with buttons: **Problem → Dataset → Algorithm**. Choose *Custom* to enter your own data step by step |
| `/solve <problem> [solver] [iterations=N] [seed=N] [key=value ...]` | Solve one problem directly |
| `/compare <problem> [iterations=N] [seed=N] [key=value ...]` | Run every algorithm on the same problem |
| `/cancel` | Stop your current job |
| `/help` | List commands, built-in instances and input formats |

`<problem>` is either a problem id (`jobshop`, `tsp`, `knapsack`) or a built-in instance name (`ft06`, `random50`, ...).

`iterations` is the number of candidate solutions SA and GA may evaluate (100 to 100,000). Without it, each solver uses its own default budget.

### Examples

```text
/solve ft06                         # Simulated Annealing on the ft06 benchmark
/solve ft10 ga iterations=50000     # Genetic Algorithm with a larger budget
/solve ft10 ils                     # Iterated Local Search
/solve tsp cities=30 sa seed=7      # Random 30-city instance, reproducible
/compare knapsack items=50          # Every solver on a random knapsack
/compare random50                   # Every solver on a built-in TSP instance
```

### Custom input

Put the instance on the lines after the command, or attach a `.txt` file (max 30 KB) with the command as its caption.

**Job shop**: first line is `jobs machines`, then one line per job with `machine time` pairs. Machines are numbered from 0.

```text
/solve jobshop sa
3 3
0 3 1 2 2 2
0 2 2 1 1 4
1 4 2 3 0 1
```

**TSP**: one city per line as `x y`.

```text
/solve tsp hc
0 0
10 0
10 10
0 10
```

**Knapsack**: first line is the capacity, then `value weight` per item.

```text
/solve knapsack ga
50
60 10
100 20
120 30
```

## Architecture

Optinull follows a layered design. Dependencies point inwards: the domain knows nothing about Telegram, solvers, or charts.

```text
                    ┌───────────────────┐
                    │   Optinull.Bot    │  host + background workers
                    └─────────┬─────────┘
              ┌───────────────┴───────────────┐
    ┌─────────▼──────────┐          ┌─────────▼──────────┐
    │ Optinull.          │          │ Optinull.          │
    │ Infrastructure     │─────────►│ Application        │  commands, dialogs, job queue,
    │ (Telegram)         │          │                    │  solve + comparison services
    └────────────────────┘          └───┬────────┬───┬───┘
                                        │        │   │
                  ┌─────────────────────▼─┐ ┌────▼───▼────────┐ ┌──────────────┐
                  │ Optinull.Problems     │ │ Optinull.       │ │ Optinull.    │
                  │ JobShop · Tsp ·       │ │ Optimization    │ │ Reporting    │
                  │ Knapsack              │ │ solvers, search │ │ ScottPlot    │
                  └───────────┬───────────┘ └────────┬────────┘ └──────┬───────┘
                              └──────────────┬───────┴─────────────────┘
                                    ┌────────▼────────┐
                                    │ Optinull.Domain │  variables, constraints,
                                    └─────────────────┘  objectives, expressions
```

| Project | Responsibility |
|---|---|
| `Optinull.Domain` | Generic optimization model: variables, expressions, constraints, objectives, solutions and their evaluation |
| `Optinull.Optimization` | Problem-agnostic search: solvers, neighborhoods, random solution generation, solution comparison, seeded randomness |
| `Optinull.Problems` | Concrete problems with their evaluators, parsers, benchmarks and experiment runners |
| `Optinull.Reporting` | Chart renderers (convergence, comparison, Gantt, route, knapsack) |
| `Optinull.Application` | Chat command parsing, guided dialogs, problem registry, job queue and executor, solver comparison |
| `Optinull.Infrastructure` | Telegram integration: long polling, message and photo replies |
| `Optinull.Bot` | .NET Worker Service host and dependency injection setup |

### How a request flows

1. `TelegramPoller` receives a message and passes it to `BotCommandProcessor`, which handles each chat's messages in order.
2. `BotCommandParser` turns the text into a typed command.
3. The processor resolves the problem through the `ProblemRegistry`: a built-in, a generated instance, or parsed custom input.
4. The job goes into the bounded `JobQueue` (one active job per chat).
5. `JobWorker` picks it up and `JobExecutor` runs the solver with a time limit, then sends charts back through `IBotReplies`.

## Adding a new problem

Problems are plugged in as modules; the registry rejects duplicate names at startup.

1. Implement the problem, evaluator and search problem in `Optinull.Problems` (see `Tsp/` for the smallest example).
2. Implement `IProblemModule` in `Optinull.Application/Problems`: id, title, built-in names, custom-input help, random-instance parameters, presets and a dialog.
3. Register it in `ProblemRegistry.CreateDefault()`.

The help text, the `/optimize` menu and the `/solve` command pick it up from the module.

## Project layout

```text
Optinull/
├── src/
│   ├── Optinull.Domain/           # core model
│   ├── Optinull.Optimization/     # solvers and search machinery
│   ├── Optinull.Problems/         # JobShop, Tsp, Knapsack
│   ├── Optinull.Reporting/        # charts
│   ├── Optinull.Application/      # commands, dialogs, jobs
│   ├── Optinull.Infrastructure/   # Telegram
│   └── Optinull.Bot/              # entry point
├── tests/
│   ├── Optinull.Domain.Tests/
│   ├── Optinull.Optimization.Tests/
│   ├── Optinull.Problems.Tests/
│   ├── Optinull.Reporting.Tests/
│   ├── Optinull.Application.Tests/
│   └── Optinull.Integration.Tests/
└── Optinull.sln
```

## Testing

The test suite covers all layers: expression and constraint evaluation, solver behavior and reproducibility, parsers, benchmark checks (including the known FT06 optimum), job queue and executor behavior, the command parser and the guided dialogs.

```bash
dotnet test                                              # everything
dotnet test tests/Optinull.Problems.Tests                # one project
```

## Current status and roadmap

Optinull is a work in progress. Today it is a single-process bot with an in-memory queue and conversation state.

- [x] Domain model with expression-based constraints and objectives
- [x] Greedy, Hill Climbing, Simulated Annealing, Genetic Algorithm
- [x] Job shop, TSP, knapsack with benchmarks, parsers and random generators
- [x] Telegram bot with guided `/optimize` flow, `/solve`, `/compare`, `/cancel`
- [x] Charts: convergence, comparison, Gantt, route, knapsack
- [x] Progress tracking, time limits, per-chat job limits
- [x] Expose Iterated Local Search in the bot
- [ ] Expose solver tuning (temperature, population size, mutation rate, ...) through chat parameters
- [ ] Integration tests
- [ ] CI pipeline and container image
- [ ] Persistent job and conversation storage
- [ ] More problem types (for example vehicle routing, bin packing)
