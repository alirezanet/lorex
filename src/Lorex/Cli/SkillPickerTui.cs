using Lorex.Core.Models;

namespace Lorex.Cli;

/// <summary>
/// Full-screen TUI skill picker for <c>lorex install</c>.
/// Supports real-time search, paging, and multi-selection with keyboard navigation.
/// </summary>
internal static class SkillPickerTui
{
    private const int PageSize = 15;

    // ANSI helpers
    private const string Rst  = "\x1b[0m";
    private const string Bold = "\x1b[1m";
    private const string Dim  = "\x1b[2m";
    private const string Rev  = "\x1b[7m";   // reverse video (cursor block)
    private const string Cyan = "\x1b[1;36m";
    private const string Grn  = "\x1b[1;32m";
    private const string Blue = "\x1b[34m";
    private const string Yel  = "\x1b[33m";

    private sealed class State
    {
        public string  Search    = "";
        public int     Page      = 0;
        public int     Cursor    = 0;
        public bool    Confirmed = false;
        public bool    Cancelled = false;
        public bool    ShowTaps  = true;
        public int     TapCount  = 0;   // tap skills matching current search (set by ComputePage)
        public readonly HashSet<string> Selected = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs the TUI picker. Returns selected skill names, or an empty list if cancelled.
    /// <paramref name="initialSearch"/> pre-populates the search buffer.
    /// <paramref name="tagFilter"/> is an immutable tag filter shown in the header.
    /// <paramref name="skillSources"/> maps skill name → source label (e.g. <c>"tap:dotnet"</c>).
    /// </summary>
    internal static List<string> Run(
        IReadOnlyList<SkillMetadata> choices,
        HashSet<string>              recommendedSet,
        Dictionary<string, string>?  skillSources  = null,
        string?                      initialSearch = null,
        string?                      tagFilter     = null)
    {
        if (Console.IsInputRedirected)
            return [];

        var state = new State { Search = initialSearch ?? "" };
        var lastLines = 0;
        var width = SafeWidth();

        Console.CursorVisible = false;
        try
        {
            while (!state.Confirmed && !state.Cancelled)
            {
                var (filtered, pageItems, totalPages) = ComputePage(choices, state, tagFilter, recommendedSet, skillSources);
                Render(state, filtered, pageItems, totalPages, recommendedSet, skillSources, tagFilter, width, ref lastLines);

                ConsoleKeyInfo key;
                try   { key = Console.ReadKey(intercept: true); }
                catch { state.Cancelled = true; break; }

                HandleKey(key, state, filtered, pageItems, totalPages);
            }
        }
        finally
        {
            // Erase the TUI area cleanly
            if (lastLines > 0) Console.Write($"\x1b[{lastLines}A");
            Console.Write("\x1b[J");
            Console.CursorVisible = true;
        }

        return state.Cancelled ? [] : [.. state.Selected];
    }

    // ── Page computation ─────────────────────────────────────────────────────

