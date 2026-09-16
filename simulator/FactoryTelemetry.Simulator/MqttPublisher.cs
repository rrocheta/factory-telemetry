using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace FactoryTelemetry.Simulator;

public sealed class MqttPublisher : IPublisher, IHostedService, IAsyncDisposable
{
    private readonly ILogger<MqttPublisher> _logger;

    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;

    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public MqttPublisher(ILogger<MqttPublisher> logger)
        : this(logger, Options.Create(new MqttOptions()))
    {
    }

    public MqttPublisher(ILogger<MqttPublisher> logger, IOptions<MqttOptions> mqttOptions)
    {
        _logger = logger;
        var settings = mqttOptions.Value;

        if (settings.Tls.Enabled)
        {
            throw new InvalidOperationException(
                "MQTT TLS is configured but not yet implemented in MqttPublisher. " +
                "Set Mqtt:Tls:Enabled to false until TLS wiring is added.");
        }

        // v5 sample-style
        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();

        var clientId = string.IsNullOrWhiteSpace(settings.ClientId)
            ? $"cnc-simulator-{Environment.MachineName}"
            : settings.ClientId;

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(settings.Host, settings.Port)
            .WithClientId(clientId)
            .WithCleanSession(settings.CleanSession);

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            optionsBuilder = optionsBuilder.WithCredentials(settings.Username, settings.Password);
        }

        _options = optionsBuilder.Build();

        _client.ConnectedAsync += e =>
        {
            _logger.LogInformation("MQTT connected");
            return Task.CompletedTask;
        };

        _client.DisconnectedAsync += e =>
        {
            _logger.LogWarning("MQTT disconnected: {Reason}", e.Reason);
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MQTT publisher starting");

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = RunConnectionLoopAsync(_loopCts.Token);

        _logger.LogInformation("MQTT connection loop started; broker connection will be retried in background.");
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MQTT publisher stopping");

        _loopCts?.Cancel();

        if (_loopTask is not null)
        {
            try { await _loopTask; } catch { /* ignore */ }
        }

        if (_client.IsConnected)
        {
            try
            {
                await _client.DisconnectAsync();
                
            }
            catch { /* ignore */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _connectLock.Dispose();
        _loopCts?.Dispose();
        _client.Dispose();
        await ValueTask.CompletedTask;
    }

    public async Task PublishJsonAsync(string topic, object payload, CancellationToken ct)
    {
        if (!_client.IsConnected)
        {
            _logger.LogDebug("Skip publish (not connected): {Topic}", topic);
            return;
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);

        var qos = (topic.EndsWith("/state") || topic.EndsWith("/heartbeat"))
            ? MqttQualityOfServiceLevel.AtLeastOnce
            : MqttQualityOfServiceLevel.AtMostOnce;

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(json)
            .WithQualityOfServiceLevel(qos)
            .Build();

        try
        {
            await _client.PublishAsync(msg, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish MQTT message to topic {Topic}", topic);
        }
    }

    private async Task RunConnectionLoopAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);

        while (!ct.IsCancellationRequested)
        {
            if (!_client.IsConnected)
            {
                try
                {
                    await EnsureConnectedAsync(ct);
                    delay = TimeSpan.FromSeconds(1);
                }
                catch
                {
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                }
            }

            try { await Task.Delay(delay, ct); }
            catch (OperationCanceledException) { }
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_client.IsConnected) return;

        await _connectLock.WaitAsync(ct);
        try
        {
            if (_client.IsConnected) return;

            _logger.LogInformation("MQTT connecting...");
            await _client.ConnectAsync(_options, ct);
        }
        finally
        {
            _connectLock.Release();
        }
    }
}
