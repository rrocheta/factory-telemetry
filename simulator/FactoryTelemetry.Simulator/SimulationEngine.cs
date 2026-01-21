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

    // “Process variables”
    private MachineState _state = MachineState.Idle;

    private double _x, _y, _z;      // axis positions
    private double _rpm = 0;        // spindle speed
    private double _power = 0;      // kW
    private double _vibration = 0;  // mm/s ou “g” (simulado)
    private double _tempSpindle = 35;
    private double _tempMotor = 33;

    //For states/transitions
    private DateTime _nextStateChangeUtc = DateTime.UtcNow.AddSeconds(10);

    public SimulationEngine(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public int NextTemperatureDelayMs()
    {
        // 2–5s with jitter
        return _rng.Next(2000, 5001);
    }

    public async Task PublishAxisAsync(CancellationToken ct)
    {
        TickStateMachine();

        var t = (DateTime.UtcNow - _startUtc).TotalSeconds;

        // If it's not running, the axles are almost stationary (slight noise).
        if (_state != MachineState.Running)
        {
            _x = AddNoise(_x, 0.01);
            _y = AddNoise(_y, 0.01);
            _z = AddNoise(_z, 0.01);
        }
        else
        {
            // Smooth movement (sine) + noise
            _x = 120 + 20 * Math.Sin(t * 0.8) + Noise(0.10);
            _y = 80 + 15 * Math.Sin(t * 0.6 + 1.2) + Noise(0.10);
            _z = 30 + 5 * Math.Sin(t * 1.1 + 2.1) + Noise(0.05);
        }

        var payload = new
        {
            ts = DateTime.UtcNow,
            state = _state.ToString(),
            axis = new
            {
                x = new { value = Round2(_x), unit = "mm" },
                y = new { value = Round2(_y), unit = "mm" },
                z = new { value = Round2(_z), unit = "mm" }
            }
        };

        await _publisher.PublishJsonAsync("factory/cnc1/axis/pos", payload, ct);
    }

    public async Task PublishVibrationAndPowerAsync(CancellationToken ct)
    {
        TickStateMachine();

        if (_state == MachineState.Running)
        {
            // RPM varies (example)
            _rpm = 4000 + 1500 * Math.Sin((DateTime.UtcNow - _startUtc).TotalSeconds * 0.2) + Noise(50);

            // Power correlates with RPM and "load".
            var load = 0.55 + 0.25 * Math.Sin((DateTime.UtcNow - _startUtc).TotalSeconds * 0.7) + Noise(0.03);
            load = Clamp(load, 0.1, 1.0);
            _power = 2.0 + (load * (_rpm / 6000.0)) * 6.0 + Noise(0.05);

            // Vibration increases with RPM and load.
            _vibration = 0.4 + (load * (_rpm / 6000.0)) * 2.2 + Noise(0.05);
        }
        else
        {
            _rpm = Approach(_rpm, 0, 350);
            _power = Approach(_power, 0.2, 0.2);
            _vibration = Approach(_vibration, 0.2, 0.2);
        }

        // Alarm → peaks
        if (_state == MachineState.Alarm)
        {
            _vibration += 1.5 + Noise(0.2);
            _power += 0.8 + Noise(0.1);
        }

        var payload = new
        {
            ts = DateTime.UtcNow,
            state = _state.ToString(),

            spindle = new
            {
                rpm = new { value = (int)Math.Round(_rpm), unit = "rpm" }
            },

            power = new
            {
                value = Round2(_power),
                unit = "kW"
            },

            vibration = new
            {
                value = Round2(_vibration),
                unit = "mm/s"
            }
        };

        await _publisher.PublishJsonAsync("factory/cnc1/power", payload, ct);
        await _publisher.PublishJsonAsync("factory/cnc1/vibration", payload, ct);
    }

    public async Task PublishTemperaturesAsync(CancellationToken ct)
    {
        TickStateMachine();

        // Temperatures rise slowly when running.
        if (_state == MachineState.Running)
        {
            _tempSpindle = Approach(_tempSpindle, 55 + Noise(1.0), 0.35);
            _tempMotor = Approach(_tempMotor, 50 + Noise(1.0), 0.30);
        }
        else
        {
            // Cool
            _tempSpindle = Approach(_tempSpindle, 35, 0.20);
            _tempMotor = Approach(_tempMotor, 33, 0.18);
        }

        // Alarm → startle
        if (_state == MachineState.Alarm)
        {
            _tempSpindle += 0.7 + Noise(0.2);
            _tempMotor += 0.4 + Noise(0.2);
        }

        var payload = new
        {
            ts = DateTime.UtcNow,
            state = _state.ToString(),
            temp = new
            {
                spindle = new { value = Round2(_tempSpindle), unit = "C" },
                motor = new { value = Round2(_tempMotor), unit = "C" }
            }
        };

        await _publisher.PublishJsonAsync("factory/cnc1/temp", payload, ct);
    }

    public async Task PublishStateAndHeartbeatAsync(CancellationToken ct)
    {
        TickStateMachine();

        // Simple Heartbeat
        var hb = new { ts = DateTime.UtcNow, ok = true };

        var statePayload = new
        {
            ts = DateTime.UtcNow,
            state = _state.ToString(),
            alarms = _state == MachineState.Alarm ? new[] { "SIM_ALARM_VIBRATION" } : Array.Empty<string>()
        };

        await _publisher.PublishJsonAsync("factory/cnc1/heartbeat", hb, ct);
        await _publisher.PublishJsonAsync("factory/cnc1/state", statePayload, ct);
    }

    private void TickStateMachine()
    {
        var now = DateTime.UtcNow;
        if (now < _nextStateChangeUtc) return;

        // State change (simple and realistic)
        // Ex: Idle -> Running -> (sometimes Alarm) -> Running/Idle
        _state = _state switch
        {
            MachineState.Idle => MachineState.Running,
            MachineState.Running => _rng.NextDouble() < 0.10 ? MachineState.Alarm : MachineState.Idle,
            MachineState.Alarm => MachineState.Running,
            _ => MachineState.Idle
        };

        // Next change in 8–25s
        _nextStateChangeUtc = now.AddSeconds(_rng.Next(8, 26));
    }

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
}
