---
name: lorex
description: How to use lorex — the AI artifact manager CLI — to install, manage, and author reusable skills and prompts for AI coding agents.
version: 1.0.0
tags: lorex, ai-agents, skills, prompts, cli
owner: lorex
---

# lorex

Lorex is a CLI that manages reusable AI artifacts and projects them into each agent's native integration surface. The canonical source of truth is:

- `.lorex/skills/<name>/SKILL.md`
- `.lorex/prompts/<name>/PROMPT.md`

Adapter outputs are derived from those canonical stores.

> You are an AI agent reading this skill. Use it when the user asks about lorex usage, installed artifacts, adapter behavior, or how to create and share skills and prompts.

## When a user asks what is available

1. Check `lorex status`
2. Check `.lorex/lorex.json`
3. List `.lorex/skills/` and `.lorex/prompts/`
4. Read any installed `SKILL.md` or `PROMPT.md` files the user asks about

Lorex commands resolve the project root by walking up from the current working directory to the nearest ancestor containing `.lorex/lorex.json`. Users do not need to run lorex from the repo root.

## Commands

| Command | Syntax | When to use |
|---|---|---|
| `init` | `lorex init [<url>] [--local] [--adapters a,b]` | Set up lorex in a project and load or initialize the registry policy |
| `create` | `lorex create [<name>] [-d desc] [-t tags] [-o owner] [--type skill|prompt]` | Scaffold a new local artifact |
| `install` | `lorex install [<artifact>…] [--all] [--recommended] [--type skill|prompt]` | Install registry artifacts into this project |
| `uninstall` | `lorex uninstall [<artifact>…] [--all] [--type skill|prompt]` | Remove installed artifacts from this project |
| `list` | `lorex list [--type skill|prompt]` | Browse registry artifacts of one kind |
| `status` | `lorex status [--type skill|prompt]` | Show installed artifacts, registry state, and adapter targets |
| `sync` | `lorex sync [--type skill|prompt]` | Pull the latest shared artifacts and registry policy from the registry |
| `publish` | `lorex publish [<artifact>…] [--type skill|prompt]` | Contribute local artifacts using the registry's publish policy |
| `show` | `lorex show prompt <name>` | Print a canonical prompt for manual use on adapters without prompt projection |
| `registry` | `lorex registry` | Interactively update the connected registry's publish policy |
| `refresh` | `lorex refresh [--target adapter] [--type skill|prompt]` | Re-project lorex artifacts into native agent locations after edits |

`list`, `install`, `sync`, `publish`, and `registry` require a registry. `create`, `status`, `show`, and `refresh` work in local-only mode.

### `--type` behavior

- `--type` defaults to `skill` in non-interactive command usage
- interactive `create`, `install`, `uninstall`, and `publish` ask which artifact type to use when `--type` is omitted
- `status`, `sync`, and `refresh` operate on both kinds by default and accept `--type` as an optional filter

## Canonical project layout

```text
.lorex/
  lorex.json
  skills/
    <skill-name>/
      SKILL.md
      scripts/
      docs/
  prompts/
    <prompt-name>/
      PROMPT.md
```

`.lorex/lorex.json` stores:

- selected adapters
- optional registry config and effective registry policy
- installed artifacts grouped under `artifacts.skills` and `artifacts.prompts`

Registry-backed artifacts are symlinked into `.lorex/skills` or `.lorex/prompts`. Lorex requires symlink support for registry installs and native skill-folder projections.

## Artifact formats

Lorex skills and prompts use the same frontmatter fields:

```markdown
---
name: my-artifact
description: One sentence used by lorex and adapters
version: 1.0.0
tags: topic, subtopic
owner: team-or-person
---
```

Canonical files:

- skill: `.lorex/skills/<name>/SKILL.md`
- prompt: `.lorex/prompts/<name>/PROMPT.md`

Lorex still reads legacy `skill.md` files for compatibility. Prompts use `PROMPT.md` only.

