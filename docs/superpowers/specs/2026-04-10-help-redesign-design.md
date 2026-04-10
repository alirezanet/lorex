# Help Command Redesign

**Date:** 2026-04-10
**Status:** Approved

## Problem

The current lorex help system has three issues:

1. **Missing coverage.** 8 of 11 commands (`install`, `uninstall`, `create`, `publish`, `sync`, `list`, `status`, `refresh`) silently ignore `--help` and jump straight into execution.
2. **Inconsistent format.** The 3 commands that do respond (`init`, `registry`, `tap`) each use a different structure and visual style.
3. **Broken global layout.** The global `lorex --help` uses a 3-column grid where the middle column (args, fixed at 36 chars) overflows for commands with many flags, and the command groupings are unintuitive.

## Goals

- Every command responds to `--help` / `-h` with a consistent, structured output.
- The global `lorex --help` is clean, readable, and surfaces common flags.
- Code stays clean — no copy-pasted formatting logic across 11 files.
- Output is readable by both humans and AI agents.

## Non-goals

- Per-subcommand help for `lorex tap add --help`, `lorex tap remove --help`, etc. The combined `lorex tap --help` is sufficient given the number and brevity of subcommands.
- Changing any command's actual behavior or flags.

---

## Architecture

### New: `Cli/HelpPrinter.cs`

A single static class with one `Print()` method. All command-specific `PrintHelp()` methods call it. The signature:

```csharp
internal static class HelpPrinter
{
    public static void Print(
        string usage,
        string description,
        (string Flags, string Description)[]? options = null,
        (string Comment, string Command)[]? examples = null,
        (string Signature, string Description)[]? subcommands = null)
}
```

- `options` renders as an OPTIONS section (grid with auto-sized flag column).
- `subcommands` renders as a SUBCOMMANDS section (used by `lorex tap`); if present, OPTIONS is rendered after it for shared flags.
- `examples` renders as an EXAMPLES section with `# comment` lines above each command.
- All user-visible strings are passed through `Markup.Escape()`.

### Changes to existing files

**`Program.cs`** — `PrintHelp()` redesigned (see Global Help section below).

**Each of the 11 command files** — add a `--help` / `-h` check at the top of `Run()` that calls `PrintHelp()`, which in turn calls `HelpPrinter.Print()` with that command's data.

---

## Global Help (`lorex --help`)

Keeps the FigletText banner and tagline. Replaces the 3-column grid with a flat COMMANDS list (command + one-line description, ordered by natural workflow) followed by a FLAGS section that surfaces the two flags common across most commands.

```
(FigletText "lorex")
v{version} — Teach your AI agents once. Reuse everywhere.

USAGE  lorex <command> [args]

COMMANDS
  init        Configure lorex for this project or globally
  install     Install skills from the registry, taps, or a URL
  uninstall   Remove installed skills
  create      Scaffold a new skill for authoring
  list        Browse and filter available skills
  status      Show installed skills and their state
  sync        Pull latest skill versions from registry and taps
  publish     Push local skills to the registry
  refresh     Re-project skills into native agent locations
  registry    Configure the connected registry policy
  tap         Manage read-only skill sources

FLAGS
  -g, --global    Operate on the global lorex root (~/.lorex)
  -h, --help      Show help for a command
  -v, --version   Show version

Run 'lorex <command> --help' for command-specific flags and examples.
```

Implementation: replace the `Section()` / `Row()` / `MakeGrid()` helpers and the three `Section(...)` calls with a single two-column grid for COMMANDS (command name column fixed-width, description column auto) and a small FLAGS grid.

---

## Per-command Help Format

Every command uses this structure (all sections rendered by `HelpPrinter.Print`):

```
USAGE
  lorex <command> [<args>] [flags]

DESCRIPTION
  One or two sentences. What it does and when to use it.

OPTIONS
  <positional>       Description
  --flag <value>     Description
  -s, --shorthand    Description
  -h, --help         Show this help

EXAMPLES
  # Comment describing the scenario
  lorex <command> ...
```

### Command-specific data

