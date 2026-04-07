using Lorex.Core.Models;

namespace Lorex.Cli;

/// <summary>
/// Read-only TUI browser for <c>lorex list</c>.
/// Supports real-time search filtering, paging, and status-aware display.
/// </summary>
internal static class SkillBrowserTui
{
    private const int PageSize = 18;

    // ANSI helpers (shared style with SkillPickerTui)
    private const string Rst  = "\x1b[0m";
    private const string Bold = "\x1b[1m";
    private const string Dim  = "\x1b[2m";
    private const string Rev  = "\x1b[7m";
    private const string Cyan = "\x1b[1;36m";
    private const string Grn  = "\x1b[1;32m";
    private const string Yel  = "\x1b[33m";
    private const string Blue = "\x1b[34m";

    private sealed class State
    {
        public string Search    = "";
        public int    Page      = 0;
        public int    Cursor    = 0;
        public bool   Exited    = false;
        public bool   ShowTaps  = true;
        public int    TapCount  = 0;   // tap skills matching current search (set by ComputePage)
    }

    /// <summary>
    /// Shows the TUI registry browser. Blocks until the user exits (Esc / Enter).
    /// <paramref name="initialSearch"/> and <paramref name="tagFilter"/> pre-populate filters.
    /// <paramref name="skillSources"/> maps skill name → source label (e.g. <c>"tap:dotnet"</c>);
    /// absent entries are primary-registry skills.
    /// </summary>
    internal static void Run(
        IReadOnlyList<SkillMetadata> allSkills,
        HashSet<string>              installed,
        Dictionary<string, string>   installedVersions,
        HashSet<string>              recommendedSet,
        Dictionary<string, string>?  skillSources  = null,
        string?                      initialSearch = null,
        string?                      tagFilter     = null)
    {
        if (Console.IsOutputRedirected || Console.IsInputRedirected)
            return;

        var state = new State { Search = initialSearch ?? "" };
        var lastLines = 0;
        var width = SafeWidth();

        Console.CursorVisible = false;
        try
        {
            while (!state.Exited)
            {
                var (filtered, pageItems, totalPages) =
                    ComputePage(allSkills, state, tagFilter, recommendedSet, installed, installedVersions, skillSources);

                Render(state, filtered, pageItems, totalPages,
                       installed, installedVersions, recommendedSet,
                       skillSources, tagFilter, width, ref lastLines);

                ConsoleKeyInfo key;
                try   { key = Console.ReadKey(intercept: true); }
                catch { break; }

                HandleKey(key, state, pageItems, totalPages);
            }
        }
        finally
        {
            if (lastLines > 0) Console.Write($"\x1b[{lastLines}A");
            Console.Write("\x1b[J");
            Console.CursorVisible = true;
        }
    }

    // ── Page computation ─────────────────────────────────────────────────────

    private static (List<SkillMetadata> filtered, List<SkillMetadata> pageItems, int totalPages)
        ComputePage(
            IReadOnlyList<SkillMetadata> all,
            State                        state,
            string?                      tagFilter,
            HashSet<string>              recommendedSet,
            HashSet<string>              installed,
            Dictionary<string, string>   installedVersions,
            Dictionary<string, string>?  skillSources)
    {
        IEnumerable<SkillMetadata> result = all;

        if (!string.IsNullOrWhiteSpace(tagFilter))
            result = result.Where(s => s.Tags.Contains(tagFilter, StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(state.Search))
        {
            var q = state.Search;
            result = result.Where(s =>
                s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        // Materialise once to count taps (for the footer "N taps hidden" message)
        var enumerated = result.ToList();
        state.TapCount = enumerated.Count(s => IsTapSkill(s.Name, skillSources));

        // Apply tap-visibility toggle
        IEnumerable<SkillMetadata> visible = enumerated;
        if (!state.ShowTaps)
            visible = visible.Where(s => !IsTapSkill(s.Name, skillSources));

        // Sort: installed+update → installed → recommended → registry before tap → alphabetical
        var filtered = visible
            .OrderByDescending(s => installed.Contains(s.Name) && HasUpdate(s, installedVersions))
            .ThenByDescending(s => installed.Contains(s.Name))
            .ThenByDescending(s => recommendedSet.Contains(s.Name))
            .ThenByDescending(s => !IsTapSkill(s.Name, skillSources))  // registry first
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)PageSize));
        state.Page    = Math.Clamp(state.Page,   0, totalPages - 1);
        var pageItems  = filtered.Skip(state.Page * PageSize).Take(PageSize).ToList();
        state.Cursor  = Math.Clamp(state.Cursor, 0, Math.Max(0, pageItems.Count - 1));

        return (filtered, pageItems, totalPages);
    }

    // ── Key handling ─────────────────────────────────────────────────────────

