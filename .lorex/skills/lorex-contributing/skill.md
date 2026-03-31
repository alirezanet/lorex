---
name: lorex-contributing
description: Lorex project overview, architecture, and contribution guide for developers working on the lorex codebase.
version: 1.0.0
tags: lorex, contributing, architecture, internals
owner: lorex
---

# lorex-contributing

Use this skill when contributing to lorex itself.

Lorex is a .NET 10 Native AOT CLI that manages two first-class artifact kinds:

- skills in `.lorex/skills`
- prompts in `.lorex/prompts`

The architecture is centered on:

- a canonical lorex artifact store
- symlink-backed registry installs into that store
- adapter-specific derived projections for native agent surfaces
- compatibility with legacy `skill.md` files while scaffolding new `SKILL.md` files

Lorex is a cross-platform tool. Changes in commands, filesystem behavior, tests, and docs should support Windows, Linux, and macOS unless there is a clearly documented platform-specific constraint.

## Repository layout

```text
lorex/
├── src/Lorex/
│   ├── Program.cs
│   ├── Commands/
│   │   ├── InitCommand.cs
│   │   ├── InstallCommand.cs
│   │   ├── UninstallCommand.cs
│   │   ├── ListCommand.cs
│   │   ├── StatusCommand.cs
│   │   ├── SyncCommand.cs
│   │   ├── CreateCommand.cs
│   │   ├── PublishCommand.cs
│   │   ├── RefreshCommand.cs
│   │   ├── ShowCommand.cs
│   │   └── RegistryCommand.cs
│   ├── Cli/
│   │   ├── ServiceFactory.cs
│   │   ├── ArtifactCliSupport.cs
│   │   ├── ArtifactOverwritePrompts.cs
│   │   ├── RegistryCommandSupport.cs
│   │   └── RegistryPolicyPrompts.cs
│   ├── Core/
│   │   ├── Adapters/
│   │   │   ├── AdapterProjection.cs
│   │   │   ├── IAdapter.cs
│   │   │   ├── CopilotAdapter.cs
│   │   │   ├── CodexAdapter.cs
│   │   │   ├── CursorAdapter.cs
│   │   │   ├── ClaudeAdapter.cs
│   │   │   ├── WindsurfAdapter.cs
│   │   │   ├── ClineAdapter.cs
│   │   │   ├── RooAdapter.cs
│   │   │   ├── GeminiAdapter.cs
│   │   │   └── OpenCodeAdapter.cs
│   │   ├── Services/
│   │   │   ├── AdapterService.cs
│   │   │   ├── ArtifactService.cs
│   │   │   ├── ArtifactFileConvention.cs
│   │   │   ├── BuiltInSkillService.cs
│   │   │   ├── RegistryArtifactQueryService.cs
│   │   │   ├── RegistryService.cs
│   │   │   ├── ProjectRootLocator.cs
│   │   │   ├── GitService.cs
│   │   │   └── WindowsDevModeHelper.cs
│   │   ├── Models/
│   │   └── Serialization/
│   └── Resources/
│       └── lorex.md
├── tests/Lorex.Tests/
├── .lorex/
│   ├── skills/
│   │   ├── lorex/
│   │   └── lorex-contributing/
│   └── prompts/
└── README.md
```

## Architecture

### Canonical store

Lorex stores project artifacts in:

```text
.lorex/skills/<skill-name>/SKILL.md
.lorex/prompts/<prompt-name>/PROMPT.md
```

`lorex init` re-discovers existing artifact directories under `.lorex/skills/` and `.lorex/prompts/` and repopulates `.lorex/lorex.json`, so re-initialising an existing repo does not orphan local content.

### Config model

`.lorex/lorex.json` stores:

- optional registry config and cached effective registry policy
- selected adapters
- installed artifacts grouped under:
  - `artifacts.skills`
  - `artifacts.prompts`

No backward-compatibility shim is expected in this codebase. New changes should target the grouped artifact model directly.

### Adapter projections

Adapters declare projections per artifact kind through `IAdapter.GetProjection(projectRoot, kind)`.

Current projection families:

- `SkillDirectoryProjection` for native skill directories
- `CursorRulesProjection` for Cursor rule files derived from skills
- `RooRulesProjection` for Roo rule files derived from skills
- `GeminiContextProjection` for Gemini settings that load lorex skill directories
- `PromptProjection` for generated prompt, command, or workflow files

`AdapterService.Project(...)` is the central entry point. It also removes old lorex-managed blocks from obsolete instruction files such as `AGENTS.md`, `CLAUDE.md`, and `GEMINI.md`.

### Projection behavior