#### `lorex init`
```
USAGE:    lorex init [<url>] [--local] [--adapters <a,b>] [--global]
DESC:     Configure lorex for this project (or globally with --global).
          Running without arguments launches an interactive setup wizard.
OPTIONS:
  <url>                    Registry URL (HTTPS/SSH) or local absolute path
  --local                  Skip registry setup; manage skills locally
  -a, --adapters <a,b>     Comma-separated adapters to enable (e.g. claude,copilot)
  -g, --global             Initialise the global lorex root (~/.lorex)
  -h, --help               Show this help
EXAMPLES:
  # Interactive setup
  lorex init
  # Connect to a remote registry
  lorex init https://github.com/org/skills --adapters claude,copilot
  # Use a local path as registry
  lorex init /path/to/registry --adapters claude
  # Local-only, no registry
  lorex init --local --adapters claude
  # Global install
  lorex init --global https://github.com/org/skills
```

#### `lorex install`
```
USAGE:    lorex install [<skill>…] [--all] [--recommended] [--search <text>] [--tag <tag>] [-g]
DESC:     Install skills from the registry, taps, or a URL.
          Running without arguments opens an interactive picker.
OPTIONS:
  <skill>…               Skill names or URLs to install
  --all                  Install all available skills
  --recommended          Install skills recommended for this project
  --search <text>        Pre-filter the picker by name or description
  --tag <tag>            Pre-filter the picker by tag
  -g, --global           Operate on the global lorex root (~/.lorex)
  -h, --help             Show this help
EXAMPLES:
  # Interactive picker
  lorex install
  # Install a specific skill
  lorex install my-skill
  # Install all recommended skills
  lorex install --recommended
  # Install all available skills
  lorex install --all
  # Install from a URL
  lorex install https://github.com/org/skill-repo
```

#### `lorex uninstall`
```
USAGE:    lorex uninstall [<skill>…] [--all] [-g]
DESC:     Remove installed skills. Running without arguments opens an interactive picker.
OPTIONS:
  <skill>…        Skill names to uninstall
  --all           Uninstall all installed skills
  -g, --global    Operate on the global lorex root (~/.lorex)
  -h, --help      Show this help
EXAMPLES:
  # Interactive picker
  lorex uninstall
  # Remove a specific skill
  lorex uninstall my-skill
  # Remove all skills
  lorex uninstall --all
```

#### `lorex create`
```
USAGE:    lorex create [<name>] [-d <desc>] [-t <tags>] [-o <owner>]
DESC:     Scaffold a new skill in .lorex/skills/ for local authoring.
          Running without arguments prompts interactively.
OPTIONS:
  <name>                   Skill name (kebab-case)
  -d, --description        One-line description shown in lorex list
  -t, --tags <a,b>         Comma-separated tags for discovery
  -o, --owner <name>       Team or individual name
  -h, --help               Show this help
EXAMPLES:
  # Interactive
  lorex create
  # Non-interactive
  lorex create auth-overview -d "Auth patterns for this repo" -t auth,security
```

#### `lorex list`
```
USAGE:    lorex list [--search <text>] [--tag <tag>] [--page <n>] [--page-size <n>] [-g]
DESC:     Browse skills available in the registry and taps.
          Opens an interactive TUI in a terminal; outputs a plain table when piped.
OPTIONS:
  --search <text>     Filter by name or description
  --tag <tag>         Filter by tag
  --page <n>          Page number (default: 1)
  --page-size <n>     Results per page (default: 25; use 0 to show all)
  -g, --global        Operate on the global lorex root (~/.lorex)
  -h, --help          Show this help
EXAMPLES:
  # Interactive TUI
  lorex list
  # Filter by keyword
  lorex list --search auth
  # Filter by tag
  lorex list --tag security
  # Paginate non-interactively
  lorex list --page 2 --page-size 10
```

#### `lorex status`
```
USAGE:    lorex status [-g]
DESC:     Show the registry, adapters, and installed skill link states for this project.
OPTIONS:
  -g, --global    Show global lorex state (~/.lorex) instead of the current project
  -h, --help      Show this help
EXAMPLES:
  lorex status
  lorex status --global
```

