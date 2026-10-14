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
</p>

---

## Optinull

Optinull is an optimization engine written in C# / .NET with a Telegram bot as its interface. You send a problem (a built-in benchmark, a random instance, or your own data) and pick an algorithm (or all of them to compare). Optinull solves it in the background and sends back the score plus charts: a Gantt chart for a schedule, a route map for a tour, a convergence curve showing how the search progressed. The main purpose is to make heuristic solvers easy to try and easy to compare. Every solver runs against the same problem, with the same seed and the same evaluation budget, so the comparison is fair and reproducible.

## Features

- **Five search strategies:** Greedy, Hill Climbing, Iterated Local Search, Simulated Annealing, and Genetic Algorithm.
- **Three optimization problems:** Job Shop Scheduling, Traveling Salesman Problem (TSP), and 0/1 Knapsack.
- **Comparable runs:** compare solvers on the same instance with the same seed and evaluation budget.
- **Reproducible experiments:** specify a seed; the default is `42`.
- **Interactive and direct workflows:** use `/optimize` for a guided flow or `/solve` for command-driven runs.
- **Configurable solvers:** tune simulated annealing, genetic algorithm, and iterated local search from chat.
- **Visual results:** convergence and comparison charts, job-shop schedules, TSP routes, and knapsack selections.
- **Bounded execution:** queued jobs, one active job per chat, cancellation, input limits, and a 60-second job timeout.

## Supported problems

| Problem | Objective | Built-in instances | Generate a random instance with |
|---|---|---|---|
| Job Shop Scheduling | Minimize makespan | `ft06`, `ft10` | `jobs=6 machines=6` |
| Traveling Salesman Problem | Minimize tour length | `circle20`, `grid8`, `random50` | `cities=20` |
| 0/1 Knapsack | Maximize total value | `knap20`, `knap100` | `items=30` |

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

## Test

Run the full test suite from the repository root:

```bash
dotnet test
```

---

For command syntax, tuning ranges, custom input formats, architecture, extension points, and testing details, see the **[<u>Optinull documentation</u>](docs/README.md)**.
