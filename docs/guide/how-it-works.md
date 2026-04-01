# How It Works

## The canonical skill store

Lorex maintains one canonical source of truth for your knowledge inside your project:

```
.lorex/
  lorex.json          ← project config: registry URL, adapters, installed skills
  skills/
    auth-logic/
      SKILL.md
    api-conventions/
      SKILL.md
      examples/
      scripts/
```

`.lorex/skills/<name>/SKILL.md` is the only place you ever author or review skill content. Everything else is derived from it automatically.

---

## Adapter projections

When you run `lorex refresh` (or any command that installs or syncs skills), Lorex reads the list of configured adapters from `lorex.json` and projects your skills into each agent's native location.

### Skill-directory adapters

For agents with native skill folders, Lorex creates **directory symlinks** back to `.lorex/skills`:

```
.claude/skills/auth-logic        →  .lorex/skills/auth-logic
.agents/skills/auth-logic        →  .lorex/skills/auth-logic
.github/skills/auth-logic        →  .lorex/skills/auth-logic
.cline/skills/auth-logic         →  .lorex/skills/auth-logic
.windsurf/skills/auth-logic      →  .lorex/skills/auth-logic
.opencode/skills/auth-logic      →  .lorex/skills/auth-logic
```

Each agent reads the skill directly from the symlink. There is only one copy of the content.

### Rules-file adapters

For agents that use rule files instead of skill folders, Lorex generates the appropriate native file from the canonical skill source:

| Adapter | Output |
| :--- | :--- |
| **Cursor** | `.cursor/rules/lorex-<name>.mdc` — a `description:` front-matter rule file |
| **Roo** | `.roo/rules-code/lorex-<name>.md` — a Code-mode rule file |

### Settings adapters

| Adapter | Output |
| :--- | :--- |
| **Gemini** | `.gemini/settings.json` — `context.includeDirectories` is updated to point at each `.lorex/skills/<name>` directory |

---

## What to commit

Adapter projections are **derived outputs** and should typically be gitignored. The canonical state to commit is:

```
.lorex/lorex.json
.lorex/skills/
```

Registry-backed skills are symlinks inside `.lorex/skills/`, pointing to a local cache of the registry repo. Those symlinks are also committed so your team can see which skills are installed.

---

## Project root discovery

All lorex commands resolve the project root by walking **up** from the current directory to the nearest ancestor that contains `.lorex/lorex.json`. You never need to `cd` to the repo root before running lorex.

---

## Why not RAG?

| | Traditional RAG | Lorex |
| :--- | :--- | :--- |
| **Precision** | Probabilistic (can miss or hallucinate context) | Explicit and human-verified |
| **Versioning** | Hard to track in vector databases | Git-native — PRs, diffs, history |
| **Infrastructure** | Requires a vector DB and embedding pipeline | Zero infra. Just a CLI and files. |
| **Control** | "Black box" retrieval | You decide exactly what the agent knows |
