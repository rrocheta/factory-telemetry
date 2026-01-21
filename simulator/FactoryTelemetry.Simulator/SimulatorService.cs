using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FactoryTelemetry.Simulator
{
    public class SimulatorService : BackgroundService
    {

        private readonly ILogger<SimulatorService> _logger;

        public SimulatorService(ILogger<SimulatorService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Simulator started");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Simulator is running...");
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                //shutdown
            }
            finally 
            {
                _logger.LogInformation("Simulator stopping");
            }
            
        }
    }
}
