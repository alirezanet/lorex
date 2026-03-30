# lorex

> Your AI agent just cloned the repo. It already knows everything.

Lorex is a tiny CLI that packages engineering knowledge as **skills** — plain markdown files — and injects them into the AI agent config files your tools already use. Your agent reads the index, loads what it needs, and gets to work.

No SaaS. No vector database. No runtime. One ~5 MB binary.

> **⚠️ Early Beta** — lorex is young and rough around the edges. [Help us shape it.](#help-us-test)

---

## The Problem

Every major AI coding tool already supports some form of instructions file — `AGENTS.md`, `.cursorrules`, `CLAUDE.md`, `.github/copilot-instructions.md`. Most of them also support **skills** or **custom instructions** in one form or another. So you write your context once and it works, right?

Not quite.

Each file is **siloed**. Update your architecture doc in one place and nine others are stale. Switch AI tools and you rewrite everything from scratch. Onboard a new teammate and they copy-paste from someone else's repo. Share knowledge with another team and it's a Slack message or a wiki page that nobody reads.

The knowledge exists. It's just trapped — in one tool, one file, one repo, one person's machine.

Lorex is the connective layer. It gives your agent context files a shared source of truth, a versioned home, and a way to travel — across tools, across repos, across teams — without any of them knowing lorex exists.

---

## Two Ways to Use It

### For open source projects and any git repo — commit skills alongside the code

```bash
lorex init --local --adapters copilot,codex
lorex generate contributing -d "Project architecture and contribution guide"
# Write the skill. Commit it. Done.
git add .lorex/skills/ && git commit -m "add contributing skill"
```

Anyone who clones the repo gets an AI agent that already understands the codebase. No onboarding doc to find. No wiki to search. The agent just knows — because the knowledge lives in the repo, versioned with the code.

**This is what lorex does for lorex itself.** Clone this repo and your agent immediately loads `lorex-contributing` — the full architecture, build commands, and contribution checklist.

---

### For teams — share skills across every project from one place

```bash
# One-time: point lorex at your team's skill registry (a git repo you own)
lorex init https://github.com/your-org/skills --adapters copilot,cursor

# Browse and install
lorex list
lorex install auth-overview
lorex install deployment-runbook
lorex install our-api-conventions
```

Skills are symlinked from a shared local cache. Run `lorex sync` and every project on every machine reflects the latest — instantly, without touching a single file in any repo.

New hire? They run `lorex install` on their repos. Their agent knows your architecture, your conventions, your pitfalls. From day one.

---

## How It Works

A **skill** is just a markdown file. Write what your agent needs to know — architecture, patterns, pitfalls, conventions — and lorex handles the rest.

Lorex injects a **skill index** into your existing agent config files (the ones you already have). The agent reads it at the start of each session and loads only the skills relevant to what it's doing. You don't change how you work with your AI tool — lorex just makes sure it arrives informed.

```
## Lorex Skill Index
- **auth-overview**: Authentication service — supported flows and known pitfalls → .lorex/skills/auth-overview/skill.md
- **deployment**: How we deploy, environment variables, rollback steps → .lorex/skills/deployment/skill.md
```

That's it. No plugins. No new tools. Works with whatever AI agent you already use.

> Skills have optional YAML frontmatter (`name`, `description`, `version`, `tags`, `owner`) used by `lorex list` and the registry. For local use you barely need any of it — see [Skill Format](#skill-format) for the full spec.

---

## Works With Every Major AI Tool

Lorex injects the skill index into whichever config files you already use. Enable one or all — lorex auto-detects what's present.

| Tool | Config file |
|---|---|
| GitHub Copilot | `.github/copilot-instructions.md` |
| Codex / ChatGPT | `AGENTS.md` |
| OpenClaw | `AGENTS.md` |
| Cursor | `.cursorrules` |
| Claude | `CLAUDE.md` |
| Windsurf | `.windsurfrules` |
| Cline | `.clinerules` |
| Roo | `.roorules` |
| Gemini | `GEMINI.md` |
| OpenCode | `opencode.md` |

---

## Install

### Option 1 — Native binary (no runtime needed)

Download from the [Releases](https://github.com/your-org/lorex/releases) page and add to your PATH.

| Platform | Binary |
|---|---|
| Windows x64 | `lorex-win-x64.exe` |
| Windows ARM64 | `lorex-win-arm64.exe` |
| Linux x64 | `lorex-linux-x64` |
| Linux ARM64 | `lorex-linux-arm64` |
| macOS Intel | `lorex-osx-x64` |
| macOS Apple Silicon | `lorex-osx-arm64` |

### Option 2 — dotnet tool

```bash
dotnet tool install -g lorex
```

---

## All Commands

```bash
lorex init [<url>] [--local] [--adapters a,b]   # set up this project
lorex list                                        # browse skills in the registry
lorex install <skill>                             # install a skill
lorex uninstall <skill>                           # remove a skill
lorex status                                      # show installed skills and state
lorex sync                                        # pull latest from the registry
lorex generate [<name>] [-d desc] [-t tags]       # scaffold a new skill
lorex publish [<skill>]                           # push a local skill to the registry
lorex refresh                                     # re-inject the index into agent configs
```

Every command works interactively (prompts for anything missing) and non-interactively (pass flags, skip all prompts — useful in CI or with an AI agent).

`list`, `install`, `sync`, and `publish` require a registry. Everything else works in local-only mode.

---

## Skill Format

A skill is a folder with a `skill.md` file and optionally anything else your agent might need — scripts, executables, config templates, whatever makes sense:

```
.lorex/skills/
  auth-overview/
    skill.md          ← required: YAML frontmatter + free-form markdown
    check-tokens.sh   ← optional: a script the agent can invoke
    schema.json       ← optional: any supporting file you want alongside
```

The frontmatter fields:

| Field | Required | Purpose |
|---|---|---|
| `name` | yes | Unique ID (kebab-case) |
| `description` | yes | One line — shown in `lorex list` and the injected index |
| `version` | no | Used by `lorex sync` to detect updates |
| `tags` | no | Shown in `lorex list` |
| `owner` | no | Team or person responsible |

The body is pure markdown. Write whatever your agent needs to know.

---

## Why Not RAG?

| | RAG | Lorex |
|---|---|---|
| Who decides what to load | The model, based on similarity scores | The agent, by reading an explicit index |
| Ownership | No owner for a vector chunk | Every skill has an author and a version |
| Infrastructure | Vector DB + embedding pipeline | A CLI and markdown files |
| Distribution | Per-team setup | `git clone` or `lorex install` |
| Auditability | Hard to know what was retrieved | Index is a readable text file in the repo |

---

## Help Us Test

Lorex is in **early beta**. We're a small project and we genuinely need people to use it, break it, and tell us what's wrong.

**Things that would help most:**

- Run `lorex init` on a real project — does the flow feel right?
- Write a skill from an actual conversation with your agent — does the format work?
- Try it on Linux or macOS (most development has been on Windows)
- Report confusing error messages — every unclear error is a bug
- Tell us which AI tools you use that aren't in the adapter table

**How to help:**

- **[Open an issue](https://github.com/your-org/lorex/issues)** — bugs, awkward UX, missing adapters, ideas
- **[Start a discussion](https://github.com/your-org/lorex/discussions)** — design questions, use cases, share skills you've written
- **Submit a PR** — first-time contributors welcome; see Contributing below

We'd rather hear about rough edges early than polish the wrong thing.

---

## Roadmap

### v0.0.x — current (stabilisation)
- [x] Skill format with YAML frontmatter
- [x] CLI: `init`, `install`, `uninstall`, `list`, `status`, `generate`, `publish`, `sync`, `refresh`
- [x] Local-only mode — no registry needed
- [x] Git-based registry with shared symlink cache
- [x] 10 adapter targets
- [x] Built-in `lorex` skill — agent knows how to use lorex from day one
- [x] Cross-platform native AOT binaries (win/linux/osx × x64/arm64)
- [x] Non-interactive / CI-friendly flags on all commands

### vNext
- [ ] `lorex upgrade <skill>` — force-reinstall a single skill
- [ ] `lorex init --re` — reconfigure an existing project
- [ ] Skill dependency resolution
- [ ] Public community registry



---

## Contributing

Built with **C# / .NET 10**, compiled to a native AOT binary.

**Prerequisites:** .NET 10 SDK

```bash
git clone https://github.com/your-org/lorex
cd lorex
dotnet run install.cs        # build + install as a local dev tool
lorex --help
```

```bash
dotnet test                  # run tests
dotnet build                 # build only
dotnet publish src/Lorex /p:PublishProfile=win-x64 -c Release   # native binary
```

1. Fork → feature branch → pull request
2. Open an issue first for significant changes

The `lorex-contributing` skill in this repo has the full architecture walkthrough — your agent will load it automatically after cloning.

---

## License

MIT