- Registry installs are canonical symlinks into `.lorex/skills` or `.lorex/prompts`
- Native skill adapters create per-skill directory symlinks in the adapter target root
- If a target entry is not a symlink into `.lorex/skills`, Lorex treats it as user-managed and ignores it
- Prompt projections are generated files, not symlinks
- Lorex only rewrites generated prompt files when the existing file is Lorex-managed
- Cursor and Roo receive generated rule files derived from the canonical skill body
- Gemini receives `.gemini/settings.json` updates so it loads lorex skill directories as context, plus generated `.gemini/commands/*.toml` prompt files
- Copilot prompt support also maintains `.vscode/settings.json` with `chat.promptFiles: true`

Adapter projections are derived outputs and are typically gitignored. `.lorex/lorex.json` plus `.lorex/skills/` and `.lorex/prompts/` remain the canonical committed state.

### Artifact lifecycle

| Type | Location | Typical representation |
|---|---|---|
| Registry skill | Registry cache → `.lorex/skills/<name>` | Symlink |
| Registry prompt | Registry cache → `.lorex/prompts/<name>` | Symlink |
| Local skill | `.lorex/skills/<name>/SKILL.md` | Real directory |
| Local prompt | `.lorex/prompts/<name>/PROMPT.md` | Real directory |
| Built-in skill | `src/Lorex/Resources/*.md` → `.lorex/skills/<name>/SKILL.md` | Real directory |

## Supported adapter targets

| Adapter | Skills | Prompts |
|---|---|---|
| `copilot` | `.github/skills/` | `.github/prompts/` and `.vscode/settings.json` |
| `codex` | `.agents/skills/` | unsupported projection; use `lorex show prompt` |
| `cursor` | `.cursor/rules/` | `.cursor/commands/` |
| `claude` | `.claude/skills/` | `.claude/commands/` |
| `windsurf` | `.windsurf/skills/` | `.windsurf/workflows/` |
| `cline` | `.cline/skills/` | `.clinerules/workflows/` |
| `roo` | `.roo/rules-code/` | `.roo/commands/` |
| `gemini` | `.gemini/settings.json` | `.gemini/commands/` |
| `opencode` | `.opencode/skills/` | `.opencode/commands/` |

## Build and run

```bash
dotnet build
dotnet run --project src/Lorex -- <args>
dotnet run install.cs
dotnet test
```

All commands can be run from a nested directory inside the project; `ProjectRootLocator` resolves the nearest ancestor containing `.lorex/lorex.json`.

Native AOT publish profiles remain under `src/Lorex/Properties/PublishProfiles/`.

## Common contribution tasks

### Add a new adapter

1. Add or update the adapter type in `src/Lorex/Core/Adapters/`
2. Register it in `AdapterService.KnownAdapters`
3. Update `src/Lorex/Resources/lorex.md`
4. Update `.lorex/skills/lorex/SKILL.md`
5. Update this file
6. Update `README.md`
7. Update `tests/Lorex.Tests/AdapterServiceTests.cs`

### Change artifact file conventions

1. Update `ArtifactFileConvention.cs`
2. Update `ArtifactService.cs`
3. Update `RegistryService.cs`
4. Update `src/Lorex/Resources/lorex.md`
5. Update `.lorex/skills/lorex/SKILL.md`
6. Update `README.md`

### Change adapter projection behavior

1. Update the relevant adapter type and `AdapterProjection`
2. Update `AdapterService.cs`
3. Update tests in `tests/Lorex.Tests/AdapterServiceTests.cs`
4. Update `src/Lorex/Resources/lorex.md`
5. Update `.lorex/skills/lorex/SKILL.md`
6. Update this file
7. Update `README.md`

## Checklist after changes

If you change any of these areas, update the docs and skills so agents stay accurate:

| Changed area | Files to update |
|---|---|
| User-facing CLI behavior | `README.md`, `src/Lorex/Resources/lorex.md`, `.lorex/skills/lorex/SKILL.md` |
| Adapter paths or projection model | `README.md`, `src/Lorex/Resources/lorex.md`, `.lorex/skills/lorex/SKILL.md`, this file |
| Architecture or repo layout | this file |
| Artifact file format | `README.md`, `src/Lorex/Resources/lorex.md`, `.lorex/skills/lorex/SKILL.md` |

## Pitfalls

- `Spectre.Console` markup uses `[tag]`; escape literal brackets as `[[`.
- Do not hard-code Windows-only or Unix-only path expectations in tests; use `Path.Combine`, `Path.GetFullPath`, and platform-neutral assertions.
- Do not regress the symlink-only behavior for registry installs or native skill directory projections.
- Native skill-folder projections are considered Lorex-managed only when the target entry is a symlink into `.lorex/skills`.
- Prompt projections are Lorex-managed only when they contain the Lorex generation marker.
- Keep `src/Lorex/Resources/lorex.md` and `.lorex/skills/lorex/SKILL.md` identical.
- If a change would make an agent reading only the skill files wrong, update the skill files in the same patch.
