using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FactoryTelemetry.Simulator
{
    public class SimulatorService : BackgroundService
    {

        private readonly ILogger<SimulatorService> _logger;
        private readonly SimulationEngine _engine;

        public SimulatorService(ILogger<SimulatorService> logger, SimulationEngine engine)
        {
            _logger = logger;
            _engine = engine;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Simulator started");

            var tasks = new List<Task>
            {
                RunFastSignalsLoop(stoppingToken), // 1 s
                RunTemperatureLoop(stoppingToken)  // 2–5 s
            };

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
            finally
            {
                _logger.LogInformation("Simulator stopping");
            }

        }

        private async Task RunFastSignalsLoop(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(ct))
            {
                await _engine.PublishAxisAsync(ct);
                await _engine.PublishVibrationAndPowerAsync(ct);
                await _engine.PublishStateAndHeartbeatAsync(ct);
            }
        }

        private async Task RunTemperatureLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await _engine.PublishTemperaturesAsync(ct);

                var delayMs = _engine.NextTemperatureDelayMs();
                await Task.Delay(delayMs, ct);
            }
        }
    }
}
