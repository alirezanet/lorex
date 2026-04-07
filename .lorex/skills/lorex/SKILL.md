---
name: lorex
description: How to use lorex — the AI skill manager CLI — to install, manage, and author skills for AI coding agents.
version: 1.0.0
tags: lorex, ai-agents, skills, cli
owner: lorex
---

# lorex

Lorex is a CLI that manages reusable AI skills and projects them into each agent's native integration surface. The canonical source of truth is `.lorex/skills/<name>/SKILL.md`; adapter outputs are derived from that store.

Full documentation: https://alirezanet.github.io/Lorex/

> You are an AI agent reading this skill. Use it when the user asks about lorex usage, installed skills, adapter behavior, or how to create and share skills.

## When a user asks what skills are available

1. Check `lorex status`
2. Check `.lorex/lorex.json`
3. List `.lorex/skills/`
4. Read any installed `SKILL.md` files the user asks about

Lorex commands resolve the project root by walking up from the current working directory to the nearest ancestor containing `.lorex/lorex.json`. Users do not need to run lorex from the repo root.

## Commands

| Command | Syntax | When to use |
|---|---|---|
| `init` | `lorex init [<url>] [--local] [--global] [--adapters a,b]` | Set up lorex in a project (or globally with `--global`) and load or initialize the registry policy |
| `create` | `lorex create [<name>] [-d desc] [-t tags] [-o owner]` | Scaffold a new local skill |
| `install` | `lorex install [<skill\|url>…] [--all] [--recommended] [--search <text>] [--tag <tag>] [-g\|--global]` | Install skills from the registry, a tap, or directly from a URL |
| `uninstall` | `lorex uninstall [<skill>…] [--all] [-g\|--global]` | Remove installed skills from this project or globally |
| `list` | `lorex list [--search <text>] [--tag <tag>] [--page <n>] [--page-size <n>] [-g\|--global]` | Browse and filter skills available in the registry and all taps |
| `status` | `lorex status [-g\|--global]` | Show installed skills, registry state, and adapter targets |
| `sync` | `lorex sync [-g\|--global]` | Pull the latest versions from the registry and all taps. Skills deleted from the registry are removed automatically. |
| `publish` | `lorex publish [<skill>…]` | Contribute local skills using the registry's publish policy |
| `registry` | `lorex registry` | Interactively update the connected registry's publish policy |
| `refresh` | `lorex refresh [--target adapter]` | Re-project skills into native agent locations after skill edits |
| `tap add` | `lorex tap add <url> [--name <name>] [--root <path>] [-g\|--global]` | Add a read-only skill source (any git repo) |
| `tap remove` | `lorex tap remove <name> [-g\|--global]` | Remove a tap |
| `tap list` | `lorex tap list [-g\|--global]` | List configured taps with skill counts |
| `tap sync` | `lorex tap sync [<name>] [-g\|--global]` | Pull the latest content from all taps or a specific one |

`list`, `install`, `sync`, `publish`, and `registry` require a registry or at least one tap. `create`, `status`, and `refresh` work in local-only mode. Tap commands work independently of the primary registry.

Running `lorex init` with no arguments opens a guided setup flow:

1. Choose a saved registry, enter a new registry URL, or keep the repo local-only
2. Choose which agent integrations lorex should maintain
3. If the registry has no manifest yet, choose its publish mode so lorex can initialize it
4. If the registry defines `recommendedTaps`, offer to add them (user must accept — never added silently)

When a connected registry already has skills that this project does not have installed, `lorex init` finishes by pointing users to `lorex install --recommended` or `lorex list`, and reminds them to use `lorex sync` later to refresh installed shared skills.

Running `lorex install` with no skill names opens an interactive flow where users can install recommended skills, install everything, or choose a subset. If "Choose specific" is selected, lorex opens a full-screen TUI with live typing-to-filter, paging, and multi-select (Space to toggle, Enter to confirm). Skills from taps are shown with a `(tapname)` badge. Use `--search <text>` or `--tag <tag>` to pre-populate the filter. You can also pass a URL directly: `lorex install https://github.com/owner/repo/tree/main/skill-name` installs a skill from any git repository without adding a tap. Recommendations are based on exact tag matches against the current repo slug like `owner/repo`, or the folder name if no git slug is available. `lorex uninstall` similarly supports `--all` or an interactive flow.

`lorex list` opens an interactive TUI browser when run in a terminal (live search, paging with PgUp/PgDn, status icons). Pass `--page`/`--page-size` or pipe output to get the classic paginated table. Supports `--search <text>`, `--tag <tag>`, `--page <n>`, and `--page-size <n>` (default 25; use 0 for all).

## Taps

Taps are read-only skill sources — any git repository containing skills. Unlike the primary registry, taps have no publish policy; they are always read-only from lorex's perspective.

```sh
# Add a tap (clones and validates at least one skill is found)
lorex tap add https://github.com/dotnet/skills

# With an explicit name and optional root subdirectory
lorex tap add https://github.com/dotnet/skills --name dotnet --root skills/

# Add a tap to the global config (available as a source across all projects)
lorex tap add https://github.com/dotnet/skills --global

# Skills from taps appear in lorex list and lorex install alongside registry skills
lorex list
lorex install

# Keep taps up to date
lorex tap sync            # sync all taps
lorex tap sync dotnet     # sync one tap
lorex sync                # also syncs all taps alongside the primary registry

# Remove a tap (local cache is kept for other projects)
lorex tap remove dotnet

# Manage global taps
lorex tap list --global
lorex tap sync --global
```

