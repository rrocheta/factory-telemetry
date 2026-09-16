# 📊 Factory Telemetry

A work-in-progress IIoT platform for simulating, transporting, processing and visualizing industrial machine telemetry using a modern event-driven architecture.

The project is designed as a learning and portfolio initiative inspired by real industrial environments, with a focus on architectural clarity, realistic telemetry and maintainable software.

> [!IMPORTANT]
> **Current status:** Only the .NET 8 CNC telemetry simulator and MQTT publishing component are currently implemented.
>
> The MQTT broker configuration, backend API, PostgreSQL persistence, React dashboard, OEE calculation and Docker Compose environment are planned and under active development.

---

## 🎯 Project Goals

- Simulate realistic CNC machine telemetry
- Explore real-time, event-driven data pipelines
- Publish industrial data using MQTT
- Support multiple simulated machines
- Process and store telemetry through a backend service
- Visualize live data through a responsive React dashboard
- Transform raw telemetry into production KPIs such as OEE
- Run the complete platform locally using Docker Compose

---

## ✅ Currently Implemented

The current implementation provides a configurable .NET 8 CNC telemetry simulator.

It includes:

- Deterministic simulation through machine-specific seeds
- Machine states: `RUN`, `IDLE` and `DOWN`
- Axis position simulation
- Spindle speed, power and vibration simulation
- Spindle and motor temperature simulation
- Heartbeat and machine-state events
- Production counters
- Configurable parts, programs and production routes
- Operation and part completion events
- Good and rejected part simulation
- Different MQTT publishing cadences
- Automatic MQTT reconnection with progressive backoff
- Structured console logging
- Configuration through JSON and environment variables

---

## 🧪 CNC Telemetry Simulator

The simulator models the behaviour of a CNC machine through a state-machine-based approach.

### Machine states

- `RUN`: the machine executes a simulated production cycle
- `IDLE`: the machine is available but not producing
- `DOWN`: the machine is stopped by a simulated alarm

State transitions and production values are deterministic when using the same machine identifier or simulation seed.

### Simulated signals

- X, Y and Z axis positions
- Spindle RPM
- Power consumption
- Vibration
- Spindle temperature
- Motor temperature
- Machine state
- Heartbeat
- Good, rejected and total production counters

### Production events

The simulator also publishes business-relevant production events:

- `operationCompleted`
- `partCompleted`
- current part, operation and program
- ideal and actual cycle times
- production result (`OK` or `NOK`)

Parts, programs and production routes are defined through configuration files located in:

```text
simulator/FactoryTelemetry.Simulator/Config/
```

---

## 📡 MQTT Topics

Topics follow a hierarchical structure:

```text
factory/{machineId}/{signal}
```

Examples:

```text
factory/cnc1/state
factory/cnc1/heartbeat
factory/cnc1/axis/x/pos
factory/cnc1/spindle/rpm
factory/cnc1/spindle/power
factory/cnc1/spindle/vibration
factory/cnc1/spindle/temp
factory/cnc1/motor/temp
factory/cnc1/production/current
factory/cnc1/production/goodCount
factory/cnc1/production/badCount
factory/cnc1/production/operationCompleted
factory/cnc1/production/partCompleted
```

Telemetry is published using different cadences to represent realistic industrial behaviour:

- Axis positions: 250 ms
- Spindle telemetry: 500 ms
- Machine state and heartbeat: 1 second
- Temperatures: 2 seconds
- Production events: event-driven

---

## 🧱 Planned Architecture

The intended data flow is:

```text
CNC Simulator
      ↓
MQTT Broker
      ↓
Backend API
      ↓
PostgreSQL
      ↓
WebSocket / SSE
      ↓
React Dashboard
```

### Planned components

#### MQTT Broker

Mosquitto will provide message transport with:

- username and password authentication
- topic-based access control
- containerized local configuration

#### Backend API

The backend service will:

- subscribe to MQTT topics
- validate and process telemetry
- persist relevant data
- expose REST endpoints
- stream live updates through WebSocket or SSE
- calculate simplified OEE metrics

#### PostgreSQL

The database will store:

- telemetry history
- machine-state intervals
- production events
- aggregated metrics
- OEE values

#### React Dashboard

The responsive dashboard will provide:

- machine status cards
- live telemetry values
- real-time charts
- production counters
- alarm visualization
- OEE indicators and trends
- desktop and mobile support

The frontend will not connect directly to the MQTT broker. All external access will flow through the backend API to maintain clear security, scalability and protocol boundaries.

---

## 🧰 Technology Stack

| Layer | Technology | Status |
|---|---|---|
| Simulator | C# / .NET 8 | Implemented |
| Messaging | MQTT / MQTTnet | Implemented |
| Broker | Mosquitto | Planned |
| Backend | ASP.NET Core API | Planned |
| Database | PostgreSQL | Planned |
| Real-time communication | WebSocket / SSE | Planned |
| Frontend | React | Planned |
| Infrastructure | Docker Compose | Planned |

---

## ▶️ Running the Simulator

### Requirements

- .NET 8 SDK
- An MQTT broker available at `localhost:1883`

Navigate to the simulator directory:

```bash
cd simulator/FactoryTelemetry.Simulator
```

Restore dependencies:

```bash
dotnet restore
```

Build the project:

```bash
dotnet build
```

Run the simulator:

```bash
dotnet run
```

By default, the simulator connects to:

```text
localhost:1883
```

MQTT settings can be changed in:

```text
appsettings.json
```

---

## ⚙️ Environment Variables

The simulator supports the following environment variables:

| Variable | Description |
|---|---|
| `SIM_MACHINE_ID` | Identifier used in MQTT topics |
| `SIM_SEED` | Optional deterministic random seed |

Example:

```bash
SIM_MACHINE_ID=cnc2 SIM_SEED=1234 dotnet run
```

When no seed is provided, a stable seed is generated from the machine identifier.

---

## 🔮 Roadmap

- [x] Implement CNC telemetry simulator
- [x] Add MQTT publishing
- [x] Add configurable parts and programs
- [x] Add production-routing events
- [ ] Add automated tests
- [ ] Add Mosquitto configuration
- [ ] Add Docker support for the simulator
- [ ] Implement the backend API
- [ ] Add PostgreSQL persistence
- [ ] Add WebSocket or SSE streaming
- [ ] Implement the React dashboard
- [ ] Calculate simplified OEE metrics
- [ ] Add multi-machine dashboard support
- [ ] Provide a complete Docker Compose environment

---

## 💡 What This Project Demonstrates

- Real-time systems thinking
- Event-driven architecture
- MQTT communication
- Industrial telemetry modelling
- CNC production concepts
- Deterministic state-machine simulation
- Configuration-driven software
- Asynchronous .NET services
- Separation between simulation and transport concerns
- Planning and documentation of an evolving distributed system

---

## 👨‍💻 Author

**Ricardo Rocheta**

- GitHub: [github.com/rrocheta](https://github.com/rrocheta)
- LinkedIn: [linkedin.com/in/ricardorocheta](https://linkedin.com/in/ricardorocheta)
