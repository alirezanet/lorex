---
name: lorex-contributing
description: Required for any contribution to the lorex codebase. Architecture, services, adapters, config files, and mandatory doc-update rules.
version: 1.0.0
tags: lorex, contributing, architecture, internals
owner: lorex
---

# lorex-contributing

Use this skill when contributing to lorex itself.

Full documentation: https://alirezanet.github.io/lorex/

Lorex is a .NET 10 Native AOT CLI that stores canonical skills in `.lorex/skills` and projects them into native agent integrations. The current architecture is centered on:

- a canonical lorex skill store
- symlink-only projection into native agent paths
- compatibility with legacy `skill.md` files while scaffolding new `SKILL.md` files

Lorex is a cross-platform tool. Changes in commands, filesystem behavior, tests, and docs should support Windows, Linux, and macOS unless there is a clearly documented platform-specific constraint.

## Repository layout

```text
lorex/
├── install.cs                          ← dev installer: packs & installs lorex as a global .NET tool
├── src/Lorex/
│   ├── Program.cs                      ← CLI entry point, command dispatch, --help / --version
│   ├── Commands/
│   │   ├── InitCommand.cs
│   │   ├── InstallCommand.cs
│   │   ├── UninstallCommand.cs
│   │   ├── ListCommand.cs
│   │   ├── StatusCommand.cs
│   │   ├── SyncCommand.cs
│   │   ├── CreateCommand.cs            ← also aliased as "generate"
│   │   ├── PublishCommand.cs
│   │   ├── RefreshCommand.cs
│   │   ├── RegistryCommand.cs
│   │   └── TapCommand.cs
│   ├── Cli/
│   │   ├── ServiceFactory.cs
│   │   ├── ArgParser.cs                ← flag-value parsing utilities (FlagValue, IntFlagValue)
│   │   ├── HelpPrinter.cs              ← consistent USAGE/DESCRIPTION/OPTIONS/EXAMPLES rendering for all commands
│   │   ├── RegistryCommandSupport.cs
│   │   ├── RegistryPolicyPrompts.cs
│   │   ├── SkillOverwritePrompts.cs
│   │   ├── SkillBrowserTui.cs          ← read-only TUI browser for lorex list (search, paging, arrow keys)
│   │   └── SkillPickerTui.cs           ← multi-select TUI for install/uninstall/publish
│   ├── Core/
│   │   ├── Adapters/
│   │   │   ├── AdapterProjection.cs
│   │   │   ├── IAdapter.cs
│   │   │   ├── CopilotAdapter.cs       ← .github/skills/
│   │   │   ├── CodexAdapter.cs         ← .agents/skills/
│   │   │   ├── CursorAdapter.cs        ← .cursor/rules/
│   │   │   ├── ClaudeAdapter.cs        ← .claude/skills/
│   │   │   ├── WindsurfAdapter.cs      ← .windsurf/skills/
│   │   │   ├── ClineAdapter.cs         ← .cline/skills/
│   │   │   ├── RooAdapter.cs           ← .roo/rules-code/
│   │   │   ├── GeminiAdapter.cs        ← .gemini/settings.json
│   │   │   └── OpenCodeAdapter.cs      ← .opencode/skills/
│   │   ├── Services/
│   │   │   ├── AdapterService.cs           ← native projection engine
│   │   │   ├── SkillService.cs             ← install / uninstall / sync / scaffold / publish
│   │   │   ├── SkillFileConvention.cs      ← canonical and legacy skill entry helpers
│   │   │   ├── BuiltInSkillService.cs      ← seeds built-in skills from embedded Resources/
│   │   │   ├── RegistryService.cs          ← registry cache, skill discovery (flat + nested layouts)
│   │   │   ├── RegistrySkillQueryService.cs← filters available/recommended skills for a project
│   │   │   ├── TapService.cs               ← tap clone cache and skill discovery
│   │   │   ├── ProjectRootLocator.cs       ← resolves nearest lorex project root from any subdirectory
│   │   │   ├── GlobalRootLocator.cs        ← resolves ~/.lorex for --global operations
│   │   │   ├── GitService.cs               ← git operations; accepts local paths and file:// URIs
│   │   │   └── WindowsDevModeHelper.cs     ← checks/enables Windows Developer Mode for symlinks
│   │   ├── Models/
│   │   │   ├── LorexConfig.cs          ← .lorex/lorex.json: registry, adapters, installedSkills
│   │   │   ├── GlobalConfig.cs         ← ~/.lorex/config.json: MRU registry list
│   │   │   ├── RegistryConfig.cs       ← registry URL + cached policy
│   │   │   ├── RegistryPolicy.cs
│   │   │   ├── RegistryPublishModes.cs
│   │   │   ├── RegistryPolicyUpdateResult.cs
│   │   │   ├── SkillMetadata.cs        ← YAML frontmatter model (name, description, version, tags, owner)
│   │   │   └── PublishResult.cs
│   │   └── Serialization/
│   │       ├── LorexJsonContext.cs     ← AOT-safe JSON serialization context
│   │       └── SimpleYamlParser.cs     ← minimal YAML frontmatter parser for SKILL.md files
│   └── Resources/
│       └── lorex.md                    ← embedded built-in lorex skill (must stay identical to .lorex/skills/lorex/SKILL.md)
├── tests/Lorex.Tests/
│   ├── Integration/
│   │   ├── LorexTestHarness.cs         ← IDisposable sandbox: isolated temp dirs, local git registries
│   │   ├── IntegrationCollection.cs    ← xUnit collection: DisableParallelization (shared AnsiConsole)
│   │   ├── LocalOnlyFlowTests.cs       ← init, create, status, refresh — no registry
│   │   ├── RegistryFlowTests.cs        ← init with registry, install, sync, publish
│   │   ├── TapFlowTests.cs             ← tap add / list / sync, install from tap
│   │   └── GlobalFlowTests.cs          ← --global flag flows, project/global independence
│   ├── AdapterServiceTests.cs
│   ├── SkillServiceTests.cs
│   ├── RegistryServiceTests.cs
│   ├── ProjectRootLocatorTests.cs
│   ├── GlobalRootLocatorTests.cs
│   ├── CommandArgumentTests.cs
│   ├── SimpleYamlParserTests.cs
│   └── SymlinkTargetTests.cs
├── docs/                               ← VitePress site (https://alirezanet.github.io/lorex/)
│   ├── guide/
│   ├── reference/
│   └── contributing.md
├── .lorex/skills/
│   ├── lorex/SKILL.md
│   └── lorex-contributing/SKILL.md
└── README.md
```

