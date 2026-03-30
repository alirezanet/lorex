#!/usr/bin/env dotnet
// lorex dev installer — builds a local nupkg and installs it as a global tool.
// The package version gets a -dev suffix so it never conflicts with a release build.
//
// Run with: dotnet install.cs
// Unix:     chmod +x install.cs && ./install.cs

#:property PublishAot=false

using System.Diagnostics;
using System.Text.RegularExpressions;

// ── ANSI helpers ──────────────────────────────────────────────────────────────
static void Print(string text) => Console.WriteLine(text);
static void Ok(string text)    => Console.WriteLine($"\x1b[32m✓\x1b[0m  {text}");
static void Info(string text)  => Console.WriteLine($"\x1b[34mℹ\x1b[0m  {text}");
static void Warn(string text)  => Console.WriteLine($"\x1b[33m⚠\x1b[0m  {text}");
static void Err(string text)   => Console.Error.WriteLine($"\x1b[31m✗\x1b[0m  {text}");
static void Bold(string text)  => Console.WriteLine($"\x1b[1m{text}\x1b[0m");
static void Dim(string text)   => Console.WriteLine($"\x1b[2m{text}\x1b[0m");

// ── Shell helper ──────────────────────────────────────────────────────────────
static (int exitCode, string stdout, string stderr) Run(string exe, string arguments)
{
    var psi = new ProcessStartInfo(exe, arguments)
    {
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
        CreateNoWindow         = true,
    };
    using var proc = Process.Start(psi)!;
    var stdout = proc.StandardOutput.ReadToEnd().Trim();
    var stderr = proc.StandardError.ReadToEnd().Trim();
    proc.WaitForExit();
    return (proc.ExitCode, stdout, stderr);
}

// ── Banner ────────────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("\x1b[34m\x1b[1m  lorex dev installer\x1b[0m");
Dim("  Builds a -dev package from source and installs it globally.");
Console.WriteLine();

var repoRoot  = Directory.GetCurrentDirectory();
var csprojPath = Path.Combine(repoRoot, "src", "Lorex", "Lorex.csproj");
var nupkgDir   = Path.Combine(repoRoot, "nupkg");

// ── 1. Verify dotnet SDK ≥ 10 ────────────────────────────────────────────────
var (sdkExit, sdkVersion, _) = Run("dotnet", "--version");
if (sdkExit != 0)
{
    Err("dotnet SDK not found on PATH. Install .NET 10 from https://dot.net");
    return 1;
}

if (!Version.TryParse(sdkVersion.Split('-')[0], out var sdkVer) || sdkVer.Major < 10)
{
    Warn($"Detected .NET SDK {sdkVersion}. lorex requires .NET 10 or later.");
    Warn("Download .NET 10: https://dot.net/10");
}
else
{
    Ok($".NET SDK {sdkVersion}");
}

// ── 2. Read base version from Lorex.csproj ──────────────────────────────────
if (!File.Exists(csprojPath))
{
    Err($"Project file not found: {csprojPath}");
    Err("Run this script from the repository root.");
    return 1;
}

var csprojText  = File.ReadAllText(csprojPath);
var versionMatch = Regex.Match(csprojText, @"<Version>([^<]+)</Version>");
var baseVersion  = versionMatch.Success ? versionMatch.Groups[1].Value.Trim() : "0.0.0";
var devVersion   = $"{baseVersion}-dev";

Ok($"Version: {devVersion}");

// ── 3. Pack with -dev version override ───────────────────────────────────────
Info($"Packing lorex {devVersion}…");

// Clean out any previous dev builds so dotnet tool install always picks the freshest one
if (Directory.Exists(nupkgDir))
{
    foreach (var old in Directory.EnumerateFiles(nupkgDir, "lorex.*-dev.nupkg"))
        File.Delete(old);
}

var (packExit, packOut, packErr) = Run("dotnet",
    $"pack \"{csprojPath}\" -c Release -o \"{nupkgDir}\" /p:Version={devVersion} --nologo -v quiet");

if (packExit != 0)
{
    Err("dotnet pack failed:");
    Err(string.IsNullOrEmpty(packErr) ? packOut : packErr);
    return 1;
}

var builtPkg = Directory.EnumerateFiles(nupkgDir, $"lorex.{devVersion}.nupkg").FirstOrDefault();
if (builtPkg is null)
{
    Err($"Expected package lorex.{devVersion}.nupkg was not found in {nupkgDir}");
    return 1;
}

Ok($"Package built: {Path.GetFileName(builtPkg)}");

// ── 4. Install the dev tool (uninstall first to force refresh) ────────────────
var (listExit, listOut, _) = Run("dotnet", "tool list -g");
var alreadyInstalled = listOut.Contains("lorex", StringComparison.OrdinalIgnoreCase);

if (alreadyInstalled)
{
    Info("Uninstalling existing lorex tool…");
    var (uninstallExit, _, uninstallErr) = Run("dotnet", "tool uninstall -g lorex");
    if (uninstallExit != 0)
    {
        Err("dotnet tool uninstall failed:");
        Err(uninstallErr);
        return 1;
    }
}

Info($"Installing lorex {devVersion}…");

var (installExit, installOut, installErr) = Run("dotnet",
    $"tool install -g lorex --add-source \"{nupkgDir}\" --version \"{devVersion}\"");

if (installExit != 0)
{
    Err("dotnet tool install failed:");
    Err(string.IsNullOrEmpty(installErr) ? installOut : installErr);
    return 1;
}

Ok($"lorex {devVersion} installed.");

// ── 5. Verify lorex is reachable on PATH ──────────────────────────────────────
var (verExit, verOut, _) = Run("lorex", "--version");
if (verExit == 0)
{
    Ok($"lorex {verOut} is on PATH and ready.");
}
else
{
    Warn("lorex was installed but is not on PATH yet.");
    Console.WriteLine();

    var toolsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".dotnet", "tools");

    if (OperatingSystem.IsWindows())
    {
        Bold("  Add the .NET tools directory to your PATH:");
        Print($"    [System Environment Variables] → PATH → add:  {toolsPath}");
        Print("  Or in your current shell session:");
        Print($"    $env:PATH += \";{toolsPath}\"");
    }
    else
    {
        Bold("  Add the .NET tools directory to your PATH:");
        Print("  Add the following line to your ~/.bashrc, ~/.zshrc, or equivalent:");
        Print($"    export PATH=\"$PATH:{toolsPath}\"");
        Print("  Then reload your shell:");
        Print("    source ~/.bashrc");
    }
}

// ── 6. Next steps ─────────────────────────────────────────────────────────────
Console.WriteLine();
Bold("  Next steps:");
Print("    cd your-project");
Print("    lorex init          # connect to a skill registry");
Print("    lorex list          # browse available skills");
Print("    lorex install <skill>");
Console.WriteLine();
Dim("  Docs: https://github.com/your-org/lorex");
Console.WriteLine();

return 0;

