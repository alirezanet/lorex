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
