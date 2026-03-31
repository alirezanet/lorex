# Lorex

### The Shared Knowledge Registry for AI Agents and People

**Stop repeating yourself to AI.** Lorex turns project knowledge into version-controlled reusable artifacts that every supported agent can consume.

[![CI](https://github.com/alirezanet/lorex/actions/workflows/ci.yml/badge.svg)](https://github.com/alirezanet/lorex/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#license)
[![Status](https://img.shields.io/badge/status-early%20beta-orange.svg)](#contributing)
![Status](https://img.shields.io/badge/status-under%20development-orange)

Lorex is under active development. Expect rapid changes, rough edges, and frequent improvements.

## Who Needs Lorex?

- People using multiple AI agents who want one reusable source of truth
- Teams who want to share AI-ready project knowledge without rewriting it per tool
- Developers who want project-specific skills and prompts to be easy to install and reuse
- People who have strong AI sessions but want to turn them into reusable artifacts

## Why Lorex?

- **Works across agents:** author once, project into each agent's native surface
- **Shared intelligence:** use any Git repo as a registry for reusable team artifacts
- **Native AOT:** fast CLI, no runtime or VM
- **Git-native:** diffs, history, reviews, and normal repo workflows

## Get Started

### 1. Install

Windows (PowerShell):

```powershell
irm https://raw.githubusercontent.com/alirezanet/lorex/main/scripts/install.ps1 | iex
```

macOS / Linux:

```bash
curl -fsSL https://raw.githubusercontent.com/alirezanet/lorex/main/scripts/install.sh | sh
```

For .NET developers:

```bash
dotnet tool install -g lorex --prerelease
```

### 2. Initialize a Project

```bash
cd your-project
lorex init
```

`lorex init`:

- detects known adapters
- installs the built-in `lorex` skill
- configures a shared registry or local-only mode
- re-discovers existing local skills and prompts under `.lorex/`
- suggests recommended registry skills for the current repo when available

## Common Flows

### Create a local skill

```bash
lorex create api-conventions -d "API rules and versioning constraints" -t "api,http" -o "platform"
```

Then edit:

```text
.lorex/skills/api-conventions/SKILL.md
```

### Create a local prompt

```bash
lorex create review-pr --type prompt -d "Review a pull request for bugs, regressions, and missing tests" -t "review,qa" -o "platform"
```

Then edit:

```text
.lorex/prompts/review-pr/PROMPT.md
```

### Install from a team registry

```bash
lorex init https://github.com/your-org/ai-artifacts.git
lorex install auth-logic
lorex install --type prompt review-pr
```

### Publish a local artifact

```bash
lorex publish auth-logic
lorex publish --type prompt review-pr
```

### Show a prompt for Codex

Codex prompt projection is intentionally not repo-managed. Use the CLI fallback:

```bash
lorex show prompt review-pr
```

## CLI Overview

| Command | Description |
|---|---|
| `lorex init` | Configure lorex, registry policy, and adapters for this project |
| `lorex create [--type skill|prompt]` | Scaffold a local artifact |
| `lorex install [--type skill|prompt]` | Install registry artifacts |
| `lorex uninstall [--type skill|prompt]` | Remove installed artifacts |
| `lorex list [--type skill|prompt]` | Browse registry artifacts |
| `lorex status [--type skill|prompt]` | Show installed artifacts and adapter targets |
| `lorex sync [--type skill|prompt]` | Refresh installed shared artifacts |
| `lorex publish [--type skill|prompt]` | Publish local artifacts to the registry |
| `lorex show prompt <name>` | Print a canonical prompt for manual use |
| `lorex registry` | Update registry publish policy |
| `lorex refresh [--type skill|prompt]` | Re-project artifacts into agent-native locations |

`--type` defaults to `skill` in non-interactive command usage.

For interactive `create`, `install`, `uninstall`, and `publish`, Lorex asks for the artifact type first when `--type` is omitted.

`status`, `sync`, and `refresh` operate on both skills and prompts by default, with optional `--type` filtering.

## How Projection Works

Lorex keeps one canonical store:

```text
.lorex/
  lorex.json
  skills/
    auth-logic/
      SKILL.md
  prompts/
    review-pr/
      PROMPT.md
```

When you run `lorex refresh`, Lorex projects those artifacts into each agent's native integration surface.

### Skill projections

Skills use native skill directories or skill-derived rules/settings:

- Copilot: `.github/skills/`
- Codex: `.agents/skills/`
- Claude: `.claude/skills/`
- Windsurf: `.windsurf/skills/`
- Cline: `.cline/skills/`
- OpenCode: `.opencode/skills/`
- Cursor: `.cursor/rules/`
- Roo: `.roo/rules-code/`
- Gemini: `.gemini/settings.json` referencing `.lorex/skills/*`

### Prompt projections

Prompts project into native prompt, command, or workflow surfaces:

- Copilot: `.github/prompts/*.prompt.md` plus `.vscode/settings.json` with `chat.promptFiles: true`
- Claude: `.claude/commands/*.md`
- Cursor: `.cursor/commands/*.md`
- Windsurf: `.windsurf/workflows/*.md`
- Cline: `.clinerules/workflows/*.md`
- Roo: `.roo/commands/*.md`
- Gemini: `.gemini/commands/*.toml`
- OpenCode: `.opencode/commands/*.md`
- Codex: no repo projection; use `lorex show prompt`

Registry-installed artifacts are symlinked into `.lorex/skills` or `.lorex/prompts`. Adapter projection outputs are derived files and should usually be gitignored.

## Registry Layout

Lorex registries are plain Git repositories:

```text
.lorex-registry.json
skills/
  auth-logic/
    SKILL.md
prompts/
  review-pr/
    PROMPT.md
```

Registry policy lives in `.lorex-registry.json` and controls how `lorex publish` behaves:

- `direct`
- `pull-request`
- `read-only`

Use `lorex registry` to update that policy.

## Why Not Just RAG?

| Feature | Traditional RAG | Lorex |
|---|---|---|
| Precision | Probabilistic | Explicit and reviewable |
| Versioning | Hard to audit | Git-native |
| Infrastructure | Vector DB and services | Files plus a CLI |
| Control | Retrieval heuristics | Canonical files you own |

## Contributing

```bash
git clone https://github.com/alirezanet/lorex
cd lorex
dotnet ./install.cs
```

This repo dogfoods Lorex. Once cloned, your AI agent can read:

- `lorex`
- `lorex-contributing`

to learn the CLI behavior and internal architecture automatically.

Current roadmap areas include:

- more adapters and richer native projections
- better workflows for extracting reusable artifacts from successful AI sessions
- support for more structured reusable agent assets beyond skills and prompts

## License

MIT
