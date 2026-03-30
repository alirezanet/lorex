using System.Diagnostics;
using System.Text;

namespace Lorex.Core.Services;

/// <summary>Thrown when a git subprocess exits with a non-zero code.</summary>
public sealed class GitException(string message) : Exception(message);

/// <summary>
/// Thin wrapper around the system <c>git</c> executable.
/// All operations shell out to git, reusing the user's existing credentials and SSH agent.
/// </summary>
public sealed class GitService
{
    /// <summary>Runs a git command in the given working directory. Throws GitException on failure.</summary>
    public string Run(string workingDirectory, params string[] arguments)
    {
        var args = string.Join(' ', arguments.Select(EscapeArg));
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new GitException("Failed to start git process.");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = stderr.ToString().Trim();
            var hint = IsAuthError(error)
                ? "\nHint: configure git credentials — SSH key, HTTPS token, or run `gh auth login`."
                : string.Empty;
            throw new GitException($"git {args} failed (exit {process.ExitCode}):\n{error}{hint}");
        }

        return stdout.ToString();
    }

    /// <summary>
    /// Checks whether a remote URL is a reachable git repository.
    /// Returns null on success, or an error message on failure.
    /// </summary>
    public string? ProbeRemote(string url)
    {
        // Basic format check before hitting the network
        if (!IsPlausibleGitUrl(url))
            return $"'{url}' does not look like a git URL. Use HTTPS (https://...) or SSH (git@host:org/repo.git).";

        try
        {
            // ls-remote with no extra flags: exits 0 if the remote is reachable (even empty repo),
            // non-zero on auth failure, bad URL, or network error.
            Run(Path.GetTempPath(), "ls-remote", "--", url);
            return null;
        }
        catch (GitException ex)
        {
            return ex.Message;
        }
    }

    public void CloneShallow(string url, string destination) =>
        Run(Path.GetDirectoryName(destination)!, "clone", "--depth", "1", "--", url, destination);

    public void Pull(string repoPath) =>
        Run(repoPath, "pull", "--ff-only");

    public void AddAll(string repoPath) =>
        Run(repoPath, "add", "-A");

    public void Commit(string repoPath, string message) =>
        Run(repoPath, "commit", "-m", message);

    public void Push(string repoPath) =>
        Run(repoPath, "push");

    // ── helpers ──────────────────────────────────────────────────────────────

    private static bool IsPlausibleGitUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        // HTTPS
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return true;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return true;
        // SSH: git@github.com:org/repo.git  or  ssh://git@...
        if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase)) return true;
        if (url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsAuthError(string stderr) =>
        stderr.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("could not read Username", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("access denied", StringComparison.OrdinalIgnoreCase);

    private static string EscapeArg(string arg)
    {
        // Wrap in double-quotes if the arg contains spaces or special chars;
        // escape any existing double-quotes inside.
        if (arg.AsSpan().IndexOfAny(" \t\"\\") < 0)
            return arg;

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
