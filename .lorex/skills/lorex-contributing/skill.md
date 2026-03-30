---
name: lorex-contributing
description: Lorex project overview, architecture, and contribution guide for developers working on the lorex codebase.
version: 1.0.0
tags: lorex, contributing, architecture, internals
owner: lorex
---

# lorex-contributing

This skill is for any AI agent (or human) working **on** the lorex codebase itself — fixing bugs, adding features, or reviewing architecture. The companion `lorex` skill covers how to _use_ lorex.

> **IMPORTANT — Keep skill files in sync.**
> Whenever you make changes to lorex, you are responsible for updating the relevant skill files. See the [Skill File Update Checklist](#skill-file-update-checklist) at the bottom of this file.

---

## Project Overview

Lorex is a **native AOT CLI tool** written in C#/.NET 10. It is distributed as a single self-contained binary with no runtime dependency. It lets developers package engineering knowledge as versioned markdown "skills" and inject them into AI agent config files.

- **Version:** 0.0.1 (early beta)
- **Target framework:** `net10.0`
- **Key dependency:** `Spectre.Console` (core only, no `.Cli` package — dispatch is manual)
- **Tool packaging:** `PackAsTool=true`, command name `lorex`
- **AOT:** `InvariantGlobalization=true`, `EnableTrimAnalyzer=true`; all native publish profiles live in `src/Lorex/Properties/PublishProfiles/`

---

## Repository Layout

```
lorex/
├── src/
│   └── Lorex/
│       ├── Program.cs                     ← entry point; manual switch dispatch
│       ├── Lorex.csproj
│       ├── Commands/                      ← one file per CLI command
│       │   ├── InitCommand.cs
│       │   ├── InstallCommand.cs
│       │   ├── UninstallCommand.cs
│       │   ├── ListCommand.cs
│       │   ├── StatusCommand.cs
│       │   ├── SyncCommand.cs
│       │   ├── GenerateCommand.cs
│       │   ├── PublishCommand.cs
│       │   ├── RefreshCommand.cs
│       │   └── ServiceFactory.cs          ← singleton service locator
│       ├── Core/
│       │   ├── Adapters/                  ← one file per AI tool adapter
│       │   │   ├── IAdapter.cs
│       │   │   ├── CopilotAdapter.cs
│       │   │   ├── CodexAdapter.cs
│       │   │   ├── OpenClawAdapter.cs
│       │   │   ├── CursorAdapter.cs
│       │   │   ├── ClaudeAdapter.cs
│       │   │   ├── WindsurfAdapter.cs
│       │   │   ├── ClineAdapter.cs
│       │   │   ├── RooAdapter.cs
│       │   │   ├── GeminiAdapter.cs
│       │   │   └── OpenCodeAdapter.cs
│       │   ├── Services/
│       │   │   ├── AdapterService.cs      ← KnownAdapters registry; auto-detect
│       │   │   ├── SkillService.cs        ← install/uninstall/scaffold/publish/sync
│       │   │   ├── BuiltInSkillService.cs ← reads embedded resources; auto-install on init
│       │   │   ├── RegistryService.cs     ← git clone/pull of registry to ~/.lorex/cache/
│       │   │   ├── GitService.cs          ← thin Process wrapper around git
│       │   │   └── WindowsDevModeHelper.cs
│       │   ├── Models/                    ← LorexConfig, GlobalConfig, SkillMeta
│       │   └── Serialization/             ← LorexJsonContext (AOT-safe source-gen JSON)
│       └── Resources/
│           └── lorex.md                   ← EmbeddedResource; auto-installed on lorex init
├── tests/
│   └── Lorex.Tests/                       ← xUnit unit tests
├── .lorex/
│   └── skills/                            ← locally-managed skills (committed to repo)
│       ├── lorex/                         ← built-in usage skill (auto-managed by lorex)
│       └── lorex-contributing/            ← this file
├── install.cs                             ← dev installer (dotnet script)
├── lorex.slnx
└── README.md
```

---

## Architecture — Key Concepts

### Dispatch
`Program.cs` uses a `switch(args[0])` to route to static `Run(string[] rest)` methods on each command class. There is no command framework. Help text and argument parsing are handwritten.

### Services
`ServiceFactory` is a lazy singleton locator. Commands call `ServiceFactory.Skills`, `ServiceFactory.Registry`, etc. Services are constructed once per process.

### Adapters
Each adapter implements `IAdapter`:
- `Key` — the string used in `lorex init` (e.g., `"copilot"`)
- `ConfigFile` — relative path to the file written (e.g., `.github/copilot-instructions.md`)
- `InjectIndex(string projectRoot, string index)` — writes or updates the `<!-- lorex:start -->…<!-- lorex:end -->` block
- `RemoveIndex(string projectRoot)` — strips the block
- `IsPresent(string projectRoot)` — returns true if the config file already exists (used for auto-detect on init)

All known adapters are registered in `AdapterService.KnownAdapters` (a `Dictionary<string, IAdapter>`).

### Skill Lifecycle
1. **Registry skills** — cloned to `~/.lorex/cache/<registry-hash>/skills/<name>/`; installed as a directory symlink at `.lorex/skills/<name>` → cache path.
2. **Local skills** — scaffolded by `lorex generate` directly into `.lorex/skills/<name>/`; real directory (not a symlink); publishable via `lorex publish`.
3. **Built-in skills** — embedded as `EmbeddedResource` in the binary (`Resources/*.md`); extracted to `.lorex/skills/<name>/` on `lorex init`; not publishable.

`SkillService.LocalOnlySkills()` returns skills that are real directories and not built-in — i.e., candidates for `lorex publish`.

### Config
`.lorex/lorex.json` — project-level config:
```json
{ "registry": "https://…", "adapters": ["copilot"], "installedSkills": ["lorex"] }
```
Skills must be in `installedSkills` to appear in the injected index. `ScaffoldSkill` adds the name automatically. For manually created skill folders, add the name here and run `lorex refresh`.

### Skill Index Injection
Each adapter's `InjectIndex` writes a fenced block into its config file:
```
<!-- lorex:start -->
## Lorex Skill Index
- **skill-name**: description → `.lorex/skills/skill-name/skill.md`
<!-- lorex:end -->
```
The agent reads this block and knows which skill files to load on demand.

---

## Build & Run

```bash
# Build
dotnet build

# Run from source
dotnet run --project src/Lorex -- <args>

# Dev install (builds -dev nupkg + installs as global tool)
dotnet run install.cs

# Run tests
dotnet test

# Native AOT publish (requires platform matching the profile)
dotnet publish src/Lorex /p:PublishProfile=win-x64   -c Release
dotnet publish src/Lorex /p:PublishProfile=linux-x64 -c Release
dotnet publish src/Lorex /p:PublishProfile=osx-arm64 -c Release
```

The dev installer (`install.cs`) uninstalls any existing `lorex` global tool, bumps the version with a `-dev` suffix, packs, and re-installs. It requires .NET 10 SDK.

---

## Common Contribution Tasks

### Adding a new adapter

1. Create `src/Lorex/Core/Adapters/<Name>Adapter.cs` implementing `IAdapter`.
2. Register it in `AdapterService.KnownAdapters` dictionary.
3. ✏️ Update `src/Lorex/Resources/lorex.md` — add the key and file to the Supported Adapters table.
4. ✏️ Update `.lorex/skills/lorex/skill.md` — same table.
5. ✏️ Update `README.md` — Supported AI Tools table.
6. ✏️ Update this file (`.lorex/skills/lorex-contributing/skill.md`) if the architecture changed.

### Adding a new command

1. Create `src/Lorex/Commands/<Name>Command.cs` with a static `Run(string[] args)` method.
2. Add a case to the `switch` in `Program.cs`.
3. Add a row to the `PrintHelp()` grid in `Program.cs`.
4. ✏️ Update `src/Lorex/Resources/lorex.md` — All Commands table.
5. ✏️ Update `.lorex/skills/lorex/skill.md` — All Commands table.
6. ✏️ Update `README.md` — How It Works command list.

### Changing the skill file format (frontmatter fields, layout conventions)

1. ✏️ Update `src/Lorex/Resources/lorex.md` — Skill File Format section.
2. ✏️ Update `.lorex/skills/lorex/skill.md` — Skill File Format section.
3. ✏️ Update `README.md` — Skill Format section.
4. Consider updating `ScaffoldSkill` in `SkillService.cs` to reflect the new template.

### Changing the skill index format (the injected block)

1. Update the relevant adapter(s) in `Core/Adapters/`.
2. ✏️ Update `src/Lorex/Resources/lorex.md`.
3. ✏️ Update `.lorex/skills/lorex/skill.md`.

---

## Skill File Update Checklist

When you finish a change, ask yourself:

| Changed area | Files to update |
|---|---|
| New or changed CLI command | `lorex.md`, `lorex/skill.md`, `README.md` |
| New or changed adapter | `lorex.md`, `lorex/skill.md`, `README.md`, this file |
| Skill format / frontmatter | `lorex.md`, `lorex/skill.md`, `README.md` |
| Build / install workflow | This file (`lorex-contributing/skill.md`), `README.md` |
| Project structure / architecture | This file (`lorex-contributing/skill.md`) |
| Any of the above | Run `lorex refresh` to re-inject the updated index |

> **Rule of thumb:** if an AI agent reading only the skill files would now have wrong or missing information, the skill files need updating. Treat `lorex.md` (the embedded resource) and `.lorex/skills/lorex/skill.md` as a pair — they should always be identical.

---

## Notes & Pitfalls

- `Spectre.Console` markup uses `[tag]` syntax. To output a literal bracket include it as `[[`. Skill content injected into markup must be escaped with `Markup.Escape()` or wrapped in `[[ ]]`.
- The `lorex` built-in skill lives in two places: `src/Lorex/Resources/lorex.md` (source, compiled into binary) and `.lorex/skills/lorex/skill.md` (installed copy). Keep them in sync manually — there is currently no automation for this.
- AOT compatibility: no `dynamic`, no `System.Reflection.Emit`, no unregistered `System.Text.Json` types. Add new model types to `LorexJsonContext`.
- **`LorexConfig.Registry`** is `string?` — null means local-only mode.
- `InstallCommand`, `SyncCommand`, `ListCommand`, `PublishCommand` guard against null registry and print a helpful message.
- `SkillService.InstallSkill`, `SyncSkills`, `PublishSkill` throw `InvalidOperationException` if registry is null (commands guard before reaching them).
- `StatusCommand` shows `(none — local-only mode)` when registry is null.
- `InitCommand` accepts `--local` flag (non-interactive skip) or empty Enter press (interactive skip).
- `git ls-remote -- <url>` validates a registry URL (works on empty repos; avoid `--exit-code` or `--heads` flags which behave differently across git versions).
- `install.cs` uses the `dotnet` C# scripting runner (`#!/usr/bin/env dotnet`). It uninstalls before installing to work around `dotnet tool update` no-op on same version.
