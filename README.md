# Lorex

### The Shared Knowledge Registry for AI Agents and People

**Stop repeating yourself to AI.** Lorex turns your architecture notes, conventions, and runbooks into version-controlled "Skills" that every AI agent understands.

[![CI](https://github.com/alirezanet/lorex/actions/workflows/ci.yml/badge.svg)](https://github.com/alirezanet/lorex/actions/workflows/ci.yml)
[![Native AOT](https://img.shields.io/badge/Native-AOT-blue.svg)](https://docs.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#license)
[![Status](https://img.shields.io/badge/status-early%20beta-orange.svg)](#contributing)

*Lorex is under active development. Expect rapid changes, rough edges, and frequent improvements.*

-----

### ℹ️ Who Needs Lorex?

  * **Multi-agent users:** People using multiple AI agents who want a single, reusable source of truth for skills.
  * **Teams:** Groups that want to share AI-ready project knowledge without rewriting it for every person or tool.
  * **Developers:** Those who want project-specific skills to be easy for others to install and use.
  * **Power Users:** People who have productive AI sessions but find it difficult to turn that knowledge into a reusable skill.

### ⚡ Why Lorex?

  * **Works with every agent:** Add a skill once. Lorex creates a synchronized link to all your AI agent tools.
  * **Shared Intelligence:** Build a central "Team Registry" (any Git repo) to share standards (e.g., `security-rules`, `api-conventions`) across your entire organization.
  * **Native AOT:** Fast CLI with no runtime, no VM, and no bulky dependencies.

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
dotnet tool install -g lorex --prerelease
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

### 1\. Let your AI write the "Lore" (Local Skills)

You don't need to write documentation manually. Since Lorex installs its own definition during `init`, you can simply tell your AI to document the project for you.

**Example Prompt:**

> Create a lorex skill called `<projectName>-conventions`. Analyze this repository's architecture, coding patterns, build and test commands, and common pitfalls. Capture the rules every contributor and AI agent should follow before making changes.

**The Result:**
Your AI creates `.lorex/skills/<projectName>-conventions/SKILL.md`. Run `lorex refresh`, and that knowledge is now permanently available to **every** supported AI agent used your this repo.

### 2\. Share Your Wisdom (The Team Registry)

Turn a local skill into a company-wide standard in seconds. Lorex allows you to **publish** local knowledge to a shared Git registry.

```bash
# 1. Connect to your team's central library (a private Git repo)
lorex init https://github.com/your-org/ai-skills.git

# 2. Publish a locally created skill to the registry
# This makes it available to everyone else in the organization!
lorex publish auth-logic

# 3. Teammates can now install it in their own repos
lorex install auth-logic
```

*Update the skill in the registry once; `lorex sync` updates it for every developer and every repo in the company.*

*For team registries, Lorex can store a registry policy in `.lorex-registry.json` so contributors sync and install from the shared registry normally, while `lorex publish` utilizes a pull-request workflow instead of pushing directly.*

-----

## 🧠 How It Works

Lorex maintains **one canonical source of truth** for your knowledge:

```text
.lorex/skills/
  auth-logic/
    SKILL.md
  api-conventions/
    SKILL.md
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

Because projections are derived from the canonical skill store, your agents stay in sync without duplicating knowledge across multiple incompatible formats.

-----

## ❓ Why Not Just RAG?

| Feature | Traditional RAG | Lorex |
| :--- | :--- | :--- |
| **Precision** | Probabilistic (can "hallucinate" context) | Explicit & Human-verified |
| **Versioning** | Hard to track in databases | Git-native (PRs, Diff, History) |
| **Infrastructure** | Requires Vector DB & API | Zero infra. Just a CLI and files. |
| **Control** | "Black box" retrieval | You decide exactly what the agent knows. |

-----

## 🤝 Contributing

Lorex is a young project, and contributions are welcome\! If you want to help improve the tool, its integrations, or the developer experience:

```bash
git clone https://github.com/alirezanet/lorex
cd lorex
dotnet ./install.cs # Builds and installs the dev version
```

**This repo dogfoods Lorex.** Once cloned, your AI agent can read the `lorex-contributing` skill to learn the internal architecture and contribution workflow automatically\!

### Roadmap Ideas:

  * Shared prompts and other reusable AI assets alongside skills.
  * Support for sub-agents and structured agent building blocks.
  * Expanded AI provider support and native integrations.
  * Improved methods for extracting reusable skills from AI sessions.

-----

### License

MIT