## Architecture

### Canonical store

Lorex stores project skills in:

```text
.lorex/skills/<skill-name>/SKILL.md
```

Lorex still reads legacy `skill.md` files for compatibility, but new skills are scaffolded as `SKILL.md`.

`lorex init` re-discovers existing skill directories under `.lorex/skills/` and repopulates `installedSkills` so re-initialising an existing repo does not lose local skill activation.

### Adapter projections

Adapters no longer inject a lorex index into main instruction files. Instead, each adapter declares a native projection surface:

- `SkillDirectoryProjection` for agents with native skill directories
- `CursorRulesProjection` for Cursor rule files
- `RooRulesProjection` for Roo Code mode rules
- `GeminiContextProjection` for Gemini project settings that point at lorex skill directories

`AdapterService.Project(...)` is the central entry point. It also removes old lorex-managed blocks from obsolete instruction files such as `AGENTS.md`, `CLAUDE.md`, and `GEMINI.md`.

### Projection behavior

- Native skill adapters create per-skill directory symlinks in the target root.
- If a target entry is not a symlink into `.lorex/skills`, Lorex treats it as user-managed and ignores it.
- Lorex requires symlink support for registry installs and skill-folder projections.
- Cursor and Roo receive generated rule files derived from the canonical lorex skill body.
- Gemini receives `.gemini/settings.json` updates so it loads lorex skill directories as context.

Adapter projections are derived outputs and are typically gitignored. `.lorex/lorex.json` plus `.lorex/skills/` remain the canonical committed state.

### Global skills

Commands that accept `--global` use `GlobalRootLocator` to resolve `~/.lorex/` as the project-root equivalent. Skills are installed to `~/.lorex/skills/` and projected into user-level agent paths (`~/.claude/skills/`, etc.). `GlobalRootLocator.ResolveForExistingGlobal()` throws `FileNotFoundException` if `~/.lorex/lorex.json` does not exist, so commands should surface the "run `lorex init --global` first" message to the user.

### Nested registry layout

`RegistryService` supports both flat (`skills/name/`) and nested (`skills/category/name/`) registry layouts. A directory is considered a skill if it contains `SKILL.md`, `skill.md`, or `metadata.yaml`; directories without these files are treated as category folders and traversed recursively. `BuildSkillPathIndex` builds a name→path map in a single pass for batch operations. Skill names are deduplicated — first-found wins when two paths share the same leaf name.

### Configuration files

| File | Type | Purpose |
|---|---|---|
| `.lorex/lorex.json` | `LorexConfig` | Project-level state: registry URL + policy, enabled adapters, installed skill names |
| `~/.lorex/lorex.json` | `LorexConfig` | Global equivalent; used when `--global` is passed |
| `~/.lorex/config.json` | `GlobalConfig` | Machine-level MRU registry list (populated by `lorex init`) |

`LorexConfig` is the source of truth for what lorex owns in a given project. Any command that modifies installed skills or adapter state reads and writes this file.

### RegistrySkillQueryService

Sits above `RegistryService` and `GitService` to answer project-specific questions:

