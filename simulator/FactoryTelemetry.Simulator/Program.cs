using FactoryTelemetry.Simulator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

internal class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger);

            var machineId = Environment.GetEnvironmentVariable("SIM_MACHINE_ID") ?? "cnc1";
            var seedEnv = Environment.GetEnvironmentVariable("SIM_SEED");
            var seed = int.TryParse(seedEnv, out var parsedSeed)
                ? parsedSeed
                : StableHash(machineId);

            var basePath = builder.Environment.ContentRootPath;
            var simConfig = SimulatorConfig.Load(basePath, machineId, seed);

            builder.Services.AddSingleton(simConfig);
            builder.Services.Configure<MqttOptions>(
                builder.Configuration.GetSection(MqttOptions.SectionName));
            builder.Services.AddSingleton<IPublisher, MqttPublisher>();
            builder.Services.AddSingleton<SimulationEngine>();
            builder.Services.AddHostedService<SimulatorService>();

            var host = builder.Build();
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Simulator terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Deterministic seed from machineId for reproducible simulations per machine.
    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619u;
            }

            return (int)hash;
        }
    }
}
