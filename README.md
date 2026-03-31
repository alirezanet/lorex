<div align="center">

# lorex

### Teach your AI agents once. Reuse everywhere.

Turn architecture notes, runbooks, conventions, and project knowledge into Git-native skills that work across Codex, Copilot, Cursor, Claude, and more.

[![CI](https://github.com/alirezanet/lorex/actions/workflows/ci.yml/badge.svg)](https://github.com/alirezanet/lorex/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-blue)](#install)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#license)
[![Status](https://img.shields.io/badge/status-early%20beta-orange.svg)](#help-shape-lorex)

</div>

lorex is a native AOT CLI for making AI sessions repo-aware on day one.

Instead of copying the same context into `AGENTS.md`, `.cursorrules`, `CLAUDE.md`, `.github/copilot-instructions.md`, and every other tool-specific instruction file, you keep that knowledge as skills in Git. lorex injects a small skill index into the config files your agents already read, so the right context is visible when work starts.

Start with one repo in local-only mode. Grow into a shared team registry later. Same workflow, same skill format, no SaaS, no vendor lock-in.

## Why Lorex

Lorex has a simple goal:
- When someone clones a repo, their AI agent should not start from zero.
- Help people create new skills with their favorite AI agent.
- Make great skills easy to share across repos, teammates, and teams.

## Install

### Native binary

Download the latest binary from [GitHub Releases](https://github.com/alirezanet/lorex/releases) and add it to your `PATH`.

| Platform | Artifact |
|---|---|
| Windows x64 | `lorex-win-x64.exe` |
| Windows ARM64 | `lorex-win-arm64.exe` |
| Linux x64 | `lorex-linux-x64` |
| Linux ARM64 | `lorex-linux-arm64` |
| macOS Intel | `lorex-osx-x64` |
| macOS Apple Silicon | `lorex-osx-arm64` |

### .NET global tool

If you prefer the `dotnet` tool distribution:

```bash
dotnet tool install -g lorex
```

This path requires the .NET 10 SDK.

### Build from source

If you want to build the current source and install a local development version:

```bash
git clone https://github.com/alirezanet/lorex
cd lorex
dotnet install.cs
```

This builds a `-dev` package from source and installs it as a global tool.

## Quick Start

The easiest path is interactive:

```bash
cd your-project
lorex init
```

`lorex init` asks for a registry URL (or lets you skip into local-only mode), detects existing agent config files, and injects the first skill index block.

`lorex init` also installs the built-in `lorex` skill, so your AI agent already knows the lorex skill format and where local skills belong.

## Let Your AI Create The Skill

Once a project is initialized, you usually do not need to manually scaffold anything.

```bash
cd your-project
lorex init --local --adapters codex,copilot
```

Then ask your AI agent to create the skill for you:

> Create a lorex skill called `contributing` for this project. Analyze the repo and write a skill that explains the architecture, local setup, workflows, and contribution rules.

Or after a work session:

> Create a lorex skill called `payments-overview` based on what we built today. Capture the flow, key files, constraints, and pitfalls.

Or for a reusable team skill:

> Create a lorex skill called `api-conventions` from this discussion. Make it reusable across services and keep it concise and scannable.

Because the built-in `lorex` skill is already installed, the agent knows:

- where the skill should live
- what `skill.md` should look like
- how to register it in `.lorex/lorex.json`
- when to run `lorex refresh`

If you want a starter file first, `lorex create` is still available, but it is optional.

## A Few Common Ways To Start

These are examples, not limits. You can mix repo-local skills and shared registry skills in the same project.

### Example: Repo-local skills

Use this when the knowledge belongs to the repository itself, such as architecture notes, contribution guidance, or service-specific constraints.

```bash
lorex init --local --adapters codex,copilot
```

This pattern is ideal for skills like `contributing`, `architecture`, `deployment-notes`, or any other knowledge that should live and evolve with the repo itself.

If you prefer an explicit scaffold first, use:

```bash
lorex create contributing -d "Project architecture and contribution guide"
```

Commit `.lorex/`, and every clone of the repo gets the same AI context.

### Example: Shared team registry

Use this when the same knowledge should travel across projects, such as company standards, security rules, platform runbooks, or domain overviews.

```bash
lorex init https://github.com/your-org/ai-skills.git --adapters codex,cursor,claude
lorex list
lorex install
lorex sync
```

Your registry is just a Git repository you own. lorex caches it locally and installs skills as symlinks when possible, with copy fallback when symlinks are unavailable.

In practice, many teams will use both:

- repo-local skills for project-specific knowledge
- shared registry skills for team standards, architecture patterns, and runbooks

## How It Works

1. `lorex init` stores project config in `.lorex/lorex.json` and chooses which agent config files to manage.
2. You either ask your AI agent to create a local skill directly, use `lorex create` to scaffold one, or install one from a registry with `lorex install`.
3. lorex injects a skill index into the instruction files your AI tools already use.
4. The agent reads that index and loads the skill files relevant to the current task.

The injected block looks like this:

```md
<!-- lorex:start -->
## Lorex Skill Index

Read this index and load the skill files relevant to your current task.

- **auth-overview**: Authentication service overview, supported flows, and pitfalls -> `.lorex/skills/auth-overview/skill.md`
- **deploy-runbook**: Release process, rollback steps, and environment notes -> `.lorex/skills/deploy-runbook/skill.md`
<!-- lorex:end -->
```

If a skill includes helper scripts or files next to `skill.md`, lorex can surface those too.

## What Lorex Adds To A Repo

```text
.lorex/
  lorex.json            # registry, adapters, installed skills
  skills/
    lorex/
      skill.md
    auth-overview/
      skill.md
      check-tokens.sh

AGENTS.md               # lorex injects the skill index here for Codex / OpenClaw
CLAUDE.md               # same idea for Claude
.cursorrules            # same idea for Cursor
```

`lorex.json` keeps the project wiring small and explicit: which registry is connected, which adapters are enabled, and which skills are installed.

## This Repo Uses Lorex

This repository dogfoods lorex.

Clone the repo and your agent can already load:

- `lorex`: how to use the tool
- `lorex-contributing`: the project architecture and contribution workflow

That makes lorex a good example of the core promise: a repo can teach its own agent how the project works.

## Supported AI Tools

| Tool | Adapter key | Target file |
|---|---|---|
| GitHub Copilot | `copilot` | `.github/copilot-instructions.md` |
| Codex | `codex` | `AGENTS.md` |
| OpenClaw | `openclaw` | `AGENTS.md` |
| Cursor | `cursor` | `.cursorrules` |
| Claude | `claude` | `CLAUDE.md` |
| Windsurf | `windsurf` | `.windsurfrules` |
| Cline | `cline` | `.clinerules` |
| Roo | `roo` | `.roorules` |
| Gemini | `gemini` | `GEMINI.md` |
| OpenCode | `opencode` | `opencode.md` |

`lorex init` auto-detects existing config files and preselects helpful defaults if nothing is found.

## Commands

| Command | Description |
|---|---|
| `lorex init [<url>] [--local] [--adapters a,b]` | Set up the project, connect a registry or stay local-only, and inject the first index |
| `lorex list` | List skills available in the connected registry |
| `lorex install [<skill>...]` | Install one or more skills, or open an interactive multi-select picker |
| `lorex uninstall <skill>` | Remove an installed skill from the current project |
| `lorex status` | Show the registry, enabled adapters, and installed skill state |
| `lorex sync` | Pull the latest registry content and refresh installed skills |
| `lorex create [<name>] [-d desc] [-t tags] [-o owner]` | Optionally scaffold a new local skill in `.lorex/skills/` |
| `lorex publish [<skill>...]` | Publish one or more locally authored skills to the registry |
| `lorex refresh [--target adapter]` | Re-inject the skill index without fetching from the registry |

`list`, `install`, `sync`, and `publish` require a registry. The rest work in local-only mode.

Because the built-in `lorex` skill is installed during `lorex init`, you can usually just ask your AI agent to create a skill directly. `lorex create` is a convenience helper, not the only authoring path.

## Skill Format

A skill lives in its own folder and must contain a `skill.md` file. Anything else in the folder can travel with the skill.

```text
.lorex/skills/
  auth-overview/
    skill.md
    check-tokens.sh
    schema.json
```

`skill.md` uses YAML frontmatter plus free-form markdown:

```md
---
name: auth-overview
description: Authentication service overview, supported flows, and pitfalls
version: 1.0.0
tags: auth, security, backend
owner: platform-team
---

# Auth Overview

Document the architecture, invariants, common flows, failure modes, and the commands or tools an agent should use.
```

Recommended frontmatter fields:

| Field | Purpose |
|---|---|
| `name` | Stable skill identifier |
| `description` | One-line summary shown in `lorex list` and the injected index |
| `version` | Used to track updates during sync |
| `tags` | Helpful for browsing and filtering |
| `owner` | Team or person responsible for the skill |

The markdown body is intentionally flexible. Skills can be short or deep, repo-specific or reusable across many projects.

## Why Not Just RAG?

Lorex is not trying to replace retrieval systems. It solves a different problem: giving the agent explicit, versioned, human-owned knowledge that travels cleanly across repos and tools.

| | Typical RAG setup | lorex |
|---|---|---|
| Source of truth | Chunks in a retrieval pipeline | Markdown skills in Git |
| Ownership | Often unclear after ingestion | Clear file, owner, version, and history |
| Infrastructure | Embeddings, storage, retrieval | A CLI plus files you already control |
| Transparency | Retrieval is usually opaque | The index is readable in the repo |
| Portability | Usually tied to one system | Works across multiple AI coding tools |

RAG answers "what text is similar?". lorex answers "what should this agent know here, and where does that knowledge live?".

## Contributing

Lorex is built with C# and .NET 10, with native AOT publish profiles for Windows, Linux, and macOS.

```bash
git clone https://github.com/alirezanet/lorex
cd lorex
dotnet install.cs
lorex --help
dotnet test
```

If you want to work on the codebase itself, this repo already includes a `lorex-contributing` skill that explains the architecture, layout, and common contribution tasks.

## Help Shape Lorex

Lorex is still in early beta, and this is exactly the stage where feedback is most useful.

If something feels confusing, missing, or awkward:

- open an issue: <https://github.com/alirezanet/lorex/issues>
- send a pull request
- tell us which AI tool or workflow you want lorex to support next

## License

MIT
