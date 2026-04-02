using System.Diagnostics;
using Spectre.Console;

namespace Lorex.Core.Services;

/// <summary>
/// Detects whether Windows Developer Mode is enabled (required for unprivileged symlink creation)
/// and provides guidance to help the user enable it.
/// </summary>
internal static class WindowsDevModeHelper
{
    private static bool? _cached;

    /// <summary>
    /// Returns true when symlinks are available without elevation:
    ///   - Always true on non-Windows platforms
    ///   - True when Windows Developer Mode is enabled
    ///   - True when running as Administrator (elevation grants SeCreateSymbolicLinkPrivilege)
    /// </summary>
    public static bool IsSymlinkAvailable()
    {
        if (!OperatingSystem.IsWindows()) return true;
        if (_cached.HasValue) return _cached.Value;

        _cached = IsDevModeEnabled() || IsElevated();
        return _cached.Value;
    }

    /// <summary>
    /// Prints a formatted message explaining that Developer Mode is off,
    /// with step-by-step instructions to enable it.
    /// </summary>
    public static void PrintDevModeGuidance()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow bold]Symlinks require Windows Developer Mode[/]");
        AnsiConsole.MarkupLine("[dim]Without Developer Mode, lorex falls back to copying skill files.");
        AnsiConsole.MarkupLine("Copied skills do [bold]not[/] auto-update when you run [bold]lorex sync[/].[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]To enable Developer Mode:[/]");
        AnsiConsole.MarkupLine("  1. Open [bold]Settings[/] → [bold]System[/] → [bold]For developers[/]");
        AnsiConsole.MarkupLine("  2. Toggle [bold]Developer Mode[/] on");
        AnsiConsole.MarkupLine("  3. Re-run [bold]lorex install[/] to replace copies with symlinks");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Or open the settings page now:[/]");
        AnsiConsole.MarkupLine("  [bold]ms-settings:developers[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Asks the user (interactively) whether to open the Developer Mode settings page.
    /// Safe to call during an install flow.
    /// </summary>
    public static void OfferToOpenSettings()
    {
        var open = AnsiConsole.Confirm("[dim]Open Developer Mode settings now?[/]", defaultValue: false);
        if (!open) return;

        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:developers")
            {
                UseShellExecute = true
            });
            AnsiConsole.MarkupLine("[dim]Settings page opened. Enable Developer Mode, then re-run [bold]lorex install[/].[/]");
        }
        catch
        {
            AnsiConsole.MarkupLine("[dim]Could not open Settings automatically. Navigate there manually.[/]");
        }
    }

    /// <summary>
    /// Checks if symlinks are available. If not (Windows without Developer Mode and not elevated),
    /// offers to re-launch the current command as Administrator via UAC.
    /// Returns true if the caller should continue (symlinks available or already elevated).
    /// Returns false if an elevated process was launched (caller should exit with code 0).
    /// Throws if the user declines elevation.
    /// </summary>
    public static bool EnsureSymlinkOrElevate()
    {
        if (IsSymlinkAvailable())
            return true;

        if (!OperatingSystem.IsWindows())
            return true;

        AnsiConsole.MarkupLine("[yellow bold]Symlinks are required but not available.[/]");
        AnsiConsole.MarkupLine("[dim]Windows requires Administrator privileges or Developer Mode to create symlinks.[/]");
        AnsiConsole.WriteLine();

        var elevate = AnsiConsole.Confirm("[bold]Re-run this command as Administrator?[/]", defaultValue: true);
        if (!elevate)
        {
            PrintDevModeGuidance();
            throw new OperationCanceledException("Symlinks are required. Enable Developer Mode or run as Administrator.");
        }

        RelaunchElevated();
        return false;
    }

    /// <summary>
    /// Re-launches the current process with Administrator privileges via UAC.
    /// The current process should exit after calling this.
    /// </summary>
    private static void RelaunchElevated()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the current executable path.");

        var arguments = string.Join(' ', Environment.GetCommandLineArgs().Skip(1));

        try
        {
            var psi = new ProcessStartInfo(exePath, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
            };

            var process = Process.Start(psi);
            process?.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User declined UAC prompt
            throw new OperationCanceledException("Administrator privileges are required for symlinks. Operation cancelled.");
        }
    }

    // ── internals ─────────────────────────────────────────────────────────────

    private static bool IsDevModeEnabled()
    {
        // Query the Windows registry via reg.exe — keeps the binary AOT-safe (no reflection).
        try
        {
            var psi = new ProcessStartInfo(
                "reg",
                @"query HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock /v AllowDevelopmentWithoutDevLicense")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            return output.Contains("0x1", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsElevated()
    {
        try
        {
            // A quick probe: try to create a symlink in the temp directory.
            var test = Path.Combine(Path.GetTempPath(), $"lorex-symtest-{Guid.NewGuid():N}");
            var target = Path.GetTempPath();
            Directory.CreateSymbolicLink(test, target);
            Directory.Delete(test);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