    private static void HandleKey(ConsoleKeyInfo key, State state, List<SkillMetadata> pageItems, int totalPages)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (state.Cursor > 0)
                    state.Cursor--;
                else if (state.Page > 0)
                    { state.Page--; state.Cursor = PageSize - 1; }
                break;

            case ConsoleKey.DownArrow:
                if (state.Cursor < pageItems.Count - 1)
                    state.Cursor++;
                else if (state.Page < totalPages - 1)
                    { state.Page++; state.Cursor = 0; }
                break;

            case ConsoleKey.PageUp:
                if (state.Page > 0) { state.Page--; state.Cursor = 0; }
                break;

            case ConsoleKey.PageDown:
                if (state.Page < totalPages - 1) { state.Page++; state.Cursor = 0; }
                break;

            case ConsoleKey.Enter:
                state.Exited = true;
                break;

            case ConsoleKey.Tab:
                state.ShowTaps = !state.ShowTaps;
                state.Page     = 0;
                state.Cursor   = 0;
                break;

            case ConsoleKey.Escape:
                if (state.Search.Length > 0)
                    { state.Search = ""; state.Page = 0; state.Cursor = 0; }
                else
                    state.Exited = true;
                break;

            case ConsoleKey.Backspace:
                if (state.Search.Length > 0)
                    { state.Search = state.Search[..^1]; state.Page = 0; state.Cursor = 0; }
                break;

