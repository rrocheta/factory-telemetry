# AGENTS.md

## Project Overview

Factory Telemetry is a learning and portfolio project for a real-time IIoT platform.

Only the .NET 8 CNC telemetry simulator is currently implemented. The MQTT broker, backend API, PostgreSQL persistence, React dashboard, OEE calculation and Docker Compose environment are planned components and must not be treated as existing functionality.

## Current Structure

The implemented project is located at:

`simulator/FactoryTelemetry.Simulator/`

Main components:

- `SimulationEngine.cs`: machine state and telemetry simulation
- `MqttPublisher.cs`: MQTT connection and message publishing
- `SimulatorService.cs`: background simulation service
- `SimulationConfig.cs`: configuration loading and validation
- `Config/`: simulated programs, parts and production routes

## Build and Run

Run these commands from `simulator/FactoryTelemetry.Simulator/`:

```bash
dotnet restore
dotnet build
dotnet run
```

There is currently no automated test project.

## Engineering Guidelines

- Keep simulation logic separate from MQTT transport.
- Preserve deterministic simulations through machine-specific seeds.
- Use dependency injection and asynchronous I/O.
- Avoid blocking operations inside simulation loops.
- Keep telemetry payloads small and schema-consistent.
- Make state transitions explicit and testable.
- Prefer clear, maintainable code over unnecessary abstractions.
- Add tests when changing state transitions, configuration validation or production-routing behavior.

## Configuration

MQTT settings are defined in `appsettings.json`.

The machine identifier and deterministic seed can be configured through:

- `SIM_MACHINE_ID`
- `SIM_SEED`

Do not hardcode credentials or environment-specific configuration.

## MQTT Conventions

Topics use the following prefix:

`factory/{machineId}/`

Examples:

- `factory/cnc1/state`
- `factory/cnc1/heartbeat`
- `factory/cnc1/spindle/rpm`
- `factory/cnc1/production/partCompleted`

When adding topics:

- preserve the existing hierarchy;
- use predictable names;
- maintain payload compatibility;
- choose QoS deliberately;
- avoid publishing derived business metrics from the simulator.

## Scope Boundaries

The simulator is responsible for producing source telemetry and production events.

The following concerns belong to future external services:

- telemetry persistence;
- historical queries;
- aggregation;
- OEE and KPI calculation;
- REST APIs;
- WebSocket or SSE streaming;
- frontend visualization.

Do not introduce these responsibilities into the simulator.

## Planned Architecture

The intended data flow is:

`Simulator → MQTT Broker → Backend API → Database / WebSocket → React Dashboard`

Treat this as a planned architecture, not as implemented functionality. Update the README and this file whenever a new component becomes operational.
