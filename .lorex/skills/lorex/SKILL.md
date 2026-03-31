---
name: lorex
description: How to use lorex — the AI skill manager CLI — to install, manage, and author skills for AI coding agents.
version: 1.0.0
tags: lorex, ai-agents, skills, cli
owner: lorex
---

# lorex

Lorex is a CLI that manages reusable AI skills and projects them into each agent's native integration surface. The canonical source of truth is `.lorex/skills/<name>/SKILL.md`; adapter outputs are derived from that store.

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
| `init` | `lorex init [<url>] [--local] [--adapters a,b]` | Set up lorex in a project and load or initialize the registry policy |
| `create` | `lorex create [<name>] [-d desc] [-t tags] [-o owner]` | Scaffold a new local skill |
| `install` | `lorex install [<skill>…] [--all]` | Install skills from the registry into this project |
| `uninstall` | `lorex uninstall [<skill>…] [--all]` | Remove installed skills from this project |
| `list` | `lorex list` | Browse skills available in the registry |
| `status` | `lorex status` | Show installed skills, registry state, and adapter targets |
| `sync` | `lorex sync` | Pull the latest versions and registry policy from the registry |
| `publish` | `lorex publish [<skill>…]` | Contribute local skills using the registry's publish policy |
| `registry` | `lorex registry` | Interactively update the connected registry's publish policy |
| `refresh` | `lorex refresh [--target adapter]` | Re-project skills into native agent locations after skill edits |

`list`, `install`, `sync`, `publish`, and `registry` require a registry. `create`, `status`, and `refresh` work in local-only mode.

Running `lorex init` with no arguments opens a guided setup flow:

1. Choose a saved registry, enter a new registry URL, or keep the repo local-only
2. Choose which agent integrations lorex should maintain
3. If the registry has no manifest yet, choose its publish mode so lorex can initialize it

Running `lorex install` with no skill names opens an interactive flow where users can install all available skills or choose a subset. `lorex uninstall` similarly supports `--all` or an interactive flow to remove all installed skills or choose a subset.

Shared registries declare their own contribution policy in `/.lorex-registry.json`:

- `direct`: `lorex publish` commits and pushes straight to the registry
- `pull-request`: `lorex publish` creates a branch, pushes it, and prints a PR URL when possible
- `read-only`: `lorex publish` is blocked

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

## Registry layout

```text
.lorex-registry.json
skills/
  auth-overview/
    SKILL.md
  deployment/
    SKILL.md
    scripts/
```

## Troubleshooting

| Symptom | Fix |
|---|---|
| Skill not appearing in an agent | Run `lorex refresh` |
| `lorex publish` opens a branch instead of pushing directly | The registry policy is `pull-request`; check `lorex status` |
| `lorex publish` is blocked | The registry policy is `read-only`; the registry owner must change `/.lorex-registry.json` |
| `lorex registry` opens a branch instead of changing the policy immediately | The current registry policy is `pull-request`; merge the generated PR branch, then run `lorex sync` |
| Old `AGENTS.md` / `CLAUDE.md` files still exist | Lorex removes its legacy managed block during refresh; delete the file if it is now empty |
| Symlinks not working on Windows | Enable Developer Mode or otherwise allow symlink creation; lorex requires symlinks for registry installs and native skill projections |
| Gemini not loading lorex skills | Confirm `.gemini/settings.json` exists and `context.loadFromIncludeDirectories` is `true` |
| Published skill still shows as local | Run `lorex status`; registry-backed installs should show as `symlink` when available |
