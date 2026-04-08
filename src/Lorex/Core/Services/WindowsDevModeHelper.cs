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
    /// Ensures symlinks are available. If Developer Mode is not enabled, opens the
    /// Windows Settings page directly and waits for the user to toggle it on.
    /// Returns true when the caller should continue. Throws on user cancellation.
    /// </summary>
    public static bool EnsureSymlinkOrElevate()
    {
        if (IsSymlinkAvailable())
            return true;

        if (!OperatingSystem.IsWindows())
            return true;

        AnsiConsole.MarkupLine("[yellow bold]Symlinks require Windows Developer Mode.[/]");
        AnsiConsole.MarkupLine("[dim]Opening the Settings page — toggle [bold]Developer Mode[/] on, then come back here.[/]");
        AnsiConsole.WriteLine();

        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:developers") { UseShellExecute = true });
        }
        catch
        {
            AnsiConsole.MarkupLine("[red]Could not open Settings automatically.[/]");
            AnsiConsole.MarkupLine("[dim]Open [bold]Settings → System → For developers[/] and enable Developer Mode manually.[/]");
        }

        AnsiConsole.MarkupLine("[dim]Press [bold]Enter[/] once Developer Mode is enabled…[/]");
        Console.ReadLine();

        // Clear the cached check so we re-probe.
        _cached = null;

        if (!IsSymlinkAvailable())
        {
            AnsiConsole.MarkupLine("[red]Developer Mode is still not detected.[/]");
            AnsiConsole.MarkupLine("[dim]Make sure the toggle is on, then try again.[/]");
            throw new OperationCanceledException("Symlinks are required. Enable Developer Mode and re-run the command.");
        }

        AnsiConsole.MarkupLine("[green]✓[/] Developer Mode detected — continuing.");
        return true;
    }

    /// <summary>
    /// Prints a formatted message explaining that Developer Mode is off,
    /// with step-by-step instructions to enable it manually.
    /// </summary>
    public static void PrintDevModeGuidance()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow bold]Symlinks require Windows Developer Mode[/]");
        AnsiConsole.MarkupLine("[dim]Without Developer Mode, lorex cannot create symlinks for shared skills.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]To enable Developer Mode:[/]");
        AnsiConsole.MarkupLine("  1. Open [bold]Settings[/] → [bold]System[/] → [bold]For developers[/]");
        AnsiConsole.MarkupLine("  2. Toggle [bold]Developer Mode[/] on");
        AnsiConsole.MarkupLine("  3. Re-run the lorex command");
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
            AnsiConsole.MarkupLine("[dim]Settings page opened. Enable Developer Mode, then re-run the command.[/]");
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
