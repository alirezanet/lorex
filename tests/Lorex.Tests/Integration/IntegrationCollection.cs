namespace Lorex.Tests.Integration;

/// <summary>
/// Integration tests share a static AnsiConsole and git subprocess pool.
/// Running them in parallel within the same process causes non-deterministic failures.
/// This collection definition serializes all integration tests.
/// </summary>
[CollectionDefinition("Integration", DisableParallelization = true)]
public sealed class IntegrationCollection { }
