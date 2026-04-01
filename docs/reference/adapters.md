# Adapters

An adapter tells Lorex how to make your skills visible inside a specific AI agent. Lorex supports nine adapters, each projecting skills into the format and location that agent expects.

You choose adapters during `lorex init`. You can add or remove adapters at any time by editing `.lorex/lorex.json` and running `lorex refresh`.

---

## All adapters at a glance

| Key | Agent | Projection type | Native target |
| :--- | :--- | :--- | :--- |
| `claude` | Claude Code | Symlink | `.claude/skills/<name>/` |
| `copilot` | GitHub Copilot | Symlink | `.github/skills/<name>/` |
| `codex` | OpenAI Codex | Symlink | `.agents/skills/<name>/` |
| `windsurf` | Windsurf | Symlink | `.windsurf/skills/<name>/` |
| `cline` | Cline | Symlink | `.cline/skills/<name>/` |
| `opencode` | OpenCode | Symlink | `.opencode/skills/<name>/` |
| `cursor` | Cursor | Generated `.mdc` files | `.cursor/rules/lorex-<name>.mdc` |
| `roo` | Roo Code | Generated `.md` files | `.roo/rules-code/lorex-<name>.md` |
| `gemini` | Gemini CLI | Settings update | `.gemini/settings.json` |

---

## Symlink adapters

`claude`, `copilot`, `codex`, `windsurf`, `cline`, `opencode`

For these agents, Lorex creates a **directory symlink** for each installed skill:

```
.claude/skills/auth-logic    →   .lorex/skills/auth-logic
.claude/skills/checkout-flow →   .lorex/skills/checkout-flow
```

The agent reads the skill by following the symlink into `.lorex/skills/`. There is only one copy of the content.

**Lorex only manages symlinks** — any directory inside the agent's skill folder that is not a symlink pointing into `.lorex/skills/` is treated as user-managed and is never touched. This means you can have non-lorex skills in `.claude/skills/` and Lorex will leave them alone.

When you uninstall a skill or change adapters, Lorex removes the symlinks it created, not any other directories.

---

## Cursor

The `cursor` adapter generates a `.mdc` rule file for each installed skill at:

```
.cursor/rules/lorex-<name>.mdc
```

The file follows Cursor's rule format:

```yaml
---
description: "Authentication flows, token validation, and session rules"
alwaysApply: false
---

# auth-logic

...skill body from SKILL.md...
```

The `description` field is read from the skill's YAML frontmatter. When Cursor loads rules, it uses the description to decide which rule applies to the current context.

Lorex prefixes all generated rule files with `lorex-` and removes any `lorex-*.mdc` files for skills that are no longer installed. Files that don't start with `lorex-` are left untouched.

---

## Roo Code

The `roo` adapter generates a `.md` rule file for each installed skill at:

```
.roo/rules-code/lorex-<name>.md
```

The generated file includes a header that explains when the rule applies, followed by the skill body:

```markdown
# auth-logic

Use this lorex skill when the task matches: Authentication flows, token validation, and session rules

...skill body...
```

Same as Cursor — only files prefixed with `lorex-` are managed; others are left alone.

---

## Gemini CLI

The `gemini` adapter does not create files — it updates `.gemini/settings.json` to tell the Gemini CLI where to find lorex skills.

Lorex adds each installed skill's directory to `context.includeDirectories` and sets `loadFromIncludeDirectories: true`:

```json
{
  "context": {
    "fileName": ["SKILL.md", "skill.md"],
    "includeDirectories": [
      ".lorex/skills/auth-logic",
      ".lorex/skills/checkout-flow"
    ],
    "loadFromIncludeDirectories": true
  }
}
```

Lorex preserves any other settings already in the file. It only replaces the `fileName`, `includeDirectories`, and `loadFromIncludeDirectories` fields inside `context`.

When you uninstall a skill, its path is removed from `includeDirectories` on the next `lorex refresh`.

---

## How Lorex detects adapters

During `lorex init`, Lorex scans the project and marks adapters as `(detected)` if it finds existing configuration files:

| Adapter | Detection signal |
| :--- | :--- |
| `claude` | `.claude/skills/` exists or `CLAUDE.md` is present |
| `copilot` | `.github/skills/` exists or `.github/copilot-instructions.md` is present |
| `codex` | `.agents/skills/` exists or `AGENTS.md` is present |
| `cursor` | `.cursor/rules/` exists or `.cursorrules` is present |
| `windsurf` | `.windsurf/skills/` exists or `.windsurfrules` is present |
| `cline` | `.cline/skills/` exists or `.clinerules` is present |
| `roo` | `.roo/rules-code/` exists or `.roorules` is present |
| `gemini` | `.gemini/settings.json` exists or `GEMINI.md` is present |
| `opencode` | `.opencode/skills/` exists or `opencode.md` is present |

Detection is just for the `init` prompt — you can enable any adapter regardless of whether it was detected.

---

## Adding or removing an adapter

**Via `lorex init`** (re-run with new adapter list):

```bash
lorex init --local --adapters claude,copilot,cursor
```

**By editing `lorex.json` directly:**

Open `.lorex/lorex.json` and update the `adapters` array:

```json
{
  "adapters": ["claude", "copilot", "cursor"],
  ...
}
```

Then run:

```bash
lorex refresh
```

Lorex will create projections for newly added adapters and clean up projections it created for removed ones.

---

## Gitignore recommendations

Adapter projections are derived outputs. Most teams gitignore them so the repo only contains the canonical skill source:

```gitignore
# Lorex adapter projections
.claude/skills/
.agents/skills/
.github/skills/
.cline/skills/
.windsurf/skills/
.opencode/skills/
.cursor/rules/lorex-*.mdc
.roo/rules-code/lorex-*.md
```

After a fresh clone, a teammate runs `lorex init` and Lorex re-creates all projections for their machine.

---

## Requiring symlink support

All symlink-based adapters — and registry installs — require symlink creation to work. On Linux and macOS this works out of the box. On Windows, enable [Developer Mode](https://learn.microsoft.com/en-us/windows/apps/get-started/enable-your-device-for-development).

If Lorex cannot create a symlink it prints an error and, on Windows, offers to open the Developer Mode settings page.
