using System.Collections.Concurrent;

namespace FactoryTelemetry.Simulator;

public enum MachineState
{
    Idle,
    Running,
    Alarm
}

public sealed class SimulationEngine
{
    private readonly IPublisher _publisher;
    private readonly Random _rng;
    private readonly string _machineId;
    private readonly Dictionary<string, ProgramDefinition> _programsById;
    private readonly List<PartDefinition> _parts;
    private readonly ConcurrentQueue<OperationCompletedEvent> _operationEvents = new();
    private readonly ConcurrentQueue<PartCompletedEvent> _partEvents = new();

    private DateTime _startUtc = DateTime.UtcNow;

    // "Plant state"
    private MachineState _state = MachineState.Idle;

    private double _x, _y, _z;      // mm
    private double _rpm;            // rpm
    private double _power;          // kW
    private double _vibration;      // mm/s
    private double _tempSpindle;    // C
    private double _tempMotor;      // C

    private DateTime _nextStateChangeUtc = DateTime.UtcNow.AddSeconds(10);

    private volatile Snapshot _snapshot = Snapshot.Empty();

    private const int TickPeriodMs = 100;

    // Production state
    private int _currentPartIndex;
    private int _currentOperationIndex;
    private int _cycleRemainingMs;
    private int _cycleActualMs;
    private int _cycleIdealMs;
    private string _currentProgramId = string.Empty;
    private string _currentPartId = string.Empty;
    private string _currentOperationId = string.Empty;
    private long _goodCount;
    private long _badCount;
    private string _lastPublishedStateValue = string.Empty;

    public SimulationEngine(IPublisher publisher, SimulatorConfig config)
    {
        _publisher = publisher;
        _machineId = config.MachineId;
        _rng = new Random(config.RandomSeed);
        _programsById = config.Programs.Programs.ToDictionary(p => p.Id, p => p);
        _parts = config.Parts.Parts;
    }

    public Task RunAsync(CancellationToken ct)
    {

        var tickLoop = RunTickLoopAsync(periodMs: TickPeriodMs, ct);    // 10 Hz "scan cycle"
        var axisLoop = RunAxisPublishLoopAsync(periodMs: 250, ct);
        var spindleLoop = RunSpindlePublishLoopAsync(periodMs: 500, ct);
        var tempLoop = RunTempPublishLoopAsync(periodMs: 2000, ct);
        var stateLoop = RunStatePublishLoopAsync(periodMs: 1000, ct);
        var productionLoop = RunProductionPublishLoopAsync(periodMs: 200, ct);

        return Task.WhenAll(tickLoop, axisLoop, spindleLoop, tempLoop, stateLoop, productionLoop);
    }

    // TICK LOOP
    private async Task RunTickLoopAsync(int periodMs, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(periodMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            Tick();
        }
    }

    private void Tick()
    {
        TickStateMachine();
        TickProduction();

        UpdateAxis();
        UpdateSpindleTelemetry();
        UpdateTemperatures();

        _snapshot = new Snapshot(
            tsUtc: DateTime.UtcNow,
            state: _state,
            xMm: Round2(_x),
            yMm: Round2(_y),
            zMm: Round2(_z),
            rpm: (int)Math.Round(_rpm),
            powerKw: Round2(_power),
            vibrationMms: Round2(_vibration),
            spindleTempC: Round2(_tempSpindle),
            motorTempC: Round2(_tempMotor)
        );
    }

    // PUBLISH LOOPS
    private async Task RunAxisPublishLoopAsync(int periodMs, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(periodMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            var s = _snapshot;

            await PublishMetricAsync($"factory/{_machineId}/axis/x/pos", s.tsUtc, s.xMm, "mm", ct);
            await PublishMetricAsync($"factory/{_machineId}/axis/y/pos", s.tsUtc, s.yMm, "mm", ct);
            await PublishMetricAsync($"factory/{_machineId}/axis/z/pos", s.tsUtc, s.zMm, "mm", ct);
        }
    }

