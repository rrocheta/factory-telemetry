namespace FactoryTelemetry.Simulator;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1883;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? ClientId { get; init; }
    public bool CleanSession { get; init; } = true;
    public MqttTlsOptions Tls { get; init; } = new();
}

public sealed class MqttTlsOptions
{
    public bool Enabled { get; init; }
    public bool AllowUntrustedCertificates { get; init; }
    public bool IgnoreCertificateChainErrors { get; init; }
    public bool IgnoreCertificateRevocationErrors { get; init; }
    public string? CaCertificatePath { get; init; }
    public string? ClientCertificatePath { get; init; }
    public string? ClientCertificatePassword { get; init; }
}
