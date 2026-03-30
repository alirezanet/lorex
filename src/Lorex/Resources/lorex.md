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
```

## All Commands

| Command | Syntax | Description |
|---|---|---|
| `init` | `lorex init [<url>] [--local] [--adapters a,b]` | Connect registry (or `--local` to skip); select agent config targets |
| `install` | `lorex install <skill>` | Install a skill from the registry |
| `uninstall` | `lorex uninstall <skill>` | Remove an installed skill |
| `list` | `lorex list` | List all skills in the registry |
| `status` | `lorex status` | Show installed skills and symlink/copy state |
| `sync` | `lorex sync` | Pull latest skill versions from the registry |
| `generate` | `lorex generate [<name>] [-d desc] [-t tags] [-o owner]` | Scaffold a new skill for authoring |
| `publish` | `lorex publish [<skill>]` | Push a staged skill to the registry |
| `refresh` | `lorex refresh [--target adapter]` | Re-inject skill index without a network call |

`list`, `install`, `sync`, and `publish` require a registry and will exit with a clear message if none is configured. `generate`, `status`, and `refresh` work in both modes.

## Supported Adapters

| Key | File written |
|---|---|
| `copilot` | `.github/copilot-instructions.md` |
| `codex` | `AGENTS.md` |
| `cursor` | `.cursorrules` |

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

## Authoring a Skill — How to Help the User

When the user asks you (the AI agent) to create a lorex skill based on the current session or conversation, follow these steps:

1. **Understand the scope.** Ask the user: "What should this skill be named and what topic should it cover?" if not already clear.

2. **Write the complete `skill.md`** using the exact format above. Good skill content:
   - Is dense and scannable — written as reference docs, not conversational prose
   - Covers core concepts, key conventions, concrete commands, and examples
   - Notes gotchas, non-obvious behaviours, and decisions made
   - Avoids anything local/machine-specific
   - Uses markdown formatting (headers, code blocks, tables, bullets)

3. **Output only the file contents** — no explanation, no surrounding code fences.

4. **Tell the user to save it:**
   ```
   Save to: .lorex/staging/<skill-name>/skill.md
   Then run: lorex publish <skill-name>
   ```

### Example — asking for a skill at the end of a session

The user can simply say: *"Create a lorex skill called `auth-overview` covering what we built today."*

You already know the skill format from this lorex-usage skill. Write the `skill.md` directly — no special command needed.

## Symlinks vs Copies

On Linux, macOS, and Windows with Developer Mode enabled, lorex creates symlinks so `lorex sync` updates skills automatically. Without symlinks (Windows without Developer Mode), files are copied and must be reinstalled after syncing.

Enable Windows Developer Mode: Settings → System → For developers → Developer Mode.