    private async Task RunSpindlePublishLoopAsync(int periodMs, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(periodMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            var s = _snapshot;

            await PublishMetricAsync($"factory/{_machineId}/spindle/rpm", s.tsUtc, s.rpm, "rpm", ct);
            await PublishMetricAsync($"factory/{_machineId}/spindle/power", s.tsUtc, s.powerKw, "kW", ct);
            await PublishMetricAsync($"factory/{_machineId}/spindle/vibration", s.tsUtc, s.vibrationMms, "mm/s", ct);
        }
    }

    private async Task RunTempPublishLoopAsync(int periodMs, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(periodMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            var s = _snapshot;

            await PublishMetricAsync($"factory/{_machineId}/spindle/temp", s.tsUtc, s.spindleTempC, "C", ct);
            await PublishMetricAsync($"factory/{_machineId}/motor/temp", s.tsUtc, s.motorTempC, "C", ct);
        }
    }

    private async Task RunStatePublishLoopAsync(int periodMs, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(periodMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            var s = _snapshot;

            // heartbeat
            await _publisher.PublishJsonAsync($"factory/{_machineId}/heartbeat", new { ts = s.tsUtc, machineId = _machineId, ok = true }, ct);

            // state change event
            var stateValue = MapStateValue(s.state);
            if (!string.Equals(stateValue, _lastPublishedStateValue, StringComparison.Ordinal))
            {
                _lastPublishedStateValue = stateValue;
                await _publisher.PublishJsonAsync($"factory/{_machineId}/state", new
                {
                    ts = s.tsUtc,
                    machineId = _machineId,
                    state = stateValue
                }, ct);
            }

            // alarms (exemplo)
            await _publisher.PublishJsonAsync($"factory/{_machineId}/alarms", new
            {
                ts = s.tsUtc,
                value = s.state == MachineState.Alarm ? new[] { "SIM_ALARM_VIBRATION" } : Array.Empty<string>()
            }, ct);

            var total = _goodCount + _badCount;
            await _publisher.PublishJsonAsync($"factory/{_machineId}/production/goodCount", new { ts = s.tsUtc, machineId = _machineId, value = _goodCount }, ct);
            await _publisher.PublishJsonAsync($"factory/{_machineId}/production/badCount", new { ts = s.tsUtc, machineId = _machineId, value = _badCount }, ct);
            await _publisher.PublishJsonAsync($"factory/{_machineId}/production/totalCount", new { ts = s.tsUtc, machineId = _machineId, value = total }, ct);
        }
    }

    private async Task RunProductionPublishLoopAsync(int periodMs, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(periodMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            while (_operationEvents.TryDequeue(out var evt))
            {
                await _publisher.PublishJsonAsync($"factory/{_machineId}/production/operationCompleted", new
                {
                    ts = evt.TimestampUtc,
                    machineId = _machineId,
                    partId = evt.PartId,
                    operation = evt.OperationId,
                    programId = evt.ProgramId,
                    idealCycleMs = evt.IdealCycleMs,
                    actualCycleMs = evt.ActualCycleMs,
                    result = evt.IsGood ? "OK" : "NOK"
                }, ct);
            }

            while (_partEvents.TryDequeue(out var partEvt))
            {
                await _publisher.PublishJsonAsync($"factory/{_machineId}/production/partCompleted", new
                {
                    ts = partEvt.TimestampUtc,
                    machineId = _machineId,
                    partId = partEvt.PartId,
                    result = partEvt.IsGood ? "OK" : "NOK"
                }, ct);
            }
        }
    }


    // Update funcs
    private void UpdateAxis()
    {
        var t = (DateTime.UtcNow - _startUtc).TotalSeconds;

        if (_state != MachineState.Running)
        {
            _x = AddNoise(_x, 0.01);
            _y = AddNoise(_y, 0.01);
            _z = AddNoise(_z, 0.01);
        }
        else
        {
            _x = 120 + 20 * Math.Sin(t * 0.8) + Noise(0.10);
            _y = 80 + 15 * Math.Sin(t * 0.6 + 1.2) + Noise(0.10);
            _z = 30 + 5 * Math.Sin(t * 1.1 + 2.1) + Noise(0.05);
        }
    }

