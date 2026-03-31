namespace Lorex.Core.Models;

/// <summary>
/// Well-known registry publish mode values.
/// </summary>
public static class RegistryPublishModes
{
    public const string Direct = "direct";
    public const string PullRequest = "pull-request";
    public const string ReadOnly = "read-only";

    public static bool IsValid(string value) =>
        string.Equals(value, Direct, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, PullRequest, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, ReadOnly, StringComparison.OrdinalIgnoreCase);
}
