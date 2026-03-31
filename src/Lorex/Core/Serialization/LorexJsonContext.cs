using System.Text.Json.Serialization;
using Lorex.Core.Models;

namespace Lorex.Core.Serialization;

[JsonSerializable(typeof(LorexConfig))]
[JsonSerializable(typeof(ArtifactCollection))]
[JsonSerializable(typeof(GlobalConfig))]
[JsonSerializable(typeof(RegistryConfig))]
[JsonSerializable(typeof(RegistryPolicy))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public sealed partial class LorexJsonContext : JsonSerializerContext { }