    private void UpdateSpindleTelemetry()
    {
        if (_state == MachineState.Running)
        {
            _rpm = 4000 + 1500 * Math.Sin((DateTime.UtcNow - _startUtc).TotalSeconds * 0.2) + Noise(50);

            var load = 0.55 + 0.25 * Math.Sin((DateTime.UtcNow - _startUtc).TotalSeconds * 0.7) + Noise(0.03);
            load = Clamp(load, 0.1, 1.0);

            _power = 2.0 + (load * (_rpm / 6000.0)) * 6.0 + Noise(0.05);
            _vibration = 0.4 + (load * (_rpm / 6000.0)) * 2.2 + Noise(0.05);
        }
        else
        {
            _rpm = Approach(_rpm, 0, 350);
            _power = Approach(_power, 0.2, 0.2);
            _vibration = Approach(_vibration, 0.2, 0.2);
        }

        if (_state == MachineState.Alarm)
        {
            _vibration += 1.5 + Noise(0.2);
            _power += 0.8 + Noise(0.1);
        }
    }

    private void UpdateTemperatures()
    {
        if (_state == MachineState.Running)
        {
            _tempSpindle = Approach(_tempSpindle, 55 + Noise(1.0), 0.35);
            _tempMotor = Approach(_tempMotor, 50 + Noise(1.0), 0.30);
        }
        else
        {
            _tempSpindle = Approach(_tempSpindle, 35, 0.20);
            _tempMotor = Approach(_tempMotor, 33, 0.18);
        }

        if (_state == MachineState.Alarm)
        {
            _tempSpindle += 0.7 + Noise(0.2);
            _tempMotor += 0.4 + Noise(0.2);
        }
    }

    private void TickStateMachine()
    {
        var now = DateTime.UtcNow;
        if (now < _nextStateChangeUtc) return;

        _state = _state switch
        {
            MachineState.Idle => MachineState.Running,
            MachineState.Running => _rng.NextDouble() < 0.10 ? MachineState.Alarm : MachineState.Idle,
            MachineState.Alarm => MachineState.Running,
            _ => MachineState.Idle
        };

        _nextStateChangeUtc = now.AddSeconds(_rng.Next(8, 26));
    }

    private void TickProduction()
    {
        if (_parts.Count == 0 || _programsById.Count == 0)
        {
            return;
        }

        if (_currentPartId.Length == 0)
        {
            SelectNextOperation();
        }

        if (_state != MachineState.Running)
        {
            return;
        }

        if (_cycleRemainingMs <= 0)
        {
            if (!StartCycle())
            {
                return;
            }
        }

        _cycleRemainingMs -= TickPeriodMs;
        if (_cycleRemainingMs > 0) return;

        var isGood = !IsRejected(_currentProgramId);
        if (isGood)
        {
            _goodCount++;
        }
        else
        {
            _badCount++;
        }

        _operationEvents.Enqueue(new OperationCompletedEvent(
            TimestampUtc: DateTime.UtcNow,
            PartId: _currentPartId,
            OperationId: _currentOperationId,
            ProgramId: _currentProgramId,
            IsGood: isGood,
            IdealCycleMs: _cycleIdealMs,
            ActualCycleMs: _cycleActualMs));

        var part = _parts[_currentPartIndex];
        var isLastOperation = _currentOperationIndex >= part.Routing.Count - 1;

        if (!isGood)
        {
            _partEvents.Enqueue(new PartCompletedEvent(DateTime.UtcNow, _currentPartId, false));
            MoveToNextPart();
            return;
        }

        if (isLastOperation)
        {
            _partEvents.Enqueue(new PartCompletedEvent(DateTime.UtcNow, _currentPartId, true));
            MoveToNextPart();
            return;
        }

        AdvanceRouting();
    }

    private bool StartCycle()
    {
        if (!_programsById.TryGetValue(_currentProgramId, out var program))
        {
            _cycleIdealMs = 0;
            _cycleActualMs = 0;
            _cycleRemainingMs = 0;
            return false;
        }

        _cycleIdealMs = Math.Max(program.IdealCycleMs, 1);
        var jitter = 1.0 + Noise(0.08);
        var actual = (int)Math.Round(_cycleIdealMs * jitter);
        var min = (int)Math.Round(_cycleIdealMs * 0.6);
        var max = (int)Math.Round(_cycleIdealMs * 1.4);
        _cycleActualMs = Clamp(actual, min, max);
        _cycleRemainingMs = _cycleActualMs;
        return true;
    }

