# Getting Started

This guide walks you through installing Lorex and getting it working in a real project from scratch.

---

## Step 1 — Install Lorex

Pick the method that fits your setup.

### Quick install (recommended)

**Windows (PowerShell):**

```powershell
irm https://raw.githubusercontent.com/alirezanet/lorex/main/scripts/install.ps1 | iex
```

**macOS / Linux:**

```bash
curl -fsSL https://raw.githubusercontent.com/alirezanet/lorex/main/scripts/install.sh | sh
```

The script downloads the latest release binary and adds it to your PATH. Restart your terminal afterward.

### For .NET developers

If you already have the .NET 10 SDK installed:

```bash
dotnet tool install -g lorex
```

### Manual download

1. Go to [GitHub Releases](https://github.com/alirezanet/lorex/releases)
2. Download the archive for your OS (`windows-x64`, `linux-x64`, `osx-arm64`, etc.)
3. Extract it, rename the binary to `lorex`, and put it somewhere on your PATH

### Verify the install

```bash
lorex --version
```

You should see the version number printed. If you get "command not found", check that the install directory is on your PATH and restart your terminal.

---

## Step 2 — Initialize a project

Navigate to a project you want to add AI skills to, then run:

```bash
cd my-project
lorex init
```

### What the interactive setup asks

**1. Where should Lorex get shared skills from?**

Lorex shows any registries you have used before, plus two options:

- **Enter a new registry URL** — paste a HTTPS or SSH Git URL (e.g. `https://github.com/your-org/ai-skills.git`)
- **Keep this repo local-only** — no registry, you create and manage skills entirely within this project

If you are just getting started alone, choose **local-only**. You can connect a registry later by running `lorex init` again.

**2. Which agent integrations should Lorex maintain?**

Lorex scans the project and marks any agents it detects as `(detected)`. Use Space to toggle adapters on or off, then Enter to confirm.

If nothing is detected, Lorex pre-selects `copilot` and `codex` as safe defaults — these cover GitHub Copilot and OpenAI Codex/ChatGPT.

**3. (If connecting to a new empty registry) How should contributors publish?**

Lorex asks you to choose a publish policy and write `/.lorex-registry.json` to the registry. See [Registry Policy](/reference/registry-policy) for details. If you are unsure, choose **pull-request** — it is the safest option for teams.

### What `lorex init` does to your project

After the setup completes, Lorex:

1. Creates `.lorex/lorex.json` with your chosen registry and adapters
2. Installs the built-in `lorex` skill into `.lorex/skills/lorex/` — this teaches your AI agent how to use Lorex itself
3. Projects skills into each adapter's native location (symlinks, rule files, or settings updates)
4. Offers to install any registry skills that are recommended for this project

Example output:

```
✓ lorex initialised. Native agent projections updated:
  .claude/skills/
  .agents/skills/

Built-in skills installed:
  • lorex

Run lorex sync later to refresh installed shared skills.
```

### Non-interactive usage

If you want to skip the prompts entirely:

```bash
# Connect to a registry, use only the claude adapter
lorex init https://github.com/your-org/ai-skills.git --adapters claude

# Local-only, multiple adapters
lorex init --local --adapters claude,copilot,codex

# Shorthand for --adapters
lorex init --local -a cursor,claude
```

---

## Step 3 — Create your first skill

Now that Lorex is set up, let's create a skill. There are two ways.

### Option A: Let your AI agent write it

Since Lorex just installed its own skill, your AI agent already knows the format. Open your AI agent in this project and give it a prompt like:

> Create a lorex skill called `my-project-conventions`. Analyze this repository's architecture, coding patterns, build commands, test commands, and common pitfalls. Capture the rules every contributor and AI agent should follow before making changes.

The AI will create `.lorex/skills/my-project-conventions/SKILL.md`. Then run:

```bash
lorex refresh
```

### Option B: Scaffold it yourself

```bash
lorex create my-project-conventions
```

Lorex will ask for a description, tags, and owner, then create the file. Open it in your editor and fill it in:

```
.lorex/skills/my-project-conventions/SKILL.md
```

After editing, run `lorex refresh` to update adapter projections.

---

## Step 4 — Check what's installed

```bash
lorex status
```

This shows a summary of your project's state:

```
Project:      /home/you/my-project
Registry:     (none — local-only mode)
Adapters:     claude, copilot

┌─────────────────────────┬───────────┬────────────────────────────────────────┐
│ Skill                   │ Link type │ Path                                   │
├─────────────────────────┼───────────┼────────────────────────────────────────┤
│ lorex                   │ local     │ .lorex/skills/lorex                    │
│ my-project-conventions  │ local     │ .lorex/skills/my-project-conventions   │
└─────────────────────────┴───────────┴────────────────────────────────────────┘
```

**Link type** tells you how each skill is stored:
- `local` (yellow) — a real directory you authored in this project
- `symlink` (green) — installed from a registry, points to the registry cache
- `missing` or `broken symlink` (red) — something went wrong; run `lorex refresh`

---

## What to commit

Once initialized, commit these paths:

```bash
git add .lorex/lorex.json .lorex/skills/
```

The adapter projection directories (`.claude/skills/`, `.agents/skills/`, `.cursor/rules/`, etc.) are **derived outputs** and should not be committed. Add them to `.gitignore`:

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

When a teammate clones the project they run `lorex init` once, which re-creates all the projection symlinks for their own machine.

---

## Windows: enabling symlinks

Lorex creates directory symlinks to project skills into agent locations. On Windows this requires either:

- **Developer Mode** — go to Settings → System → For Developers and turn on Developer Mode (recommended)
- **Running as Administrator** — works but is not required if Developer Mode is on

If Lorex detects that symlinks are unavailable it will print a warning and offer to open the Developer Mode settings page for you.