#### `lorex sync`
```
USAGE:    lorex sync [-g]
DESC:     Pull the latest skill versions from the registry and all taps,
          and restore any missing symlinks (e.g. after a fresh clone).
OPTIONS:
  -g, --global    Operate on the global lorex root (~/.lorex)
  -h, --help      Show this help
EXAMPLES:
  lorex sync
  lorex sync --global
```

#### `lorex publish`
```
USAGE:    lorex publish [<skill>…]
DESC:     Push local skills to the registry. Running without arguments opens an interactive picker.
          Direct registries publish immediately; pull-request registries prepare a review branch.
OPTIONS:
  <skill>…    Skill names to publish
  -h, --help  Show this help
EXAMPLES:
  # Interactive picker
  lorex publish
  # Publish a specific skill
  lorex publish my-skill
```

#### `lorex refresh`
```
USAGE:    lorex refresh [--target <adapter>]
DESC:     Re-project lorex skills into native agent locations without fetching from the registry.
          Useful after adding a new adapter or when projections are out of sync.
OPTIONS:
  -t, --target <adapter>    Re-project a single adapter only
  -h, --help                Show this help
EXAMPLES:
  # Refresh all adapters
  lorex refresh
  # Refresh only the Claude adapter
  lorex refresh --target claude
```

#### `lorex registry`
```
USAGE:    lorex registry
DESC:     Interactively update the connected registry's publish policy and recommended taps.
          Direct registries update immediately; pull-request registries prepare a review branch.
OPTIONS:
  -h, --help    Show this help
```

#### `lorex tap`
```
USAGE:    lorex tap <subcommand> [args]
DESC:     Manage read-only skill sources (taps). Skills from taps appear
          alongside registry skills in lorex list and lorex install.
SUBCOMMANDS:
  add <url> [--name <name>] [--root <path>] [-g]    Add a tap
  remove <name> [-g]                                Remove a tap
  list [-g]                                         List configured taps
  sync [<name>] [-g]                                Pull latest from taps
  promote [<name>]                                  Add tap(s) to registry recommended taps
OPTIONS:
  -g, --global    Operate on the global lorex root (~/.lorex)
  -h, --help      Show this help
EXAMPLES:
  lorex tap add https://github.com/org/skills
  lorex tap add https://github.com/org/skills --name myorg
  lorex tap list
  lorex tap sync
  lorex tap remove myorg
  lorex tap promote myorg
```

---

## Files to change

| File | Change |
|---|---|
| `src/Lorex/Cli/HelpPrinter.cs` | New file |
| `src/Lorex/Program.cs` | Redesign `PrintHelp()` |
| `src/Lorex/Commands/InitCommand.cs` | Replace `PrintHelp()` with `HelpPrinter.Print()` call |
| `src/Lorex/Commands/InstallCommand.cs` | Add `--help` check + `PrintHelp()` |
| `src/Lorex/Commands/UninstallCommand.cs` | Add `--help` check + `PrintHelp()` |
| `src/Lorex/Commands/CreateCommand.cs` | Add `--help` check + `PrintHelp()` |
| `src/Lorex/Commands/ListCommand.cs` | Add `--help` check + `PrintHelp()` |
| `src/Lorex/Commands/StatusCommand.cs` | Add `--help` check + `PrintHelp()` |
| `src/Lorex/Commands/SyncCommand.cs` | Add `--help` check + `PrintHelp()` |
| `src/Lorex/Commands/PublishCommand.cs` | Add `--help` check + `PrintHelp()` |
| `src/Lorex/Commands/RefreshCommand.cs` | Add `--help` check + `PrintHelp()` |
| `src/Lorex/Commands/RegistryCommand.cs` | Replace `PrintHelp()` with `HelpPrinter.Print()` call |
| `src/Lorex/Commands/TapCommand.cs` | Replace `PrintHelp()` with `HelpPrinter.Print()` call |
| `docs/reference/commands.md` | Update to reflect `--help` availability on all commands |

## Tests to add

Add cases to `tests/Lorex.Tests/CommandArgumentTests.cs`:
- `lorex <command> --help` exits with code 0 for every command
- `lorex <command> -h` exits with code 0 for every command
- `lorex tap --help` exits with code 0
