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

    public void FetchPrune(string repoPath, string remote) =>
        Run(repoPath, "fetch", "--prune", remote);

    public void Fetch(string repoPath, string remote, string reference) =>
        Run(repoPath, "fetch", remote, reference);

    public void FetchBranchToRemoteTracking(string repoPath, string remote, string branchName) =>
        Run(repoPath, "fetch", remote, $"refs/heads/{branchName}:refs/remotes/{remote}/{branchName}");

    public void UpdateRemoteHead(string repoPath, string remote)
    {
        try
        {
            Run(repoPath, "remote", "set-head", remote, "-a");
        }
        catch (GitException)
        {
            // Best-effort only. Some remotes may not advertise HEAD cleanly.
        }
    }

    public string? GetRemoteDefaultBranch(string repoPath, string remote)
    {
        try
        {
            var output = Run(repoPath, "symbolic-ref", "--short", $"refs/remotes/{remote}/HEAD").Trim();
            var prefix = $"{remote}/";
            return output.StartsWith(prefix, StringComparison.Ordinal)
                ? output[prefix.Length..]
                : output;
        }
        catch (GitException)
        {
            return GetRemoteDefaultBranchViaSymref(repoPath, remote);
        }
    }

    public string? GetRemoteDefaultBranchFromUrl(string remoteUrl)
    {
        try
        {
            var output = Run(Path.GetTempPath(), "ls-remote", "--symref", "--", remoteUrl, "HEAD");
            return ParseDefaultBranchFromLsRemote(output);
        }
        catch (GitException)
        {
            return null;
        }
    }

    public IReadOnlyList<string> GetRemoteBranchNames(string repoPath, string remote)
    {
        try
        {
            var output = Run(repoPath, "for-each-ref", "--format=%(refname:short)", $"refs/remotes/{remote}");
            return ParseRemoteBranchNames(output, remote);
        }
        catch (GitException)
        {
            return [];
        }
    }

    public void AddAll(string repoPath) =>
        Run(repoPath, "add", "-A");

    public bool HasChanges(string repoPath) =>
        !string.IsNullOrWhiteSpace(Run(repoPath, "status", "--porcelain"));

    public void Commit(string repoPath, string message) =>
        Run(repoPath, "commit", "-m", message);

    public void Push(string repoPath) =>
        Run(repoPath, "push");

    public void PushSetUpstream(string repoPath, string remote, string branchName) =>
        Run(repoPath, "push", "-u", remote, branchName);

    public void CheckoutResetToRemoteBranch(string repoPath, string remote, string branchName) =>
        Run(repoPath, "checkout", "-B", branchName, $"{remote}/{branchName}");

    public void CheckoutOrphan(string repoPath, string branchName) =>
        Run(repoPath, "checkout", "--orphan", branchName);

    public bool HasCommits(string repoPath)
    {
        try
        {
            Run(repoPath, "rev-parse", "--verify", "HEAD");
            return true;
        }
        catch (GitException)
        {
            return false;
        }
    }

    public void WorktreeAdd(string repoPath, string worktreePath, string branchName, string startPoint)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(worktreePath)!);
        Run(repoPath, "worktree", "add", "-B", branchName, worktreePath, startPoint);
    }

    public void WorktreeRemove(string repoPath, string worktreePath) =>
        Run(repoPath, "worktree", "remove", "--force", worktreePath);

    // ── helpers ──────────────────────────────────────────────────────────────

    private string? GetRemoteDefaultBranchViaSymref(string repoPath, string remote)
    {
        try
        {
            var output = Run(repoPath, "ls-remote", "--symref", remote, "HEAD");
            return ParseDefaultBranchFromLsRemote(output);
        }
        catch (GitException)
        {
            return null;
        }
    }

    internal static string? ParseDefaultBranchFromLsRemote(string output)
    {
        using var reader = new StringReader(output);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            const string prefix = "ref: refs/heads/";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var tabIndex = line.IndexOf('\t');
            if (tabIndex <= prefix.Length)
                continue;

            var branchName = line[prefix.Length..tabIndex];
            if (!string.IsNullOrWhiteSpace(branchName))
                return branchName;
        }

        return null;
    }

    internal static IReadOnlyList<string> ParseRemoteBranchNames(string output, string remote)
    {
        var prefix = $"{remote}/";
        return [.. output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal) && !line.EndsWith("/HEAD", StringComparison.Ordinal))
            .Select(line => line[prefix.Length..])
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(line => line, StringComparer.Ordinal)];
    }

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
