# Core Concepts

Before diving into commands, it helps to understand four terms Lorex uses throughout.

---

## Skill

A **skill** is a Markdown document that teaches an AI agent something specific about your project. Think of it as a reference card the agent can always read before touching a particular area of the codebase.

A skill answers questions like:
- "What are the rules for this module?"
- "How does authentication work here?"
- "What do I need to know before deploying?"

A skill lives at `.lorex/skills/<name>/SKILL.md`. That file is the one and only place you author or edit it.

```
.lorex/skills/
  auth-logic/
    SKILL.md         ← the skill you write
  api-conventions/
    SKILL.md
```

---

## Registry

A **registry** is a plain Git repository that stores skills for a team. You create it once (any Git host works) and every project that connects to it can install skills from it.

Think of it like an npm registry, but for AI knowledge. Skills published to the registry are versioned in Git — you get PRs, diffs, rollbacks, and history for free.

A registry has a **policy** (stored as `/.lorex-registry.json` in the registry repo) that controls whether contributors can push directly or must go through a pull request.

---

## Adapter

An **adapter** tells Lorex how to make your skills visible to a specific AI agent. Different agents look for knowledge in different places — Claude reads `.claude/skills/`, Cursor reads `.cursor/rules/`, Gemini reads from paths listed in `.gemini/settings.json`, and so on.

When you run `lorex refresh`, Lorex reads your installed skills from `.lorex/skills/` and **projects** them into every configured adapter's native location. Most adapters do this by creating directory symlinks; Cursor and Roo generate rule files; Gemini updates a settings file.

You choose which adapters to enable during `lorex init`. You can enable as many as you want — they all stay in sync automatically.

---

## Projection

A **projection** is what Lorex writes into an agent's native location. It is always a *derived output* — Lorex generates it from the canonical skill stored in `.lorex/skills/`.

Because projections are derived, you should typically **gitignore them** and re-generate them with `lorex refresh` after cloning. The canonical state to commit is `.lorex/lorex.json` and `.lorex/skills/`.

---

## How the pieces fit together

```
Your project
├── .lorex/
│   ├── lorex.json          ← config: registry URL, adapters, installed skills
│   └── skills/
│       ├── auth-logic/
│       │   └── SKILL.md    ← you write this once
│       └── api-conventions/
│           └── SKILL.md
│
├── .claude/skills/
│   ├── auth-logic  ──────────────────────────────┐  symlinks back to
│   └── api-conventions  ──────────────────────┐  │  .lorex/skills/
│                                               │  │
├── .agents/skills/                             │  │
│   ├── auth-logic  ──────────────────────────────┘  (same target)
│   └── api-conventions  ──────────────────┘
│
└── .cursor/rules/
    ├── lorex-auth-logic.mdc         ← generated rule file
    └── lorex-api-conventions.mdc    ← generated rule file
```

One source of truth. Every agent stays in sync.
