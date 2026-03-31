---
name: lorex
description: How to use lorex — the AI skill manager CLI — to install, manage, and author skills for AI coding agents.
version: 1.0.0
tags: lorex, ai-agents, skills, cli
owner: lorex
---

# lorex-usage

Lorex is a CLI tool that installs reusable knowledge "skills" into AI agent config files (GitHub Copilot, Cursor, Codex, etc.). Skills are markdown files with YAML frontmatter stored in a git registry. When installed, lorex injects a skill index into the agent's instruction file so the agent knows what to load.

## Core Workflow

```sh
lorex init                                                # interactive; press Enter to skip registry (local-only)
lorex init https://github.com/org/registry.git            # connect registry, select adapters
lorex init --local                                        # skip registry entirely
lorex init https://github.com/org/registry.git --adapters copilot,codex
lorex list                                                # browse skills in the registry
lorex install                                             # choose one or more registry skills interactively
lorex install <skill>                                     # install a specific registry skill
lorex status                                              # see installed skills and their state
```

## All Commands

| Command | Syntax | Description |
|---|---|---|
| `init` | `lorex init [<url>] [--local] [--adapters a,b]` | Connect registry (or `--local` to skip); select agent config targets |
| `install` | `lorex install [<skill>...]` | Install one or more skills; prompts with a multi-select picker if omitted |
| `uninstall` | `lorex uninstall <skill>` | Remove an installed skill |
| `list` | `lorex list` | List all skills in the registry |
| `status` | `lorex status` | Show installed skills and symlink/copy state |
| `sync` | `lorex sync` | Pull latest skill versions from the registry |
| `create` | `lorex create [<name>] [-d desc] [-t tags] [-o owner]` | Scaffold a new skill for AI/manual authoring |
| `publish` | `lorex publish [<skill>...]` | Push one or more local skills to the registry |
| `refresh` | `lorex refresh [--target adapter]` | Re-inject skill index without a network call |

`list`, `install`, `sync`, and `publish` require a registry and will exit with a clear message if none is configured. `create`, `status`, and `refresh` work in both modes. The legacy `lorex generate` command name may still exist as a compatibility alias, but `create` is the canonical name.

## Supported Adapters

| Key | File written |
|---|---|
| `copilot` | `.github/copilot-instructions.md` |
| `codex` | `AGENTS.md` |
| `openclaw` | `AGENTS.md` |
| `cursor` | `.cursorrules` |
| `claude` | `CLAUDE.md` |
| `windsurf` | `.windsurfrules` |
| `cline` | `.clinerules` |
| `roo` | `.roorules` |
| `gemini` | `GEMINI.md` |
| `opencode` | `opencode.md` |

## Skill File Format

A skill is a markdown file (`skill.md`) with YAML frontmatter:

```markdown
---
name: my-skill
description: One sentence — shown in `lorex list` and the agent index.
version: 1.0.0
tags: topic, subtopic
owner: your-name-or-team
---

# My Skill

Markdown content here. Write whatever the agent needs to know.
Use headers, code blocks, bullet lists, tables.
```

## Creating New Skills For The User

Preferred workflow: if the user asks you to create a lorex skill for the current repo, based on the current project, or based on the current session, you can author it directly. The user does not need to run `lorex create` first.

### Direct AI-authored workflow

1. Ensure the project is initialised with lorex. If `.lorex/lorex.json` does not exist, run `lorex init` first.
2. Choose a kebab-case skill name.
3. Create `.lorex/skills/<skill-name>/skill.md` using the exact frontmatter format shown above.
4. Add `<skill-name>` to `.lorex/lorex.json` under `installedSkills` if it is not already present.
5. Run `lorex refresh` so the updated skill index is injected into the configured agent files.
6. If the user wants to share the skill to a registry later, run `lorex publish <skill-name>`.

### Optional scaffolded workflow

If you want lorex to prepare the folder and starter file first, use:

```sh
lorex create <skill-name> -d "Short description"
```

This is a convenience helper, not the only way to create a skill.

### Example user request

The user can say:

> Create a lorex skill called `contributing` for this project based on the current repo. Cover architecture, setup, and contribution rules.

In that case, create the skill directly in `.lorex/skills/contributing/skill.md`, update `.lorex/lorex.json` if needed, and run `lorex refresh`.

## Registry Layout

A lorex registry is any git repo structured as:

```
skills/
  my-skill/
    skill.md           ← required; YAML frontmatter + content
    helper.sh          ← optional embedded tools alongside skill.md
  another-skill/
    skill.md
```

## Symlinks vs Copies

On Linux, macOS, and Windows with Developer Mode enabled, lorex creates symlinks so `lorex sync` updates skills automatically. Without symlinks (Windows without Developer Mode), files are copied and must be reinstalled after syncing.

Enable Windows Developer Mode: Settings → System → For developers → Developer Mode.
