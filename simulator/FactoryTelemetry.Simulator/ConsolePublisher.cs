using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FactoryTelemetry.Simulator
{
    public sealed class ConsolePublisher : IPublisher
    {
        private readonly ILogger<ConsolePublisher> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public ConsolePublisher(ILogger<ConsolePublisher> logger)
        {
            _logger = logger;
        }

        public Task PublishJsonAsync(string topic, object payload, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            _logger.LogInformation("[PUB] {Topic} => {Json}", topic, json);
            return Task.CompletedTask;
        }
    }
}
