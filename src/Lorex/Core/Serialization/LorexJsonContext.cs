using System.Text.Json.Serialization;
using Lorex.Core.Models;

namespace Lorex.Core.Serialization;

[JsonSerializable(typeof(LorexConfig))]
[JsonSerializable(typeof(GlobalConfig))]
[JsonSerializable(typeof(RegistryConfig))]
[JsonSerializable(typeof(RegistryPolicy))]
[JsonSerializable(typeof(TapConfig))]
[JsonSerializable(typeof(TapConfig[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(SkillMetadata[]))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public sealed partial class LorexJsonContext : JsonSerializerContext { }