## Adapter projections

Lorex keeps the canonical artifact store in `.lorex/` and projects derived files into agent-native locations.

| Adapter | Skills | Prompts |
|---|---|---|
| `copilot` | `.github/skills/` symlink directories | `.github/prompts/*.prompt.md` and `.vscode/settings.json` with `chat.promptFiles: true` |
| `codex` | `.agents/skills/` symlink directories | no native projection; use `lorex show prompt <name>` |
| `cursor` | `.cursor/rules/lorex-*.mdc` generated rules | `.cursor/commands/*.md` |
| `claude` | `.claude/skills/` symlink directories | `.claude/commands/*.md` |
| `windsurf` | `.windsurf/skills/` symlink directories | `.windsurf/workflows/*.md` |
| `cline` | `.cline/skills/` symlink directories | `.clinerules/workflows/*.md` |
| `roo` | `.roo/rules-code/lorex-*.md` generated rules | `.roo/commands/*.md` |
| `gemini` | `.gemini/settings.json` plus `.lorex/skills/*` as context directories | `.gemini/commands/*.toml` |
| `opencode` | `.opencode/skills/` symlink directories | `.opencode/commands/*.md` |

For skills, adapters either use native skill directories or generated rule/settings files. For prompts, adapters receive generated native prompt/command/workflow files when a repo-local prompt surface exists.

The agent-specific projection folders are derived outputs. The canonical state to commit is `.lorex/lorex.json` plus `.lorex/skills/` and `.lorex/prompts/`; generated adapter folders should usually be gitignored.

## Common flows

### Create a skill

```sh
lorex create auth-overview -d "Authentication flows and constraints" -t "auth,security" -o "platform"
```

Then edit `.lorex/skills/auth-overview/SKILL.md`.

### Create a prompt

```sh
lorex create review-pr --type prompt -d "Review a pull request for bugs and regressions" -t "review,qa" -o "platform"
```

Then edit `.lorex/prompts/review-pr/PROMPT.md`.

### Install from a registry

```sh
lorex install auth-overview
lorex install --type prompt review-pr
```

### Browse registry artifacts

```sh
lorex list
lorex list --type prompt
```

### Refresh projections

```sh
lorex refresh
lorex refresh --type prompt
lorex refresh --target claude
```

### Show a prompt for Codex or another unsupported adapter

```sh
lorex show prompt review-pr
```

## Registry layout

```text
.lorex-registry.json
skills/
  auth-overview/
    SKILL.md
prompts/
  review-pr/
    PROMPT.md
```

Shared registries declare their own contribution policy in `/.lorex-registry.json`:

- `direct`: `lorex publish` commits and pushes straight to the registry
- `pull-request`: `lorex publish` creates a branch, pushes it, and prints a PR URL when possible
- `read-only`: `lorex publish` is blocked

## Troubleshooting

| Symptom | Fix |
|---|---|
| Artifact not appearing in an agent | Run `lorex refresh` |
| Prompt not showing up in Copilot | Confirm `.github/prompts/` exists and `.vscode/settings.json` contains `"chat.promptFiles": true` |
| Gemini not loading lorex skills | Confirm `.gemini/settings.json` exists and `context.loadFromIncludeDirectories` is `true` |
| Codex has no repo prompt file | Use `lorex show prompt <name>` |
| `lorex publish` opens a branch instead of pushing directly | The registry policy is `pull-request`; check `lorex status` |
| `lorex publish` is blocked | The registry policy is `read-only`; the registry owner must change `/.lorex-registry.json` |
| Old `AGENTS.md` / `CLAUDE.md` / `GEMINI.md` files still exist | Lorex removes its legacy managed block during refresh; delete the file if it is now empty |
| Symlinks not working on Windows | Enable Developer Mode or otherwise allow symlink creation; lorex requires symlinks for registry installs and native skill projections |
