<p align="center">
  <img src="docs/images/optinull-logo.webp" alt="Optinull" width="600">
</p>

<p align="center">
  <strong>An optimization engine you talk to.</strong><br>
  Solve, visualize, and compare heuristic algorithms on classic optimization problems—straight from Telegram.
</p>

<p align="center">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white">
  <img alt="C#" src="https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white">
  <img alt="Telegram" src="https://img.shields.io/badge/interface-Telegram-26A5E4?logo=telegram&logoColor=white">
  <img alt="Status" src="https://img.shields.io/badge/status-work%20in%20progress-orange">
</p>

<p align="center">
  <a href="docs/README.md"><u>Documentation</u></a>
  &nbsp;·&nbsp;
  <a href="docs/README.md#screenshots"><u>Screenshots</u></a>
</p>

---

## Optinull

Optinull is an optimization engine written in C# / .NET with a Telegram bot as its interface. You send a problem (a built-in benchmark, a random instance, or your own data) and pick an algorithm (or all of them to compare). Optinull solves it in the background and sends back the score plus charts: a Gantt chart for a schedule, a route map for a tour, a convergence curve showing how the search progressed. The main purpose is to make heuristic solvers easy to try and easy to compare. Every solver runs against the same problem, with the same seed and the same evaluation budget, so the comparison is fair and reproducible.

## Features

- **Five search strategies:** Greedy, Hill Climbing, Iterated Local Search, Simulated Annealing, and Genetic Algorithm.
- **Four optimization problems:** Job Shop Scheduling, Traveling Salesman Problem (TSP), 0/1 Knapsack, and Capacitated Vehicle Routing.
- **Comparable runs:** compare solvers on the same instance with the same seed and evaluation budget.
- **Reproducible experiments:** specify a seed; the default is `42`.
- **Interactive and direct workflows:** use `/optimize` for a guided flow or `/solve` for command-driven runs.
- **Automatic settings, optional overrides:** the engine chooses solver settings for each problem and evaluation budget; override any of them from chat, and every result lists the settings it used.
- **Visual results:** convergence and comparison charts, job-shop schedules, TSP routes, knapsack selections, and vehicle routes.
- **Bounded execution:** queued jobs, one active job per chat, cancellation, input limits, and a 60-second job timeout.

## Supported problems

| Problem | Objective | Built-in instances | Generate a random instance with |
|---|---|---|---|
| Job Shop Scheduling | Minimize makespan | `ft06`, `ft10` | `jobs=6 machines=6` |
| Traveling Salesman Problem | Minimize tour length | `circle20`, `grid8`, `random50` | `cities=20` |
| 0/1 Knapsack | Maximize total value | `knap20`, `knap100` | `items=30` |
| Vehicle Routing (CVRP) | Minimize total distance | `vrp15`, `vrp40` | `customers=20 vehicles=4` |

The job-shop benchmarks have known optimal makespans of **55** (`ft06`) and **930** (`ft10`). These values are reference optima, not a guarantee that every heuristic run will find them.

## Available solvers

| Command keyword | Solver | Summary |
|---|---|---|
| `greedy` | Greedy baseline | Fast constructive baseline |
| `hc` | Hill Climbing | Improves a solution through local search |
| `ils` | Iterated Local Search | Repeated local search with perturbations |
| `sa` | Simulated Annealing | Probabilistic local search; default solver |
| `ga` | Genetic Algorithm | Population-based search with selection, crossover, mutation, and local search |
| `all` | Compare all solvers | Runs all five strategies for comparison |

## Tuning

By default Optinull chooses the solver settings for each problem and budget. To change one, add it to the command:

```text
/solve ft10 sa temperature=2
/solve tsp cities=50 ga population=100 mutation=0.3
/compare knapsack items=60 mutation=0.1
```

Only the settings you give are overridden; everything else stays automatic. A single run lists everything the solver used, marked `(auto)` or `(yours)`, so a run can be reproduced from the chat:

```text
Settings: population=22 (auto), elite=2 (auto), mutation=0.3 (yours), tournament=3 (auto), localsearch=10 (auto)
```

`temperature` is relative to the problem's usual starting temperature, so the same value means the same thing on a tour, a schedule, a knapsack, and a delivery plan. Setting names, ranges, and the automatic rules are in the [documentation](docs/README.md#solver-tuning).

## Quick start

**Requirements:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and a Telegram bot token from [@BotFather](https://t.me/BotFather).

```bash
git clone https://github.com/Haflier/Optinull.git
cd Optinull

dotnet user-secrets set "Telegram:BotToken" "<your-telegram-bot-token>" \
  --project src/Optinull.Bot

dotnet run --project src/Optinull.Bot
```

Open your bot in Telegram and send `/start`. The application uses Telegram long polling, so a public webhook URL is not required.

For deployment, provide the token through the `Telegram__BotToken` environment variable rather than committing credentials:

```bash
export Telegram__BotToken="<your-telegram-bot-token>"
dotnet run --project src/Optinull.Bot
```

## Vehicle routing on a real map

Vehicle routing can also be set up with real places, with trucks following real roads (OSRM distances). It sits under the usual commands:

```text
/solve vrp map [greedy|hc|ils|sa|ga] [iterations=N] [seed=N]
/optimize  ->  Vehicle routing  ->  Map  ->  algorithm
```

Everything happens in the chat, and the bot is the only host:

1. Send the depot with Telegram's attach -> Location (pick any point or search for a place).
2. Send the customers the same way, one per message. A number sent right after a customer sets its demand (default 1). You can also paste lines such as `52.5156, 13.3897 4` (latitude, longitude, optional demand).
3. Press Done, then send the number of trucks and the capacity of one truck, for example `3 20`.
4. Press Solve. The bot fetches road distances, solves the problem in the background on the job queue, and sends a PNG with every truck's route along the roads (numbered stops, one colour per truck, load and distance) plus the convergence chart.

`/cancel` stops the dialog or the running job. The PNG is drawn on a plain background without street tiles. If the road shape of a truck cannot be loaded, that truck is drawn as straight lines and the caption says so.

Routing uses the public OSRM demo server by default, which is for prototyping only. Point `Osrm:BaseUrl` at your own instance for real use:

```bash
export Osrm__BaseUrl="https://<your osrm server>"
dotnet run --project src/Optinull.Bot
```

## Test

Run the full test suite from the repository root:

```bash
dotnet test
```

---

For command syntax, tuning settings and automatic rules, custom input formats, architecture, extension points, and testing details, see the **[<u>Optinull documentation</u>](docs/README.md)**.
