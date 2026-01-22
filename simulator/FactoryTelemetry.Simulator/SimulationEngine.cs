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
    private readonly Random _rng = new();

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

    public SimulationEngine(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task RunAsync(CancellationToken ct)
    {

        var tickLoop = RunTickLoopAsync(periodMs: 100, ct);    // 10 Hz "scan cycle"
        var axisLoop = RunAxisPublishLoopAsync(periodMs: 250, ct);
        var spindleLoop = RunSpindlePublishLoopAsync(periodMs: 500, ct);
        var tempLoop = RunTempPublishLoopAsync(periodMs: 2000, ct);
        var stateLoop = RunStatePublishLoopAsync(periodMs: 1000, ct);

        return Task.WhenAll(tickLoop, axisLoop, spindleLoop, tempLoop, stateLoop);
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

            await PublishMetricAsync("factory/cnc1/axis/x/pos", s.tsUtc, s.xMm, "mm", ct);
            await PublishMetricAsync("factory/cnc1/axis/y/pos", s.tsUtc, s.yMm, "mm", ct);
            await PublishMetricAsync("factory/cnc1/axis/z/pos", s.tsUtc, s.zMm, "mm", ct);
        }
    }

    private async Task RunSpindlePublishLoopAsync(int periodMs, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(periodMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            var s = _snapshot;

            await PublishMetricAsync("factory/cnc1/spindle/rpm", s.tsUtc, s.rpm, "rpm", ct);
            await PublishMetricAsync("factory/cnc1/spindle/power", s.tsUtc, s.powerKw, "kW", ct);
            await PublishMetricAsync("factory/cnc1/spindle/vibration", s.tsUtc, s.vibrationMms, "mm/s", ct);
        }
    }

    private async Task RunTempPublishLoopAsync(int periodMs, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(periodMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            var s = _snapshot;

            await PublishMetricAsync("factory/cnc1/spindle/temp", s.tsUtc, s.spindleTempC, "C", ct);
            await PublishMetricAsync("factory/cnc1/motor/temp", s.tsUtc, s.motorTempC, "C", ct);
        }
    }

    private async Task RunStatePublishLoopAsync(int periodMs, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(periodMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            var s = _snapshot;

            // heartbeat
            await _publisher.PublishJsonAsync("factory/cnc1/heartbeat", new { ts = s.tsUtc, ok = true }, ct);

            // state
            await _publisher.PublishJsonAsync("factory/cnc1/state", new { ts = s.tsUtc, value = s.state.ToString() }, ct);

            // alarms (exemplo)
            await _publisher.PublishJsonAsync("factory/cnc1/alarms", new
            {
                ts = s.tsUtc,
                value = s.state == MachineState.Alarm ? new[] { "SIM_ALARM_VIBRATION" } : Array.Empty<string>()
            }, ct);
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
}