Tap caches live at `~/.lorex/taps/<slug>/` and are shared across all projects on the machine. Skills installed from taps are symlinked (like registry skills) so `lorex sync` keeps them current automatically.

If a registry install or sync would replace an existing local skill directory in `.lorex/skills`, lorex asks for explicit approval per skill before overwriting it.

Shared registries declare their own contribution policy in `/.lorex-registry.json`:

- `direct`: `lorex publish` commits and pushes straight to the registry
- `pull-request`: `lorex publish` creates a branch, pushes it, and prints a PR URL when possible
- `read-only`: `lorex publish` is blocked

A registry can also declare `recommendedTaps` — read-only skill sources it suggests to all connected projects. Lorex surfaces these during `lorex init` (user must accept) and notifies on `lorex sync` when new ones appear. Taps are never added silently.

Run `lorex registry` to change that policy interactively. If the registry currently uses `direct`, lorex updates `/.lorex-registry.json` immediately. If it currently uses `pull-request`, lorex prepares a review branch and leaves the project on the existing policy until that PR is merged and `lorex sync` is run.

## Supported adapters

| Key | Native target maintained by lorex |
|---|---|
| `copilot` | `.github/skills/` |
| `codex` | `.agents/skills/` |
| `cursor` | `.cursor/rules/` |
| `claude` | `.claude/skills/` |
| `windsurf` | `.windsurf/skills/` |
| `cline` | `.cline/skills/` |
| `roo` | `.roo/rules-code/` |
| `gemini` | `.gemini/settings.json` plus `.lorex/skills/*` as context directories |
| `opencode` | `.opencode/skills/` |

## How to create a skill

Always start with `lorex create`. It creates the folder, writes the frontmatter template, registers the skill in `.lorex/lorex.json`, and refreshes adapter projections.

```sh
lorex create <skill-name> -d "One-line description" -t "tag1,tag2" -o "owner"

# Then edit:
# .lorex/skills/<skill-name>/SKILL.md
```

Only run `lorex refresh` manually after editing an existing skill.

`lorex init` also re-discovers any skills already present under `.lorex/skills/` and adds them back to `installedSkills`, so re-initialising a project does not orphan existing local skills.

## Skill format

A lorex skill lives at `.lorex/skills/<name>/SKILL.md`.

```markdown
---
name: my-skill
description: One sentence used by agents to decide when this skill matters
version: 1.0.0
tags: topic, subtopic
owner: team-or-person
---

# My Skill

Free-form markdown instructions.
```

Field notes:

- `name` and `description` are required
- `description` is used by agent-native skill systems and Lorex-generated rule wrappers
- `version` is used by `lorex sync`
- Lorex still reads legacy `skill.md` files for compatibility
- A skill directory can include scripts, templates, examples, or other supporting files

## Project layout

```text
.lorex/
  lorex.json
  skills/
    <skill-name>/
      SKILL.md
      scripts/
      docs/
```

Registry-backed skills are symlinked into `.lorex/skills`. Lorex requires symlink support for registry installs and native skill projections.

The agent-specific projection folders are derived outputs. The canonical state to commit is `.lorex/lorex.json` plus `.lorex/skills/`; generated adapter folders should usually be gitignored.

When a project is connected to a registry, `.lorex/lorex.json` caches the registry URL plus the effective registry policy. The registry remains the policy owner.

## Global Skills

The `--global` flag installs and syncs skills without being inside a project. Skills land in `~/.lorex/skills/` and are projected into user-level agent directories (`~/.claude/skills/`, `~/.gemini/settings.json`, etc.), making them available in every project on the machine.

```sh
# One-time setup — run from any directory
lorex init --global https://github.com/your-org/ai-skills.git --adapters claude,gemini

# Install and keep up to date
lorex install --all --global
lorex sync --global
```

Project skills and global skills are independent. A project can have its own `lorex init` alongside globally installed skills.

## Registry layout

Registries support flat and nested skill layouts. Skills are discovered by leaf directory name regardless of nesting depth.

```text
.lorex-registry.json
skills/
  auth-overview/        ← flat (traditional)
    SKILL.md
  security/             ← category folder (no SKILL.md)
    auth-logic/
      SKILL.md
    rbac/
      SKILL.md
  deployment/
    SKILL.md
    scripts/
```

After install, the project layout is always flat (`.lorex/skills/auth-logic/`, etc.). Skill names must be unique across the registry tree — if two nested paths share the same leaf name, the first one found wins.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Skill not appearing in an agent | Run `lorex refresh` |
| `lorex publish` opens a branch instead of pushing directly | The registry policy is `pull-request`; check `lorex status` |
| `lorex publish` is blocked | The registry policy is `read-only`; the registry owner must change `/.lorex-registry.json` |
| `lorex registry` opens a branch instead of changing the policy immediately | The current registry policy is `pull-request`; merge the generated PR branch, then run `lorex sync` |
| Old `AGENTS.md` / `CLAUDE.md` files still exist | Lorex removes its legacy managed block during refresh; delete the file if it is now empty |
| `lorex install --global` fails with "not initialised" | Run `lorex init --global` first |
| Symlinks not working on Windows | Enable Developer Mode or otherwise allow symlink creation; lorex requires symlinks for registry installs and native skill projections |
| Gemini not loading lorex skills | Confirm `.gemini/settings.json` exists and `context.loadFromIncludeDirectories` is `true` |
| Published skill still shows as local | Run `lorex status`; registry-backed installs should show as `symlink` when available |
