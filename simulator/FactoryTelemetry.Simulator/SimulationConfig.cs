using System.Text.Json;
using System.Text.Json.Serialization;

namespace FactoryTelemetry.Simulator;

public sealed class SimulatorConfig
{
    public string MachineId { get; init; } = "cnc1";
    public int RandomSeed { get; init; }
    public ProgramCatalog Programs { get; init; } = new();
    public PartsCatalog Parts { get; init; } = new();

    public static SimulatorConfig Load(string basePath, string machineId, int randomSeed)
    {
        var programsPath = Path.Combine(basePath, "Config", "programs.json");
        var partsPath = Path.Combine(basePath, "Config", "parts.json");

        if (!File.Exists(programsPath))
        {
            throw new FileNotFoundException("Missing programs configuration", programsPath);
        }

        if (!File.Exists(partsPath))
        {
            throw new FileNotFoundException("Missing parts configuration", partsPath);
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var programs = JsonSerializer.Deserialize<ProgramCatalog>(File.ReadAllText(programsPath), jsonOptions)
            ?? new ProgramCatalog();
        var parts = JsonSerializer.Deserialize<PartsCatalog>(File.ReadAllText(partsPath), jsonOptions)
            ?? new PartsCatalog();

        ValidateOrThrow(programs, parts);

        return new SimulatorConfig
        {
            MachineId = machineId,
            RandomSeed = randomSeed,
            Programs = programs,
            Parts = parts
        };
    }

    private static void ValidateOrThrow(ProgramCatalog programs, PartsCatalog parts)
    {
        var errors = new List<string>();

        if (programs.Programs.Count == 0)
        {
            errors.Add("programs.json: programs array must not be empty.");
        }

        var programIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var program in programs.Programs)
        {
            if (string.IsNullOrWhiteSpace(program.Id))
            {
                errors.Add("programs.json: program id is required.");
                continue;
            }

            if (!programIds.Add(program.Id))
            {
                errors.Add($"programs.json: duplicate program id '{program.Id}'.");
            }

            if (program.IdealCycleMs <= 0)
            {
                errors.Add($"programs.json: program '{program.Id}' idealCycleMs must be > 0.");
            }

            if (program.BaseRejectRate is < 0 or > 1)
            {
                errors.Add($"programs.json: program '{program.Id}' baseRejectRate must be between 0 and 1.");
            }
        }

        if (parts.Parts.Count == 0)
        {
            errors.Add("parts.json: parts array must not be empty.");
        }

        var partIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts.Parts)
        {
            if (string.IsNullOrWhiteSpace(part.Id))
            {
                errors.Add("parts.json: part id is required.");
                continue;
            }

            if (!partIds.Add(part.Id))
            {
                errors.Add($"parts.json: duplicate part id '{part.Id}'.");
            }

            if (part.Routing.Count == 0)
            {
                errors.Add($"parts.json: part '{part.Id}' routing array must not be empty.");
                continue;
            }

            foreach (var step in part.Routing)
            {
                if (string.IsNullOrWhiteSpace(step.OperationId))
                {
                    errors.Add($"parts.json: part '{part.Id}' routing step operation_id is required.");
                }

                if (string.IsNullOrWhiteSpace(step.Program))
                {
                    errors.Add($"parts.json: part '{part.Id}' routing step program is required.");
                    continue;
                }

                if (!programIds.Contains(step.Program))
                {
                    errors.Add($"parts.json: part '{part.Id}' routing program '{step.Program}' not found in programs.json.");
                }
            }
        }

        if (errors.Count > 0)
        {
            var message = "Invalid simulator configuration:\n- " + string.Join("\n- ", errors);
            throw new InvalidOperationException(message);
        }
    }
}

public sealed class ProgramCatalog
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; } = 1;
    public List<ProgramDefinition> Programs { get; init; } = new();
}

public sealed class ProgramDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    [JsonPropertyName("ideal_cycle_ms")]
    public int IdealCycleMs { get; init; }
    [JsonPropertyName("base_reject_rate")]
    public double BaseRejectRate { get; init; }
}

public sealed class PartsCatalog
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; } = 1;
    public List<PartDefinition> Parts { get; init; } = new();
}

public sealed class PartDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public List<RoutingStep> Routing { get; init; } = new();
}

public sealed class RoutingStep
{
    [JsonPropertyName("operation_id")]
    public string OperationId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Program { get; init; } = string.Empty;
}
