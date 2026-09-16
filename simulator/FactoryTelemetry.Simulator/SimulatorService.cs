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

            try
            {
                await _engine.RunAsync(stoppingToken);
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
    }
}