            default:
                if (!char.IsControl(key.KeyChar) && key.KeyChar != '\0')
                {
                    state.Search += key.KeyChar;
                    state.Page    = 0;
                    state.Cursor  = 0;
                }
                break;
        }
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    private static void Render(
        State                       state,
        List<SkillMetadata>         filtered,
        List<SkillMetadata>         pageItems,
        int                         totalPages,
        HashSet<string>             installed,
        Dictionary<string, string>  installedVersions,
        HashSet<string>             recommendedSet,
        Dictionary<string, string>? skillSources,
        string?                     tagFilter,
        int                         width,
        ref int                     lastLines)
    {
        var sb = new System.Text.StringBuilder(2048);

        if (lastLines > 0)
            sb.Append($"\x1b[{lastLines}A");

        var lines = 0;
        var sep   = new string('─', width);

        void Line(string content)
        {
            sb.Append(content).Append("\x1b[K\r\n");
            lines++;
        }

        // Determine whether the source column is needed (any tap skills in the full set)
        var hasTapSkills = skillSources != null &&
            skillSources.Values.Any(s => s.StartsWith("tap:", StringComparison.OrdinalIgnoreCase));

        // ── Header ───────────────────────────────────────────────────────────
        var tapHint = hasTapSkills ? $" {Dim}· Tab taps{Rst}" : "";
        Line($"{Bold}Browse Registry Skills{Rst}  {Dim}↑↓ navigate · type to filter · Esc clear · Enter/Esc exit{Rst}{tapHint}");

        // ── Search bar ───────────────────────────────────────────────────────
        var hint = state.Search.Length == 0 ? $"{Dim}type to filter by name, description, or tag…{Rst}" : "";
        Line($"{Dim}Search:{Rst} {Bold}{Esc(state.Search)}{Rst}{Rev} {Rst} {hint}");

        if (tagFilter is not null)
            Line($"{Dim}Tag filter:{Rst} {Blue}{Esc(tagFilter)}{Rst}  {Dim}(re-run without --tag to remove){Rst}");

        Line($"{Dim}{sep}{Rst}");

        // ── Column headers ───────────────────────────────────────────────────
        //  Row layout:  " ›  ST  Name__________  Source______  Description"
        //  Overhead before name: cursor(2) + icon(2) + 2 spaces = 6
        var nameColWidth = pageItems.Count > 0
            ? Math.Clamp(pageItems.Max(s => s.Name.Length) + 1, 18, 50)
            : 26;
        // Source column: 2-space lead + 10-char value = 12 total (only shown when taps present)
        const int SourceColWidth = 10;
        const int SourceOverhead = 12;  // 2 leading spaces + SourceColWidth
        var sourceOverhead = hasTapSkills ? SourceOverhead : 0;
        var maxDesc = Math.Max(20, width - nameColWidth - 7 - sourceOverhead);

        var sourceHdr = hasTapSkills ? $"  {Dim}{"Source",-SourceColWidth}{Rst}" : "";
        var hdrName   = "Skill".PadRight(nameColWidth);
        Line($"{Dim}  {"St",-2}  {hdrName}{sourceHdr}  {"Description"}{Rst}");
        Line($"{Dim}{sep}{Rst}");

        // ── Skill rows ───────────────────────────────────────────────────────
        if (pageItems.Count == 0)
        {
            string msg;
            if (!state.ShowTaps && state.TapCount > 0 && string.IsNullOrWhiteSpace(state.Search))
                msg = $"{Yel}  All skills are from taps — press Tab to show them{Rst}";
            else if (!string.IsNullOrWhiteSpace(state.Search))
                msg = $"{Yel}  No skills match \"{Esc(state.Search)}\" — press Backspace to refine{Rst}";
            else
                msg = $"{Dim}  No skills found.{Rst}";
            Line(msg);
            for (var p = 1; p < PageSize; p++) Line("");
        }
        else
        {
            for (var i = 0; i < pageItems.Count; i++)
            {
                var skill    = pageItems[i];
                var isCursor = i == state.Cursor;

                var (icon, iconColor) = GetStatusIcon(skill, installed, installedVersions, recommendedSet);

                var arrow   = isCursor ? $"{Cyan}›{Rst}" : " ";
                var nameClr = isCursor ? Cyan : "";
                var nameEnd = isCursor ? Rst  : "";
                var name    = PadTrunc(skill.Name, nameColWidth);

                // Source column: fixed width, no parentheses
                var sourceCol = "";
                if (hasTapSkills)
                {
                    if (skillSources != null &&
                        skillSources.TryGetValue(skill.Name, out var src) &&
                        src.StartsWith("tap:", StringComparison.OrdinalIgnoreCase))
                    {
                        var tapLabel = src["tap:".Length..];
                        if (tapLabel.Length > SourceColWidth) tapLabel = tapLabel[..(SourceColWidth - 1)] + "…";
                        sourceCol = $"  {Blue}{tapLabel,-SourceColWidth}{Rst}";
                    }
                    else
                    {
                        // Registry skill: dim placeholder so columns stay aligned
                        sourceCol = $"  {Dim}{"registry",-SourceColWidth}{Rst}";
                    }
                }

                var desc = string.IsNullOrWhiteSpace(skill.Description)
                    ? ""
                    : $"  {Dim}{Esc(Trunc(skill.Description, maxDesc))}{Rst}";

                Line($" {arrow} {iconColor}{icon}{Rst}  {nameClr}{Esc(name)}{nameEnd}{sourceCol}{desc}");
            }

            for (var p = pageItems.Count; p < PageSize; p++) Line("");
        }

        // ── Footer ───────────────────────────────────────────────────────────
        Line($"{Dim}{sep}{Rst}");

        var pageLabel  = $"Page {Bold}{state.Page + 1}{Rst}{Dim}/{totalPages}{Rst}";
        var countLabel = $"{Dim}{filtered.Count} result{(filtered.Count == 1 ? "" : "s")}{Rst}";
        var pageHint   = totalPages > 1 ? $"  {Dim}PgUp/PgDn to page{Rst}" : "";
        var legend     = $"  {Dim}{Grn}✓{Rst}{Dim} installed  {Yel}↑{Rst}{Dim} update  {Blue}★{Rst}{Dim} recommended{Rst}";

        string tapStatus;
        if (hasTapSkills)
        {
            if (!state.ShowTaps)
                tapStatus = state.TapCount > 0
                    ? $"  {Yel}■ {state.TapCount} tap skill{(state.TapCount == 1 ? "" : "s")} hidden{Rst} {Dim}(Tab to show){Rst}"
                    : $"  {Dim}Taps hidden{Rst} {Dim}(Tab to show){Rst}";
            else
                tapStatus = $"  {Dim}Tab: hide taps{Rst}";
        }
        else
            tapStatus = "";

        Line($" {pageLabel}  {countLabel}{pageHint}{tapStatus}{legend}");

        sb.Append("\x1b[J");
        Console.Write(sb);
        lastLines = lines;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsTapSkill(string name, Dictionary<string, string>? sources) =>
        sources != null &&
        sources.TryGetValue(name, out var src) &&
        src.StartsWith("tap:", StringComparison.OrdinalIgnoreCase);

    private static (string icon, string color) GetStatusIcon(
        SkillMetadata              skill,
        HashSet<string>            installed,
        Dictionary<string, string> installedVersions,
        HashSet<string>            recommendedSet)
    {
        if (installed.Contains(skill.Name))
            return HasUpdate(skill, installedVersions) ? ("↑", Yel) : ("✓", Grn);
        if (recommendedSet.Contains(skill.Name))
            return ("★", Blue);
        return (" ", Dim);
    }

    private static bool HasUpdate(SkillMetadata skill, Dictionary<string, string> installedVersions)
    {
        if (!installedVersions.TryGetValue(skill.Name, out var iv)) return false;
        return Version.TryParse(skill.Version, out var rv)
            && Version.TryParse(iv, out var ivv)
            && rv > ivv;
    }

    private static int SafeWidth()
    {
        try { return Math.Max(60, Console.WindowWidth - 1); }
        catch { return 80; }
    }

    private static string Esc(string s)   => s.Replace("\x1b", "");
    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    private static string PadTrunc(string s, int width) =>
        s.Length >= width ? s[..(width - 1)] + "…" : s.PadRight(width);
}