    private void SelectNextOperation()
    {
        _currentPartIndex %= _parts.Count;
        var part = _parts[_currentPartIndex];
        if (part.Routing.Count == 0)
        {
            _currentPartId = part.Id;
            _currentOperationId = string.Empty;
            _currentProgramId = string.Empty;
            _cycleRemainingMs = 0;
            _cycleIdealMs = 0;
            _cycleActualMs = 0;
            return;
        }

        _currentOperationIndex %= part.Routing.Count;
        var step = part.Routing[_currentOperationIndex];

        _currentPartId = part.Id;
        _currentOperationId = step.OperationId;
        _currentProgramId = step.Program;
        _cycleRemainingMs = 0;
        _cycleIdealMs = 0;
        _cycleActualMs = 0;
    }

    private void AdvanceRouting()
    {
        var part = _parts[_currentPartIndex];
        _currentOperationIndex++;
        if (_currentOperationIndex >= part.Routing.Count)
        {
            _currentOperationIndex = 0;
            _currentPartIndex = (_currentPartIndex + 1) % _parts.Count;
        }

        SelectNextOperation();
    }

    private void MoveToNextPart()
    {
        _currentOperationIndex = 0;
        _currentPartIndex = (_currentPartIndex + 1) % _parts.Count;
        SelectNextOperation();
    }

    private static string MapStateValue(MachineState state)
    {
        return state switch
        {
            MachineState.Running => "RUN",
            MachineState.Idle => "IDLE",
            MachineState.Alarm => "DOWN",
            _ => "IDLE"
        };
    }

    private bool IsRejected(string programId)
    {
        if (!_programsById.TryGetValue(programId, out var program))
        {
            return false;
        }

        var rate = Clamp(program.BaseRejectRate, 0.0, 1.0);
        return _rng.NextDouble() < rate;
    }

    // Publish helper
    private Task PublishMetricAsync(string topic, DateTime tsUtc, double value, string unit, CancellationToken ct)
        => _publisher.PublishJsonAsync(topic, new { ts = tsUtc, value, unit }, ct);

    private Task PublishMetricAsync(string topic, DateTime tsUtc, int value, string unit, CancellationToken ct)
        => _publisher.PublishJsonAsync(topic, new { ts = tsUtc, value, unit }, ct);

    // Helpers
    private double Noise(double amplitude) => (2 * _rng.NextDouble() - 1) * amplitude;
    private double AddNoise(double v, double amp) => v + Noise(amp);

    private static double Approach(double current, double target, double maxDelta)
    {
        var delta = target - current;
        if (Math.Abs(delta) <= maxDelta) return target;
        return current + Math.Sign(delta) * maxDelta;
    }

    private static double Clamp(double v, double min, double max) => Math.Min(max, Math.Max(min, v));
    private static int Clamp(int v, int min, int max) => Math.Min(max, Math.Max(min, v));
    private static double Round2(double v) => Math.Round(v, 2);

    // Snapshot
    private sealed record Snapshot(
        DateTime tsUtc,
        MachineState state,
        double xMm,
        double yMm,
        double zMm,
        int rpm,
        double powerKw,
        double vibrationMms,
        double spindleTempC,
        double motorTempC)
    {
        public static Snapshot Empty() => new(
            tsUtc: DateTime.UtcNow,
            state: MachineState.Idle,
            xMm: 0, yMm: 0, zMm: 0,
            rpm: 0,
            powerKw: 0,
            vibrationMms: 0,
            spindleTempC: 35,
            motorTempC: 33
        );
    }

    private sealed record OperationCompletedEvent(
        DateTime TimestampUtc,
        string PartId,
        string OperationId,
        string ProgramId,
        bool IsGood,
        int IdealCycleMs,
        int ActualCycleMs);

    private sealed record PartCompletedEvent(
        DateTime TimestampUtc,
        string PartId,
        bool IsGood);
}
