# Lorex

### The Shared Knowledge Registry for AI Agents and People

**Stop repeating yourself to AI.** Lorex manages **skills** (Markdown files that teach your AI agents about your project's architecture, conventions, and rules) and keeps every tool automatically in sync.

Write a skill once. Lorex projects it into Claude, Cursor, Copilot, Gemini, Cline, Windsurf, Roo, and more. Connect a Git repository as your team registry and every developer and every repo stays current with a single `lorex sync`.

[![CI](https://github.com/alirezanet/lorex/actions/workflows/ci.yml/badge.svg)](https://github.com/alirezanet/lorex/actions/workflows/ci.yml)
[![Native AOT](https://img.shields.io/badge/Native-AOT-blue.svg)](https://docs.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#license)
[![Docs](https://img.shields.io/badge/docs-alirezanet.github.io%2FLorex-blue.svg)](https://alirezanet.github.io/Lorex/)
[![NuGet version (Lorex)](https://img.shields.io/nuget/vpre/Lorex.svg?style=flat-square&label=latest&color=yellowgreen)](https://www.nuget.org/packages/Lorex/)

-----

### ℹ️ Who Needs Lorex?

  * **Multi-agent users:** You use Claude, Cursor, Copilot, or others and you're tired of re-explaining the same project context to each one.
  * **Teams:** You want a single, reviewed, version-controlled source of AI knowledge that every developer and every repo can pull from.
  * **Developers:** You have productive AI sessions and want to capture what worked as a reusable, shareable skill.
  * **Open-source maintainers:** You want contributors' AI agents to understand your project's conventions from day one.

### ⚡ Why Lorex?

  * **Works with every agent:** Add a skill once. Lorex projects it into every AI tool's native location, with no manual copying or format translations.
  * **Shared team intelligence:** Build a central registry (any private Git repo) to share standards like `security-rules` or `api-conventions` across your entire organization. Publish once, sync everywhere.
  * **Community skill sources:** Pull skills from any public Git repository with `lorex tap add <url>`. Connect a framework team's collection, an open-source library's conventions, or a colleague's repo in one command.
  * **No vendor lock-in:** Your knowledge lives in plain Markdown in your own Git repository, portable across any AI tool, today or tomorrow.
  * **You stay in control:** No probabilistic retrieval, no black-box context injection. You decide exactly what your agents know and when it changes.
  * **Zero infrastructure:** No vector database, no API, no hosted service. Just a CLI and files.
  * **Instant install:** Single native binary. No runtime, no VM, starts in milliseconds on Windows, macOS, and Linux.

-----

## 🚀 Get Started

### 1\. Install

Choose the method that fits your workflow:

#### **Quick Install (Recommended)**

Install from the latest GitHub release:

**Windows (PowerShell):**

```powershell
irm https://raw.githubusercontent.com/alirezanet/lorex/main/scripts/install.ps1 | iex
```

**macOS / Linux:**

```bash
curl -fsSL https://raw.githubusercontent.com/alirezanet/lorex/main/scripts/install.sh | sh
```

#### **For .NET Developers**

Install Lorex as a global tool using the .NET 10 SDK:

```bash
dotnet tool install -g lorex
```

#### **Manual Download (Fallback)**

1.  Download the latest release for your OS (Windows, macOS, Linux) from [GitHub Releases](https://github.com/alirezanet/lorex/releases).
2.  Rename the file to `lorex` and add the binary to your `PATH`.

-----

### 2\. Initialize

```bash
cd your-project
lorex init
```

This command detects your AI tools, installs the built-in `lorex` skill, and suggests registry skills relevant to the current repo. If the connected registry contains skills this project doesn't have yet, Lorex will direct you to `lorex install --recommended`, `lorex list`, or `lorex sync` to keep shared skills fresh.

-----

## 🛠️ Key Use Cases

### 1\. Let your AI write the skill

You don't need to write documentation manually. Since Lorex installs its own definition during `init`, you can simply tell your AI to document the project for you.

**Example Prompt:**

> Create a lorex skill called `<projectName>-conventions`. Analyze this repository's architecture, coding patterns, build and test commands, and common pitfalls. Capture the rules every contributor and AI agent should follow before making changes.

**The Result:**
Your AI creates `.lorex/skills/<projectName>-conventions/SKILL.md`. Run `lorex refresh`, and that knowledge is now permanently available to **every** supported AI agent used in your repo.

### 2\. Share your standards across the team

Turn a local skill into a company-wide standard in seconds.

```bash
# 1. Connect to your team's central library (a private Git repo)
lorex init https://github.com/your-org/ai-skills.git

# 2. Publish a locally created skill to the registry
lorex publish auth-logic

# 3. Teammates install it in their own repos
lorex install auth-logic
```

*For team registries, Lorex stores a policy in `.lorex-registry.json` so `lorex publish` can require pull-request review instead of pushing directly.*

### 3\. Pull from community skill sources

Connect any public Git repository as a read-only skill source with no registry setup needed.

```bash
# Add a tap (clones the repo, discovers skills)
lorex tap add https://github.com/dotnet/skills --root plugins/

# Tap skills appear alongside registry skills in list and install
lorex list
lorex install csharp-scripts

# Keep tap skills up to date
lorex sync
```

-----

## 🧠 How It Works

Lorex maintains **one canonical source of truth** for your knowledge:

```text
.lorex/
  lorex.json                ← config: registry, taps, adapters, installed skills
  skills/
    auth-logic/
      SKILL.md              ← you write this once
    api-conventions/        → registry cache symlink
    csharp-scripts/         → tap cache symlink
```

This folder is the only place Lorex expects you to author or review skill content. Everything else is a derived projection. When you run `lorex refresh`, Lorex projects those skills into each agent's **native integration surface**.

For agents with native skill folders, Lorex creates **directory symlinks** back to `.lorex/skills`:

```text
.claude/skills/auth-logic        -> .lorex/skills/auth-logic
.agents/skills/auth-logic        -> .lorex/skills/auth-logic
.github/skills/auth-logic        -> .lorex/skills/auth-logic
.cline/skills/auth-logic         -> .lorex/skills/auth-logic
.windsurf/skills/auth-logic      -> .lorex/skills/auth-logic
.opencode/skills/auth-logic      -> .lorex/skills/auth-logic
```

For agents that use rules or settings files instead of folders, Lorex generates the appropriate native files from the source skill:

  * **Cursor** → `.cursor/rules/`
  * **Roo** → `.roo/rules-code/`
  * **Gemini** → `.gemini/settings.json`

Because projections are derived from the canonical skill store, your agents stay in sync without duplicating knowledge across multiple incompatible formats. Symlinked skills (registry and tap installs) are gitignored automatically. Only your local skills and `lorex.json` need to be committed.

-----

## ❓ Why Not Just RAG?

| Feature | Traditional RAG | Lorex |
| :--- | :--- | :--- |
| **Precision** | Probabilistic (can "hallucinate" context) | Explicit & human-verified |
| **Versioning** | Hard to track in databases | Git-native (PRs, diffs, history) |
| **Infrastructure** | Requires vector DB & API | Zero infra, just a CLI and files |
| **Control** | "Black box" retrieval | You decide exactly what the agent knows |
| **Sharing** | Per-tool, per-person | One registry, every agent, every teammate |

-----

## 🤝 Contributing

Lorex is a young project and contributions are welcome! If you want to help improve the tool, its integrations, or the developer experience:

```bash
git clone https://github.com/alirezanet/lorex
cd lorex
dotnet build                  # build from source
dotnet run --project src/Lorex -- <args>   # run without installing
dotnet run install.cs         # build and install the dev version globally
dotnet test                   # run tests
```

**This repo dogfoods Lorex.** Once cloned, your AI agent can read the `lorex-contributing` skill to learn the internal architecture and contribution workflow automatically.

*Lorex is under active development. Expect rapid changes, rough edges, and frequent improvements.*
If lorex has been useful, consider ⭐ starring the repo!

-----

### License

MIT
