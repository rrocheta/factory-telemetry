# 📊 Real-time IIoT Dashboard (MQTT)

A real-time industrial dashboard developed as a learning-driven project to explore realistic IIoT architectures, demonstrating how MQTT-based telemetry can be processed, visualized, and transformed into meaningful production KPIs using a modern web stack.

The project simulates industrial machine data and displays it in real time with different update cadences, while also calculating a simplified **OEE (Overall Equipment Effectiveness)**.

This project is **educational**, intentionally scoped, and focused on **realistic industrial architecture rather than production completeness**.

---

## 🎯 Project Goals

- Learn and demonstrate **real-time data pipelines**
- Work with **MQTT (Mosquitto)** in a realistic industrial setup
- Visualize telemetry with **different cadences** (200 ms → seconds)
- Bridge MQTT data to the frontend via **WebSockets / SSE**
- Store telemetry and calculate **industrial KPIs (mini-OEE)**
- Run the entire stack locally using **Docker Compose**

---

## 🧠 Key Design Decisions

- The frontend **never connects directly to the MQTT broker**, reflecting real industrial security and scalability constraints.
- Telemetry may arrive at high frequency, but **UI rendering is intentionally throttled** to simulate realistic HMI/SCADA behavior.
- OEE is implemented as a **simplified model**, focusing on KPI transformation rather than production-grade MES accuracy.
- The simulator uses a **state-machine-based approach** to produce deterministic yet realistic behavior.

---

## 🧱 Architecture Overview

### Core Principle

The frontend **does not connect directly to the MQTT broker**.  
All MQTT communication is handled by a backend service acting as a bridge.

### Services (Docker Compose)

- **mosquitto**
  - MQTT broker
  - user/password authentication
  - topic-based ACLs

- **simulator**
  - publishes simulated industrial telemetry

- **api**
  - subscribes to MQTT topics
  - exposes REST + WebSocket/SSE APIs
  - stores telemetry in a database
  - calculates simplified OEE metrics

- **db**
  - PostgreSQL database for telemetry and KPI storage

- **frontend**
  - real-time dashboard (React)

### Data Flow

```
Simulator
   ↓
MQTT Broker (Mosquitto)
   ↓
API (subscribe + process)
   ↓
WebSocket / SSE
   ↓
Frontend Dashboard
```

---

## 📊 Dashboard Pages

### 1️⃣ Real-time Dashboard

Displays live machine data with **different update cadences**, simulating a real industrial environment.

**Examples of signals:**
- Axis positions (200 ms)
- Vibration and power (500 ms)
- Temperatures (2–5 s)
- Machine state and heartbeat

**UI elements:**
- Status cards (online/offline, machine state)
- Live values (temperature, vibration, power)
- Real-time charts (last 10–30 seconds)
- Cadence indicators (200 ms / 500 ms / seconds)

> Telemetry may be received at high frequency, but UI updates are intentionally throttled to ensure smooth performance and realistic visualization.

---

### 2️⃣ OEE Dashboard

Transforms raw telemetry into **business-oriented KPIs**.

**Metrics:**
- Availability
- Performance
- Quality
- Overall OEE

**Derived from:**
- Machine state durations (Run / Idle / Alarm)
- Simulated cycle times vs ideal times
- Basic good / scrap counters

**UI elements:**
- KPI cards
- OEE trend chart
- Run / Idle / Alarm time summary

---

## 🧪 Data Simulator

A dedicated container simulates industrial machine behavior using a simple state machine:

- **RUN** (2–5 minutes)
- **IDLE** (30–60 seconds)
- **ALARM** (10–20 seconds, rare)

Simulated behavior includes:
- Axis motion with noise
- Temperature increase and cooldown
- Vibration changes under load
- Power consumption variation
- Production counters

This produces **realistic and visually convincing telemetry**, suitable for demonstrating real-time visualization and KPI calculation.

---

## 🗄️ Data Storage

Telemetry data is stored in PostgreSQL using a simple time-series model:

- timestamp
- signal identifier (topic)
- numeric or boolean value

Used for:
- historical views
- short-term aggregation
- OEE calculations
- trend visualization

---

## 🧰 Tech Stack

| Layer      | Technology |
|-----------|------------|
| Frontend  | React + AdminLTE |
| Backend   | API with MQTT client + WebSocket/SSE |
| Messaging | MQTT (Mosquitto) |
| Database  | PostgreSQL |
| DevOps    | Docker & Docker Compose |

---

## 🐳 Running the Project

> ⚠️ Implementation in progress

The goal is to allow anyone to run the entire stack with:

```bash
docker compose up --build
```

and see live machine data in the dashboard in **under 30 seconds**, without manual setup.

---

## 🔮 Possible Future Improvements

- Persist aggregated machine snapshots instead of raw telemetry
- Add replay mode using historical data
- Support multiple simulated machines
- Introduce basic alerting rules
- Extend OEE logic with more detailed quality metrics

---

## 📌 Project Status

This project is under active development and may evolve as new features are added and existing components are refined as part of ongoing learning and experimentation.

---

## 💡 Portfolio Value

This project demonstrates:

- Real-time systems thinking
- MQTT and event-driven architectures
- Backend–frontend data bridging
- Containerized local infrastructure
- Industrial KPI (OEE) awareness
- Practical IIoT design patterns

It complements traditional web projects by showcasing **industrial software engineering experience applied to modern web technologies**.

---

## 👨‍💻 Author

**Ricardo Rocheta**
