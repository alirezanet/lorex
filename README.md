# lorex

### The Shared Knowledge Registry for AI Agents and People.

**Stop repeating yourself to AI.** Lorex turns your architecture notes, conventions, and runbooks into version-controlled "Skills" that every AI agent understand. 

[![CI](https://github.com/alirezanet/lorex/actions/workflows/ci.yml/badge.svg)](https://github.com/alirezanet/lorex/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#license)
[![Status](https://img.shields.io/badge/status-early%20beta-orange.svg)](#contributing)

-----

## ⚡ Why Lorex?
  * **Works with every agent:** Add a skill once. Lorex projects a synchronized link to your all AI agent tools.
  * **Shared Intelligence:** Build a central "Team Registry" (any Git repo) to share standards (e.g., `security-rules`, `api-conventions`) across your entire organization.
  * **Native AOT:** Fast CLI, No runtime, no VM, no bulky dependencies.

-----

## 🚀 Get Started

### 1\. Install

Choose the method that fits your workflow:

#### **For .NET Developers (Recommended)**

Install Lorex as a global tool using the .NET 10 SDK:

```bash
dotnet tool install -g lorex
```

#### **For Everyone Else (Standalone Binary)**

Lorex is a high-performance **Native** binary. No runtime required.

1.  Download the latest release for your OS (Windows, macOS, Linux) from [GitHub Releases](https://github.com/alirezanet/lorex/releases).
2.  Add the binary to your `PATH`.

-----

### 2\. Initialize

```bash
cd your-project
lorex init
```

This detects your AI tools and installs the built-in `lorex` skill so your agent understands how to work with the registry.

-----

## 🛠️ Key Use Cases

### 1\. Let your AI write the "Lore" (Local Skills)

You don't need to write documentation manually. Because Lorex installs its own definition during `init`, you can simply tell your AI to document the project for you.

**e.g Prompt:**

> "Create a lorex skill called `auth-logic`. Analyze our OAuth implementation and capture the core constraints, pitfalls, and flow so we don't forget them."

**The Result:**
Your AI creates `.lorex/skills/auth-logic/skill.md`. Run `lorex refresh`, and that knowledge is now permanently available to **every** selected AI agent that opens this repo.

### 2\. Share Your Wisdom (The Team Registry)

Turn a local skill into a company-wide standard in seconds. Lorex allows you to **publish** local knowledge to a shared Git registry.

```bash
# 1. Connect to your team's central library (a private Git repo)
lorex init https://github.com/your-org/ai-skills.git

# 2. Publish a locally created skill to the registry for others to use
# This makes it available to everyone else in the org!
lorex publish auth-logic

# 3. Teammates can now install it in their own repos
lorex install auth-logic
```

*Update the skill in the registry once; `lorex sync` updates it for every developer and every repo in the company.*

-----

## 🧠 The "Magic": How it Works

Lorex keeps **one canonical source of truth** for your knowledge:

```text
.lorex/skills/
  auth-logic/
    SKILL.md
  api-conventions/
    SKILL.md
```

That folder is the only place Lorex expects you to author or review skill content. Everything else is a derived projection.

When you run `lorex refresh`, Lorex projects those skills into each agent's **native integration surface**.

For agents with native skill folders, Lorex creates **directory symlinks** back to `.lorex/skills`:

```text
.claude/skills/auth-logic        -> .lorex/skills/auth-logic
.agents/skills/auth-logic        -> .lorex/skills/auth-logic
.github/skills/auth-logic        -> .lorex/skills/auth-logic
.cline/skills/auth-logic         -> .lorex/skills/auth-logic
.windsurf/skills/auth-logic      -> .lorex/skills/auth-logic
.opencode/skills/auth-logic      -> .lorex/skills/auth-logic
```

For agents that use rules or settings instead of skill folders, Lorex generates the right native files from the same source skill:

- Cursor → `.cursor/rules/`
- Roo → `.roo/rules-code/`
- Gemini → `.gemini/settings.json`

So the flow is:

1.  Write or install a skill once in `.lorex/skills/`
2.  Run `lorex refresh`
3.  Lorex syncs every selected adapter to that same source using the format that agent already understands

Because the projections are derived from the canonical skill store, your agents stay in sync without duplicating the actual knowledge across multiple incompatible formats.

-----

## ❓ Why Not Just RAG?

| Feature | Traditional RAG | Lorex |
| :--- | :--- | :--- |
| **Precision** | Probabilistic (can "hallucinate" context) | Explicit & Human-verified |
| **Versioning** | Hard to track in DBs | Git-native (PRs, Diff, History) |
| **Infrastructure** | Requires Vector DB & API | Zero infra. Just a CLI and files. |
| **Control** | "Black box" retrieval | You decide exactly what the agent knows. |

-----

## 🤝 Contributing

Lorex is a high-performance CLI built with **.NET 10** and **Native AOT**.

```bash
git clone https://github.com/alirezanet/lorex
cd lorex
dotnet install.cs # Builds and installs the dev version
```

**This repo dogfoods Lorex.** Once cloned, your AI agent can read the `lorex-contributing` skill to learn the internal architecture and contribution workflow automatically\!

-----

### License

MIT
