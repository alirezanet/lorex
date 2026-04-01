---
name: lorex-contributing
description: Lorex project overview, architecture, and contribution guide for developers working on the lorex codebase.
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
│   │   └── RegistryCommand.cs
│   ├── Cli/
│   │   ├── ServiceFactory.cs
│   │   ├── RegistryCommandSupport.cs
│   │   ├── RegistryPolicyPrompts.cs
│   │   └── SkillOverwritePrompts.cs
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
│   │   │   ├── AdapterService.cs       ← native projection engine
│   │   │   ├── SkillService.cs         ← install / uninstall / sync / scaffold / publish
│   │   │   ├── SkillFileConvention.cs  ← canonical and legacy skill entry helpers
│   │   │   ├── BuiltInSkillService.cs
│   │   │   ├── RegistryService.cs
│   │   │   ├── ProjectRootLocator.cs   ← resolves nearest lorex project root from any subdirectory
│   │   │   ├── GitService.cs
│   │   │   └── WindowsDevModeHelper.cs
│   │   ├── Models/
│   │   └── Serialization/
│   └── Resources/
│       └── lorex.md                    ← embedded built-in lorex skill
├── tests/Lorex.Tests/
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

### Skill lifecycle

| Type | Location | Typical representation |
|---|---|---|
| Registry skill | Registry cache → `.lorex/skills/<name>` | Symlink |
| Local skill | `.lorex/skills/<name>/SKILL.md` | Real directory |
| Built-in skill | `src/Lorex/Resources/*.md` → `.lorex/skills/<name>/SKILL.md` | Real directory |

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

### Change skill file conventions

1. Update `SkillFileConvention.cs`
2. Update `SkillService.cs`
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
| User-facing CLI behavior | `README.md`, `docs/`, `src/Lorex/Resources/lorex.md`, `.lorex/skills/lorex/SKILL.md` |
| Adapter paths or projection model | `README.md`, `docs/`, `src/Lorex/Resources/lorex.md`, `.lorex/skills/lorex/SKILL.md`, this file |
| Architecture or repo layout | `docs/`, this file |
| Skill file format | `README.md`, `docs/`, `src/Lorex/Resources/lorex.md`, `.lorex/skills/lorex/SKILL.md` |

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

- `Spectre.Console` markup uses `[tag]`; escape literal brackets as `[[`.
- Do not hard-code Windows-only or Unix-only path expectations in tests; use `Path.Combine`, `Path.GetFullPath`, and platform-neutral assertions.
- Do not regress the symlink-only behavior when editing projection code.
- Native skill-folder projections are considered Lorex-managed only when the target entry is a symlink into `.lorex/skills`.
- Keep `src/Lorex/Resources/lorex.md` and `.lorex/skills/lorex/SKILL.md` identical.
- If a change would make an agent reading only the skill files wrong, update the skill files in the same patch.
- Never complete a task that changes user-facing behavior without updating the corresponding `docs/` page in the same commit.