- **`ListAvailableSkills`** — returns all skills in the registry not already installed
- **`GetRecommendedSkillNames`** — filters available skills whose `tags` intersect the project's identity keys (derived from git remote slug and folder name)

This service drives the `--recommended` flag on `lorex install`. When writing tests for recommendation logic, use `RegistryServiceTests.cs` for raw discovery and `SkillServiceTests.cs` for install-path integration.

### Skill lifecycle

| Type | Location | Typical representation |
|---|---|---|
| Registry skill | Registry cache → `.lorex/skills/<name>` | Symlink |
| Local skill | `.lorex/skills/<name>/SKILL.md` | Real directory |
| Built-in skill | `src/Lorex/Resources/*.md` → `.lorex/skills/<name>/SKILL.md` | Real directory |

## Build and run

```bash
dotnet build
dotnet run --project src/Lorex -- <args>   # run directly from source
dotnet run install.cs                       # pack + install as global .NET tool (adds -dev version suffix)
dotnet test
```

`install.cs` is the development installer. It packs the project to a local `nupkg/` directory with a `-dev` version suffix and installs it as a global `dotnet tool`, so `lorex` is available on PATH for manual testing. It never conflicts with a release build. Run it from the repo root after significant changes.

All commands can be run from a nested directory inside the project; `ProjectRootLocator` resolves the nearest ancestor containing `.lorex/lorex.json`. Commands invoked with `--global` bypass project-root discovery and use `GlobalRootLocator` to resolve `~/.lorex/` instead.

Native AOT publish profiles remain under `src/Lorex/Properties/PublishProfiles/`.

## Mandatory documentation rules

**These rules are non-negotiable. Do not mark any task complete unless all applicable rules below are satisfied.**

