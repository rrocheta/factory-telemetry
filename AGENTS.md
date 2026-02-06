# Engineering Guidelines

## System Overview

This repository represents a **reference architecture for a real-time
IIoT platform**, designed for learning and portfolio purposes.\
It demonstrates how industrial telemetry can be simulated, transported,
processed, stored, and visualized using modern software practices.

The system favors **architectural clarity, realistic industrial
patterns, and maintainability** over production-grade completeness.

------------------------------------------------------------------------

## Architecture Principles

-   Prefer event-driven communication over tight coupling.
-   Keep services independently deployable.
-   Separate transport, processing, and visualization concerns.
-   Favor deterministic behavior when simulating industrial processes.
-   Design for horizontal scalability (multiple machines).
-   Optimize for observability and debuggability.

------------------------------------------------------------------------

## Core Architectural Rule

👉 **The frontend must never connect directly to the MQTT broker.**

All external access flows through the API layer to ensure:

-   security\
-   authentication boundaries\
-   scalability\
-   protocol abstraction

------------------------------------------------------------------------

## System Components

### MQTT Broker (Mosquitto)

Responsible for reliable message transport.

-   user/password authentication\
-   topic-based ACLs\
-   lightweight pub/sub communication

------------------------------------------------------------------------

### Simulator

Generates realistic industrial telemetry using a state-machine approach.

Responsibilities:

-   simulate machine states (RUN / IDLE / ALARM)\
-   publish telemetry signals\
-   emulate industrial cadence patterns

Each simulator instance should behave like an independent machine.

------------------------------------------------------------------------

### Backend API

Acts as the bridge between MQTT and external consumers.

Responsibilities:

-   subscribe to telemetry topics\
-   process incoming data\
-   expose REST endpoints\
-   stream updates via WebSocket/SSE\
-   calculate simplified OEE metrics\
-   persist telemetry

The API is the **system boundary**.

------------------------------------------------------------------------

### Database (PostgreSQL)

Stores telemetry and derived metrics.

Used for:

-   historical queries\
-   aggregation\
-   KPI calculations\
-   trend visualization

Favor simple schemas before introducing time-series specialization.

------------------------------------------------------------------------

### Frontend Dashboard

Provides real-time visualization of machine data.

Guidelines:

-   UI rendering should be throttled when necessary.
-   Avoid reflecting raw telemetry frequency directly.
-   Optimize for readability over visual noise.

The dashboard should resemble a lightweight SCADA/HMI experience.

------------------------------------------------------------------------

## Data Flow

Simulator\
↓\
MQTT Broker\
↓\
API (subscribe + process)\
↓\
WebSocket / SSE\
↓\
Frontend

------------------------------------------------------------------------

## Topic Conventions

Topics follow a scalable hierarchical pattern:

    factory/{machineId}/{signal}

Example:

    factory/cnc1/state
    factory/cnc1/temperature
    factory/cnc1/vibration

### Guidelines

-   Avoid deep topic nesting.
-   Keep signal names predictable.
-   Maintain payload schema stability.
-   Prefer numeric/boolean payloads for simplicity.

------------------------------------------------------------------------

## Observability

All services should provide:

-   structured logging\
-   meaningful error messages\
-   startup diagnostics

Future improvements may include:

-   distributed tracing\
-   metrics collection\
-   health checks

------------------------------------------------------------------------

## Service Documentation

Each service must maintain its own `AGENTS.md` describing:

-   internal structure\
-   coding conventions\
-   configuration\
-   operational notes

This document focuses only on **system-wide engineering decisions**.

------------------------------------------------------------------------

## Repository Strategy

Recommended structure for the platform:

    root
    │
    ├── README.md
    ├── AGENTS.md
    ├── ARCHITECTURE.md (optional)
    │
    ├── simulator/
    ├── api/
    ├── frontend/
    └── docs/

Avoid duplicating cross-service rules.

If a topic grows large, move it to `/docs` and reference it.

------------------------------------------------------------------------

## Design Tradeoffs

This project intentionally prioritizes:

✔ architectural realism\
✔ clarity\
✔ educational value

Over:

✖ production-scale optimization\
✖ premature complexity\
✖ vendor-specific tooling

------------------------------------------------------------------------

## Future Evolution Guidelines

When expanding the system:

-   Prefer adding services over overloading existing ones.
-   Maintain clear boundaries.
-   Revisit architectural decisions deliberately.
-   Document major changes.

Avoid turning the platform into a monolith.

------------------------------------------------------------------------

## Scope

This project is designed to demonstrate:

-   real-time systems thinking\
-   event-driven design\
-   industrial data modeling\
-   containerized infrastructure\
-   KPI transformation

It is not intended to replicate a full MES or SCADA platform.

------------------------------------------------------------------------

## Engineering Mindset

Favor:

-   simplicity\
-   explicitness\
-   predictability

Over clever abstractions.

Readable systems scale better than smart ones.
