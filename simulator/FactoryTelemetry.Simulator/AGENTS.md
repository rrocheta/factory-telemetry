# Repository Guidelines

## Architecture Context

This repository contains the **industrial telemetry simulator** for a
larger real-time IIoT system composed of:

-   MQTT broker (Mosquitto)
-   Simulator (**this repository**)
-   Backend API (MQTT subscriber + processing)
-   PostgreSQL database
-   React real-time dashboard

The simulator is responsible for generating realistic machine telemetry
that feeds the event-driven pipeline.

For the full architecture, see the main project README.

------------------------------------------------------------------------

## Project Structure & Module Organization

`FactoryTelemetry.Simulator/` contains the .NET 8 simulator source code
and solution files.

**Key modules:**

-   `SimulationEngine.cs`\
    Implements the machine state machine and telemetry generation logic.

-   `MqttPublisher.cs`\
    Handles MQTT connectivity and message publishing.

-   `SimulatorService.cs`\
    Hosted background service responsible for running the simulation
    loop.

-   `Program.cs`\
    Dependency injection setup and host configuration.

Build artifacts are generated under:

    FactoryTelemetry.Simulator/bin/
    FactoryTelemetry.Simulator/obj/

------------------------------------------------------------------------

## Design Principles

-   Prefer deterministic behavior over excessive randomness to keep
    simulations reproducible.
-   Keep telemetry payloads small, predictable, and schema-consistent.
-   Separate simulation logic from transport concerns (MQTT).
-   Favor small, single-purpose methods for clarity inside simulation
    loops.
-   Make state transitions explicit and testable.
-   Design with horizontal scalability in mind (multiple machines).

------------------------------------------------------------------------

## Simulator Scope Boundaries

This repository implements an **industrial telemetry and production
event simulator**.

### In Scope
- Publish source-of-truth machine events via MQTT
- Emit machine state change events (`RUN | IDLE | DOWN`)
- Emit production events:
  - `OperationCompleted`
  - `PartCompleted`
- Publish raw telemetry signals (temperature, vibration, power, etc.)
- Publish heartbeat signals
- Remain deterministic and configuration-driven

### Explicitly Out of Scope
- KPI or OEE calculation of any kind
- Time-window aggregation or statistics
- Historical data persistence
- Business or analytics logic

Any derived metrics or KPIs must be computed by **external services**
subscribing to the simulator’s MQTT events.

------------------------------------------------------------------------

## Build, Test, and Development Commands

Run commands from `FactoryTelemetry.Simulator/` unless noted.

``` bash
dotnet restore     # Restore NuGet packages
dotnet build       # Build the simulator
dotnet run         # Run as console application
dotnet test        # Execute tests (when a test project exists)
```

------------------------------------------------------------------------

## Coding Style & Naming Conventions

-   Target framework: **.NET 8 / C# 10+**
-   Nullable reference types enabled
-   Implicit usings enabled

**Naming**

-   `PascalCase` → types, properties, methods\
-   `camelCase` → locals and parameters\
-   `I` prefix → interfaces (e.g., `IPublisher`)

**General Guidelines**

-   Use async/await for I/O-bound work.
-   Avoid blocking calls inside simulation loops.
-   Prefer dependency injection over manual instantiation.
-   Keep classes focused --- avoid "God objects".

------------------------------------------------------------------------

## MQTT Conventions

Topics follow a hierarchical and scalable pattern:

    factory/{machineId}/{signal}

**Examples:**

    factory/cnc1/state
    factory/cnc1/temperature
    factory/cnc1/vibration

When introducing new topics:

-   Maintain naming consistency\
-   Avoid deeply nested structures\
-   Keep payload schemas stable

Default broker configuration:

    localhost:1883

Update host and credentials in `MqttPublisher.cs` when targeting another
environment.

------------------------------------------------------------------------

## Logging & Observability

The simulator uses `Microsoft.Extensions.Logging`.

Logs should provide enough context to:

-   trace state transitions\
-   diagnose publishing failures\
-   understand simulator behavior

Prefer structured logging whenever possible.

------------------------------------------------------------------------

## Testing Guidelines

There is currently no test project.

When adding tests:

-   Create a separate `*.Tests.csproj`

-   Name files using `*Tests.cs`

-   Focus on:

    -   state transitions\
    -   payload formatting\
    -   simulation boundaries

Prioritize meaningful tests over artificial coverage targets.

------------------------------------------------------------------------

## Commit & Pull Request Guidelines

Follow **Conventional Commits**:

    feat: add spindle vibration simulation
    fix: prevent negative temperature values
    chore(simulator): refactor state timing
    docs: update mqtt topic structure

**Pull Requests should include:**

-   concise description\
-   commands executed (or "not run")\
-   any MQTT topic/schema changes\
-   relevant architectural notes

------------------------------------------------------------------------

## Configuration Notes

-   MQTT defaults to `localhost:1883`.
-   Topic prefix is `factory/` --- keep new signals consistent.
-   The simulator is designed for containerized environments but can run
    standalone for development.

------------------------------------------------------------------------

## Scope

This project is intentionally scoped for:

✔ learning\
✔ portfolio demonstration\
✔ real-time architecture practice

It favors **clarity and architectural correctness** over premature
complexity.