    private static (List<SkillMetadata> filtered, List<SkillMetadata> pageItems, int totalPages)
        ComputePage(
            IReadOnlyList<SkillMetadata> choices,
            State state,
            string? tagFilter,
            HashSet<string> recommendedSet,
            Dictionary<string, string>? skillSources)
    {
        IEnumerable<SkillMetadata> result = choices;

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

        // Materialise once to count tap skills (for "N hidden" footer message)
        var enumerated = result.ToList();
        state.TapCount = enumerated.Count(s => IsTapSkill(s.Name, skillSources));

        // Apply tap-visibility toggle
        IEnumerable<SkillMetadata> visible = enumerated;
        if (!state.ShowTaps)
            visible = visible.Where(s => !IsTapSkill(s.Name, skillSources));

        // Sort: recommended first → registry before tap → alphabetical
        var filtered = visible
            .OrderByDescending(s => recommendedSet.Contains(s.Name))
            .ThenByDescending(s => !IsTapSkill(s.Name, skillSources))  // registry first
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)PageSize));
        state.Page   = Math.Clamp(state.Page,   0, totalPages - 1);
        var pageItems = filtered.Skip(state.Page * PageSize).Take(PageSize).ToList();
        state.Cursor = Math.Clamp(state.Cursor, 0, Math.Max(0, pageItems.Count - 1));

        return (filtered, pageItems, totalPages);
    }

    // ── Key handling ─────────────────────────────────────────────────────────

    private static void HandleKey(ConsoleKeyInfo key, State state, List<SkillMetadata> filtered, List<SkillMetadata> pageItems, int totalPages)
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

            case ConsoleKey.Spacebar:
                if (pageItems.Count > 0)
                {
                    var name = pageItems[state.Cursor].Name;
                    if (!state.Selected.Remove(name)) state.Selected.Add(name);
                    // Advance cursor so rapid Space-pressing walks down
                    if (state.Cursor < pageItems.Count - 1) state.Cursor++;
                }
                break;

            case ConsoleKey.Enter:
                state.Confirmed = true;
                break;

            case ConsoleKey.Tab:
                state.ShowTaps = !state.ShowTaps;
                state.Page     = 0;
                state.Cursor   = 0;
                break;

            case ConsoleKey.A when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                // Toggle all filtered (visible) skills — Ctrl+A selects all, second press deselects all
                var allNames = filtered.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (allNames.IsSubsetOf(state.Selected))
                    foreach (var n in allNames) state.Selected.Remove(n);
                else
                    foreach (var n in allNames) state.Selected.Add(n);
                break;

            case ConsoleKey.Escape:
                if (state.Search.Length > 0)
                    { state.Search = ""; state.Page = 0; state.Cursor = 0; }
                else
                    state.Cancelled = true;
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
        HashSet<string>             recommendedSet,
        Dictionary<string, string>? skillSources,
        string?                     tagFilter,
        int                         width,
        ref int                     lastLines)
    {
        var sb = new System.Text.StringBuilder(2048);

        // Go back to the start of the previous render
        if (lastLines > 0)
            sb.Append($"\x1b[{lastLines}A");

        var lines = 0;
        var sep   = new string('─', width);

        // Each call appends one terminal line (content + erase-to-EOL + CRLF)
        void Line(string content)
        {
            sb.Append(content).Append("\x1b[K\r\n");
            lines++;
        }

        var hasTapSkills = skillSources != null &&
            skillSources.Values.Any(s => s.StartsWith("tap:", StringComparison.OrdinalIgnoreCase));

        // ── Header ───────────────────────────────────────────────────────────
        var tapHint = hasTapSkills ? $" {Dim}· Tab taps{Rst}" : "";
        Line($"{Bold}Install Skills{Rst}  {Dim}↑↓ navigate · Space select · Ctrl+A all · Enter confirm · Esc cancel{Rst}{tapHint}");

        // ── Search bar ───────────────────────────────────────────────────────
        var hint = state.Search.Length == 0 ? $"{Dim}type to filter…{Rst}" : "";
        Line($"{Dim}Search:{Rst} {Bold}{Esc(state.Search)}{Rst}{Rev} {Rst} {hint}");

        if (tagFilter is not null)
            Line($"{Dim}Tag:{Rst} {Blue}{Esc(tagFilter)}{Rst}  {Dim}(Esc clears search · re-run without --tag to remove){Rst}");

        Line($"{Dim}{sep}{Rst}");

        // ── Skill rows ───────────────────────────────────────────────────────
        //  Row layout: " › [✓] ★  NAME…  Source____  — DESC…"
        //  Fixed overhead before name: " › [✓] ★  " = 9 chars
        //  Source column: 2-space lead + 10-char value = 12 total (only when taps present)
        const int SourceColWidth = 10;
        const int SourceOverhead = 12;
        var sourceOverhead = hasTapSkills ? SourceOverhead : 0;
        var nameColWidth = pageItems.Count > 0
            ? Math.Clamp(pageItems.Max(s => s.Name.Length) + 1, 18, 50)
            : 26;
        var maxDesc = Math.Max(20, width - nameColWidth - 12 - sourceOverhead);

        if (pageItems.Count == 0)
        {
            string msg;
            if (!state.ShowTaps && state.TapCount > 0 && string.IsNullOrWhiteSpace(state.Search))
                msg = $"{Yel}  All available skills are from taps — press Tab to show them{Rst}";
            else if (!string.IsNullOrWhiteSpace(state.Search))
                msg = $"{Yel}  No skills match \"{Esc(state.Search)}\" — press Backspace to refine{Rst}";
            else
                msg = $"{Dim}  No skills available.{Rst}";
            Line(msg);
            for (var p = 1; p < PageSize; p++) Line("");
        }
        else
        {
            for (var i = 0; i < pageItems.Count; i++)
            {
                var skill     = pageItems[i];
                var isCursor  = i == state.Cursor;
                var isSel     = state.Selected.Contains(skill.Name);
                var isRec     = recommendedSet.Contains(skill.Name);

                var arrow = isCursor ? $"{Cyan}›{Rst}" : " ";
                var box   = isSel    ? $"{Grn}[✓]{Rst}" : $"{Dim}[ ]{Rst}";
                var star  = isRec    ? $"{Blue}★{Rst}" : " ";

                var nameClr  = isCursor ? Cyan : isSel ? Grn : "";
                var nameEnd  = (isCursor || isSel) ? Rst : "";
                var name     = PadTrunc(skill.Name, nameColWidth);

                // Source column: fixed width, replaces the old (tapname) badge
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
                        // Registry skill: dim placeholder to keep columns aligned
                        sourceCol = $"  {Dim}{"registry",-SourceColWidth}{Rst}";
                    }
                }

                var desc = string.IsNullOrWhiteSpace(skill.Description)
                    ? ""
                    : $" {Dim}— {Esc(Trunc(skill.Description, maxDesc))}{Rst}";

                Line($" {arrow} {box} {star} {nameClr}{Esc(name)}{nameEnd}{sourceCol}{desc}");
            }

            // Pad to PageSize so the footer doesn't jump when results change
            for (var p = pageItems.Count; p < PageSize; p++) Line("");
        }

        // ── Footer ───────────────────────────────────────────────────────────
        Line($"{Dim}{sep}{Rst}");

        var pageLabel  = $"Page {Bold}{state.Page + 1}{Rst}{Dim}/{totalPages}{Rst}";
        var countLabel = $"{Dim}{filtered.Count} result{(filtered.Count == 1 ? "" : "s")}{Rst}";
        var selLabel   = state.Selected.Count == 0
            ? $"{Dim}0 selected{Rst}"
            : $"{Grn}{Bold}{state.Selected.Count} selected{Rst}";
        var pageHint   = totalPages > 1 ? $"  {Dim}PgUp/PgDn to page{Rst}" : "";

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

        Line($" {pageLabel}  {countLabel}  {selLabel}{pageHint}{tapStatus}");

        // Erase any leftover lines from a previous (taller) render
        sb.Append("\x1b[J");

        Console.Write(sb);
        lastLines = lines;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsTapSkill(string name, Dictionary<string, string>? sources) =>
        sources != null &&
        sources.TryGetValue(name, out var src) &&
        src.StartsWith("tap:", StringComparison.OrdinalIgnoreCase);

    private static int SafeWidth()
    {
        try { return Math.Max(60, Console.WindowWidth - 1); }
        catch { return 80; }
    }

    /// <summary>Strip ESC characters from user-provided strings to prevent injection.</summary>
    private static string Esc(string s) => s.Replace("\x1b", "");

    private static string Trunc(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    /// <summary>Pads or truncates <paramref name="s"/> to exactly <paramref name="width"/> visible chars.</summary>
    private static string PadTrunc(string s, int width) =>
        s.Length >= width ? s[..(width - 1)] + "…" : s.PadRight(width);
}