1. **`src/Lorex/Resources/lorex.md` and `.lorex/skills/lorex/SKILL.md` must always be identical.** If you change one, change the other in the same commit. No exceptions.
2. **If your change affects architecture, repo layout, contribution workflow, or the lorex-contributing skill content itself, you must update `.lorex/skills/lorex-contributing/SKILL.md` (this file) in the same commit.**
3. **If your change affects any user-facing CLI behavior, adapter paths, skill format, or registry behavior, you must update BOTH the relevant `docs/` page(s) AND `src/Lorex/Resources/lorex.md` / `.lorex/skills/lorex/SKILL.md` in the same commit.** These are two separate obligations — updating docs/ without also updating the skill files (or vice versa) is an incomplete contribution. See the [docs/ update rules](#docs-update-rules) table and the [Checklist after changes](#checklist-after-changes) table for the full list of files per area.
4. **Never finish a contribution without verifying that every agent reading only the skill files will still get an accurate picture.** If the code changed but the skill files did not, the contribution is incomplete.
5. **Always run `dotnet build` and `dotnet test` after making code changes and before considering a task done.** A contribution with a build error or a failing test is never complete, regardless of how correct the logic appears.

## Common contribution tasks

### Add a new adapter

1. Add or update the adapter type in `src/Lorex/Core/Adapters/`
2. Register it in `AdapterService.KnownAdapters`
3. Update `src/Lorex/Resources/lorex.md`
4. Update `.lorex/skills/lorex/SKILL.md`
5. Update this file
6. Update `README.md`

### Change skill file conventions

1. Update `SkillFileConvention.cs`
2. Update `SkillService.cs`
3. Update `RegistryService.cs`
4. Update `src/Lorex/Resources/lorex.md`
5. Update `.lorex/skills/lorex/SKILL.md`
6. Update `README.md`

### Add a new command

1. Add `<Name>Command.cs` in `src/Lorex/Commands/`
2. Register the command string in the `switch` in `Program.cs`
3. Register any new services in `Cli/ServiceFactory.cs`
4. Add argument/integration tests in `tests/Lorex.Tests/CommandArgumentTests.cs`
5. Update `docs/reference/commands.md`
6. Update `src/Lorex/Resources/lorex.md`
7. Update `.lorex/skills/lorex/SKILL.md`

### Change adapter projection behavior

1. Update the relevant adapter type and `AdapterProjection`
2. Update `AdapterService.cs`
3. Update tests in `tests/Lorex.Tests/AdapterServiceTests.cs`
4. Update `src/Lorex/Resources/lorex.md`
5. Update `.lorex/skills/lorex/SKILL.md`
6. Update this file
7. Update `README.md`

## SKILL.md frontmatter fields

Every skill file starts with YAML frontmatter. These are the supported fields (all parsed by `SimpleYamlParser`):

| Field | Required | Example |
|---|---|---|
| `name` | yes | `my-skill` (kebab-case) |
| `description` | yes | one-line summary shown in `lorex list` |
| `version` | no (default `1.0.0`) | `1.2.0` |
| `tags` | no | `dotnet, auth` (comma-separated) |
| `owner` | no | team or individual name |

Tags are used by `RegistrySkillQueryService` to match skills to projects via `--recommended`. Use meaningful, specific values. See `docs/reference/skill-format.md` for full spec.

## Checklist after changes

If you change any of these areas, update the docs, skills, and tests so agents and humans stay accurate:

| Changed area | Files to update |
|---|---|
| User-facing CLI behavior | `README.md`, `docs/reference/commands.md`, `src/Lorex/Resources/lorex.md`, `.lorex/skills/lorex/SKILL.md` |
| Adapter paths or projection model | `README.md`, `docs/reference/adapters.md`, `src/Lorex/Resources/lorex.md`, `.lorex/skills/lorex/SKILL.md`, this file |
| Architecture or repo layout | `docs/guide/how-it-works.md`, this file |
| Skill file format / frontmatter | `README.md`, `docs/reference/skill-format.md`, `src/Lorex/Resources/lorex.md`, `.lorex/skills/lorex/SKILL.md` |
| Global skills (`--global` flag) | `docs/reference/commands.md`, `src/Lorex/Resources/lorex.md`, `.lorex/skills/lorex/SKILL.md`, this file |
| Registry skill discovery (nested layout) | `docs/reference/commands.md`, `src/Lorex/Resources/lorex.md`, `.lorex/skills/lorex/SKILL.md`, this file |
| Configuration file structure (`LorexConfig`, `GlobalConfig`) | `docs/guide/how-it-works.md`, this file |
| Skill recommendation logic (`RegistrySkillQueryService`) | `docs/reference/commands.md`, `tests/Lorex.Tests/RegistryServiceTests.cs` |

### docs/ update rules

The `docs/` directory is a VitePress site published at https://alirezanet.github.io/lorex/. It is the primary reference for users. **Never finish a contribution that changes user-facing behavior without updating the relevant docs page.**

| docs/ page | Update when… |
|---|---|
| `docs/guide/getting-started.md` | `lorex init` flow, installation steps, or first-run behavior changes |
| `docs/guide/concepts.md` | The mental model for skills, registries, adapters, or projections changes |
| `docs/guide/how-it-works.md` | Projection mechanics, canonical store layout, or commit guidance changes |
| `docs/guide/skills.md` | `lorex create`, `lorex uninstall`, skill lifecycle, or authoring guidance changes |
| `docs/guide/team-registry.md` | `lorex publish`, `lorex install`, `lorex sync`, registry setup, or onboarding changes |
| `docs/reference/commands.md` | Any command's flags, behavior, output, or error messages change |
| `docs/reference/adapters.md` | An adapter is added, removed, or its projection behavior changes |
| `docs/reference/skill-format.md` | Frontmatter fields or SKILL.md conventions change |
| `docs/reference/registry-policy.md` | Registry policy modes or `.lorex-registry.json` format changes |
| `docs/reference/troubleshooting.md` | A new common failure mode is identified |

## Pitfalls

- `Spectre.Console` markup uses `[tag]`; escape literal brackets as `[[`. Do not use `[` or `]` in strings passed to `AddChoices`, `SelectionPrompt`, or `MultiSelectionPrompt` — they will be parsed as markup and cause a crash.
- Integration tests set the `LOREX_HOME_OVERRIDE` environment variable to redirect all lorex home operations (`~/.lorex/cache`, `~/.lorex/taps`, `~/.lorex/config.json`) to a temp directory. Tests are serialized via `[Collection("Integration")]` + `DisableParallelization = true` because `AnsiConsole` is a static singleton. Never make integration tests parallel.
- Do not hard-code Windows-only or Unix-only path expectations in tests; use `Path.Combine`, `Path.GetFullPath`, and platform-neutral assertions.
- Do not regress the symlink-only behavior when editing projection code.
- Native skill-folder projections are considered Lorex-managed only when the target entry is a symlink into `.lorex/skills`.
- `SimpleYamlParser` is a minimal parser — it does not support multi-line values, anchors, or complex YAML. Keep frontmatter simple.
- `LorexJsonContext` is an AOT-safe source-generated JSON context. Adding new serialized types requires registering them in `LorexJsonContext.cs`.
- **Source-generated JSON deserializers do not guarantee that `init`-property defaults are applied for fields absent from the JSON** — they can arrive as `null` even when the model declares `= []`. `SkillService.ReadConfig` normalises all collection properties after deserialization; if you add new non-nullable collection fields to `LorexConfig` or `GlobalConfig`, add a corresponding null-coalescing line there.
- `lorex create` and `lorex generate` are aliases — both route to `CreateCommand`. Keep both aliases in sync in `Program.cs` if the command is renamed.
- When testing recommendation logic, remember that `RegistrySkillQueryService` normalises tags to lowercase and trims whitespace. Test data should match this contract.
