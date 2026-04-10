# Command Reference

All commands resolve the project root by walking **up** from the current directory to the nearest ancestor that contains `.lorex/lorex.json`. You never need to `cd` to the repo root before running a lorex command.

Commands that accept `--global` bypass project-root discovery entirely and operate on `~/.lorex/` instead. See [Global Skills](#global-skills) for the workflow.

Every command supports `--help` / `-h` for command-specific usage, flags, and examples:

```bash
lorex <command> --help
```

---

## `lorex init`

Set up Lorex in a project, configure a registry, and choose which AI agent adapters to maintain.

```bash
lorex init [<url|path>] [--local] [--adapters <a,b,...>] [--install-recommended-taps] [-g|--global]
lorex init                         # guided interactive setup
lorex init --global [<url|path>]   # user-level (global) setup
lorex init --help                  # show command help
```

### Flags

| Flag | Short | Description |
| :--- | :--- | :--- |
| `<url\|path>` | — | Registry URL (HTTPS or SSH) or a local absolute path. Skip to use the interactive picker. |
| `--local` | — | Set up without a registry. Skills are project-local only. |
| `--global` | `-g` | Initialise global lorex at `~/.lorex/` instead of the current project. Skills are projected into user-level agent locations (`~/.claude/skills`, `~/.gemini/settings.json`, etc.). |
| `--adapters <list>` | `-a` | Comma-separated list of adapters. Skips the adapter prompt. |
| `--install-recommended-taps` | — | Automatically install all recommended taps from the registry. Useful for CI/scripted setups. In interactive mode, taps are prompted for instead. |
| `--help` | `-h` | Show command-specific help and exit. |

### Interactive flow

Running `lorex init` with no arguments opens a three-step wizard:

**Step 1 — Registry**

Lorex shows a menu with any previously used registries, plus:
- `Enter a new registry URL` — enter a HTTPS or SSH git URL; Lorex verifies it is reachable
- `Keep this repo local-only` — no registry, project-only skills

If you enter a URL for a registry that has never been set up before, Lorex asks you to choose a **publish policy** and writes `/.lorex-registry.json` to the registry.

**Step 2 — Adapters**

Lorex scans the project and marks agents it already detects as `(detected)`. Use Space to toggle, Enter to confirm.

If nothing is detected, `copilot` and `codex` are pre-selected as defaults.

**Step 3 — Recommended skills (if connected to a registry)**

If the registry has skills whose tags match this project's repo slug or folder name, Lorex offers to install them immediately.

### What `init` writes

- `.lorex/lorex.json` — project config (registry URL, adapters, installed skills)
- `.lorex/skills/lorex/SKILL.md` — built-in skill bundled inside the binary
- Adapter projections for every selected adapter

`lorex init` is safe to re-run. It updates the configuration and re-discovers any skills already in `.lorex/skills/`, so skills are never lost.

### Examples

```bash
# Fully guided
lorex init

# Connect to a registry, let Lorex detect adapters
lorex init https://github.com/your-org/ai-skills.git

# Local-only, specific adapters
lorex init --local --adapters claude,copilot

# CI / scripted use
lorex init https://github.com/your-org/ai-skills.git -a codex,claude

# CI / scripted use — also install recommended taps from the registry
lorex init https://github.com/your-org/ai-skills.git -a claude --install-recommended-taps

# Global (user-level) setup — works from any directory
lorex init --global https://github.com/your-org/ai-skills.git --adapters claude,gemini

# Local absolute path — useful for monorepos, network shares, or local testing
lorex init /path/to/my-registry            # Linux / macOS
lorex init C:\repos\my-registry            # Windows
```

---

## `lorex create`

Scaffold a new skill in `.lorex/skills/` for local authoring.

```bash
lorex create [<name>] [--description <text>] [--tags <list>] [--owner <name>]
lorex create                       # fully interactive
lorex generate                     # alias for lorex create
```

### Flags

| Flag | Short | Description |
| :--- | :--- | :--- |
| `<name>` | — | Skill name in kebab-case. Spaces are converted to dashes automatically. |
| `--description <text>` | `-d` | One-sentence description shown to agents. |
| `--tags <list>` | `-t` | Comma-separated tags used for `--recommended` matching. |
| `--owner <name>` | `-o` | Team or person responsible for this skill. |
| `--help` | `-h` | Show command-specific help and exit. |

### Behavior

1. Creates `.lorex/skills/<name>/SKILL.md` with a frontmatter template
2. Adds the skill to `.lorex/lorex.json`
3. Runs `lorex refresh` — the skill is immediately visible to all configured agents

### After creating

```
✓ Skill created at .lorex/skills/auth-logic

Next steps:
  1. Ask your AI agent to author .lorex/skills/auth-logic/SKILL.md, or edit it yourself
  2. The skill is already active locally and projected into configured agent integrations
  3. When ready to share, run lorex publish auth-logic to push it to the registry
```

### Examples

```bash
# Interactive
lorex create

# Non-interactive
lorex create auth-logic -d "Token validation and session rules" -t "auth,security" -o "platform-team"
```

---

## `lorex install`

Install one or more skills from the registry, a tap, or directly from a URL.

```bash
lorex install [<skill|url>...] [--all] [--recommended] [--search <text>] [--tag <tag>] [--global]
lorex install                      # interactive TUI picker
```

### Flags

| Flag | Description |
| :--- | :--- |
| `<skill>...` | One or more skill names to install directly. Skills from taps are matched automatically. |
| `<url>` | A git URL to install from directly — no tap registration required. Supports GitHub tree URLs (`https://github.com/owner/repo/tree/branch/path`). |
| `--all` | Install every skill in the registry and all taps that is not already installed. |
| `--recommended` | Install only skills recommended for this project (matched by tags). |
| `--search <text>` | Pre-filter the interactive skill picker to skills whose name, description, or tags contain `<text>`. Only applies when no skill names or `--all`/`--recommended` flags are given. |
| `--tag <tag>` | Pre-filter the interactive skill picker to skills with the exact tag `<tag>`. Only applies in interactive mode. |
| `--global` | Install into `~/.lorex/skills/` and project into user-level agent locations. Requires `lorex init --global` to have been run first. |
| `--help` | Show command-specific help and exit. |

`--all` and `--recommended` cannot be used together. Neither can be combined with explicit skill names or URLs.

### Interactive mode

Running `lorex install` with no arguments opens the full-screen TUI picker directly:

- Type to filter by name, description, or tag in real time
- ↑↓ to navigate, Space to toggle selection, Enter to confirm, Esc to cancel
- **Ctrl+A** to select or deselect all currently filtered skills
- Recommended skills (matched by project tags) float to the top with a ★ badge
- Skills from taps show a source attribution badge
- PgUp/PgDn to page through results; Tab to show/hide tap skills

Use `--search` or `--tag` flags to pre-populate the filter.

### Direct URL install

You can install a skill directly from any git repository without first adding a tap:

```bash
# Install a specific skill folder from a GitHub repo
lorex install https://github.com/dotnet/skills/tree/main/auth-overview

# The skill is copied as a local directory (not symlinked)
# Its source URL is recorded in lorex.json for reference
```

If the URL points to a plain repo root with a single skill, lorex installs it automatically. If the repo contains multiple skills, lorex requires a specific path.

### Recommended matching

A skill is considered recommended for your project if any of its `tags` match:
- Your repo's GitHub slug (`owner/repo`), e.g. `alirezanet/lorex`
- Your project folder name (normalized to lowercase with slashes)

### Overwrite protection

If installing would replace a skill you already have as a local directory (not a symlink), Lorex asks for explicit confirmation per skill. You can choose to keep your local version.

### After installing

Lorex creates a symlink `.lorex/skills/<name>` → registry cache, updates `lorex.json`, and runs `lorex refresh`. The skill is immediately available to all agents.

### Examples

```bash
lorex install auth-logic
lorex install auth-logic api-conventions checkout-flow
lorex install --recommended
lorex install --all

# Global installs — available from any directory, no project required
lorex install auth-logic --global
lorex install --all --global
```

---

## `lorex uninstall`

Remove installed skills from this project (or globally with `--global`).

```bash
lorex uninstall [<skill>...] [--all] [-g|--global]
lorex uninstall                    # interactive picker
```

### Flags

| Flag | Description |
| :--- | :--- |
| `<skill>...` | One or more skill names to remove. |
| `--all` | Remove every installed skill without prompting. |
| `-g`, `--global` | Operate on the global lorex config (`~/.lorex/`) instead of the current project. |
| `--help` | Show command-specific help and exit. |

Running with no arguments opens the full-screen TUI picker showing all installed skills. Type to filter, Space to toggle, Ctrl+A to select all (filtered), Enter to confirm.

### What it removes

- `.lorex/skills/<name>/` (or the symlink)
- The skill from `.lorex/lorex.json`

Adapter projections for the removed skill are cleaned up on the next `lorex refresh`.

---

## `lorex list`

Browse and filter skills available in the registry and all configured taps.

```bash
lorex list [--search <text>] [--tag <tag>] [--page <n>] [--page-size <n>] [-g|--global]
lorex list                         # interactive TUI browser (when run in a terminal)
```

### Interactive TUI mode (default)

When run in an interactive terminal without `--page`/`--page-size` flags, `lorex list` opens a full-screen browser:

- Type to filter by name, description, or tag in real time
- ↑↓ / PgUp / PgDn to navigate
- Status icons: `✓` installed · `↑` update available · `★` recommended
- Skills from taps show a `(tapname)` attribution badge
- Esc clears the search filter; Enter or Esc with no filter exits

### Non-interactive / piped mode

Pass `--page`, `--page-size`, or pipe the output to get the classic paginated table.

### Flags

| Flag | Description |
| :--- | :--- |
| `--search <text>` | Filter skills whose name, description, or tags contain `<text>` (case-insensitive). |
| `--tag <tag>` | Filter skills with the exact tag `<tag>` (case-insensitive). Can be combined with `--search`. |
| `--page <n>` | Page number to display (1-based, default: 1). Enables non-interactive table output. |
| `--page-size <n>` | Skills per page (default: 25). Use `0` to disable pagination and show all results. Enables non-interactive table output. |
| `-g`, `--global` | Browse skills available in the global lorex config (`~/.lorex/`) instead of the current project. |
| `--help` | Show command-specific help and exit. |

Table status values:
- `installed` (green) — already in this project
- `update available` (yellow) — installed but the registry has a newer version
- `recommended` (blue) — not installed but tags match this project
- `available` (dim) — available but not recommended

### Examples

```bash
lorex list                         # interactive TUI
lorex list --search auth           # TUI pre-filtered to "auth"
lorex list --tag dotnet            # TUI pre-filtered by tag
lorex list --page 2                # table, second page
lorex list --page-size 50          # table, larger page
lorex list --page-size 0           # table, all results
lorex list --search auth --page-size 0  # table, all matching results
lorex list | grep auth             # piped (table mode, no paging)
```

::: info Registry or tap required
`lorex list` requires a registry or at least one configured tap. In local-only mode it prints a message and exits.
:::

---

## `lorex status`

Show the current state of this project: registry, adapters, and installed skills. Pass `--global` to view the global lorex context instead.

```bash
lorex status [-g|--global]
```

### Output

```
Project:      /home/you/my-project
Registry:     https://github.com/your-org/ai-skills.git
Publish mode: pull-request (base branch: main)
Adapters:     claude, copilot, codex

┌──────────┬──────────┬───────────────────────────────────┐
│ Adapter  │ Target                                        │
├──────────┼───────────────────────────────────────────────┤
│ claude   │ /home/you/my-project/.claude/skills           │
│ copilot  │ /home/you/my-project/.github/skills           │
│ codex    │ /home/you/my-project/.agents/skills           │
└──────────┴───────────────────────────────────────────────┘

┌─────────────────┬───────────┬─────────────────────────────────────┐
│ Skill           │ Link type │ Path                                │
├─────────────────┼───────────┼─────────────────────────────────────┤
│ lorex           │ local     │ .lorex/skills/lorex                 │
│ auth-logic      │ symlink   │ .lorex/skills/auth-logic            │
│ api-conventions │ symlink   │ .lorex/skills/api-conventions       │
└─────────────────┴───────────┴─────────────────────────────────────┘
```

### Flags

| Flag | Description |
| :--- | :--- |
| `-g`, `--global` | Show the global lorex context (`~/.lorex/`) instead of the current project. |
| `--help` | Show command-specific help and exit. |

### Link types

| Type | Color | Meaning |
| :--- | :--- | :--- |
| `local` | Yellow | A real directory you authored. Not yet published. |
| `symlink` | Green | Installed from a registry. Points to the local registry cache. |
| `missing` | Red | Directory does not exist. Run `lorex refresh`. |
| `broken symlink` | Red | Symlink target is gone. Run `lorex sync` to restore. |

---

## `lorex sync`

Pull the latest skill content from the registry.

```bash
lorex sync [-g|--global]
```

### Flags

| Flag | Description |
| :--- | :--- |
| `-g`, `--global` | Sync global skills at `~/.lorex/` instead of the current project. |
| `--help` | Show command-specific help and exit. |

### What it does

1. Fetches the registry (runs `git pull` on the local cache)
2. Automatically removes skills whose registry entry has been deleted (broken symlinks with no upstream source)
3. Refreshes the cached registry policy in `lorex.json`
4. Because installed skills are directory symlinks into the cache, they immediately reflect new content
5. Syncs all configured taps (`git pull` on each tap cache)
6. Runs `lorex refresh` if any skills were updated

### Stale skill cleanup

If a skill you have installed has been deleted from the registry, Lorex removes it automatically and reports it:

```
Removed 2 stale skills (deleted from registry):
  • authentication
  • authentication-core
```

### Overwrite protection

If the registry has an updated version of a skill you have edited locally (link type `local`), Lorex asks for confirmation before overwriting it:

```
Sync will replace local skill auth-logic with the registry version. Continue?
> Yes
  No (keep existing local skill)
```

Skills you decline to overwrite are skipped and reported at the end.

### Dirty registry cache

If you have edited a registry-installed (symlinked) skill in place, those changes live directly in the local registry cache. Lorex detects uncommitted tracked changes before pulling and prompts you:

```
⚠  Registry cache has uncommitted changes:
  • datacore-athena  →  publish: lorex publish -g datacore-athena

What would you like to do?
> Keep my changes (cancel sync)
  Discard changes and sync
```

Choose **Keep my changes** to cancel sync and run the suggested publish command first. Choose **Discard changes and sync** to revert your edits and continue the sync.

### Examples

```bash
lorex sync               # sync current project
lorex sync --global      # sync globally installed skills
```

::: info Registry required
`lorex sync` requires a registry. Run `lorex init <url>` first if you are in local-only mode.
:::

---

## `lorex publish`

Contribute a skill to the registry — either a locally authored skill or a registry-installed skill you have edited in place.

```bash
lorex publish [<skill>...]          # project skills
lorex publish -g [<skill>...]       # globally installed skills
lorex publish                       # interactive multi-select
```

### Flags

| Flag | Short | Description |
| :--- | :--- | :--- |
| `--global` | `-g` | Publish from the global lorex root (`~/.lorex/`) instead of the current project. |
| `--help` | `-h` | Show command-specific help and exit. |

### Which skills can be published

`lorex publish` surfaces two categories of skills:

- **Locally authored skills** (link type `local` in `lorex status`) — real directories you created with `lorex create`.
- **Registry-installed skills with local edits** (link type `symlink`) — skills whose files in the registry cache have uncommitted changes. Because the symlink points directly into the local cache, editing the skill file edits the cache, and publish commits and pushes those changes.

Built-in skills (such as `lorex`) cannot be published.

### Interactive mode

Running `lorex publish` with no arguments opens the full-screen TUI picker showing all publishable skills (locally authored, plus registry-installed skills with uncommitted cache changes). Type to filter, Space to toggle, Ctrl+A to select all, Enter to confirm.

### Behavior by policy

**Locally authored skill — `pull-request` policy:**

1. Pulls the latest registry cache
2. Creates a branch: `lorex/<skill-name>-<timestamp>`
3. Copies the skill into `skills/<name>/` on that branch
4. Commits and pushes
5. Prints the branch name and a pull request URL (GitHub only)

Your local skill directory stays as-is until the PR is merged and you run `lorex sync`.

**Locally authored skill — `direct` policy:**

1. Pulls the latest registry cache
2. Commits and pushes the skill to the base branch
3. Immediately replaces your local directory with a symlink to the registry cache

**Registry-installed skill — `direct` policy:**

1. Stages the uncommitted changes in the registry cache scoped to the skill's path
2. Commits and pushes directly to the base branch
3. The symlink continues pointing to the same cache location — no reinstall needed

**Registry-installed skill — `pull-request` policy:**

1. Snapshots your edits to a temporary directory
2. Reverts the cache working tree so the base checkout can proceed cleanly
3. Creates a worktree on a new branch and copies the snapshot there
4. Commits and pushes the branch; prints the PR URL

**`read-only` policy:**

Blocked. The command exits with an error. Contact the registry owner to change the policy.

### Examples

```bash
lorex publish checkout-flow
lorex publish checkout-flow auth-logic
lorex publish                       # pick interactively
lorex publish -g datacore-athena    # publish a globally installed registry skill
lorex publish -g                    # interactive picker for global skills
```

::: info Registry required
`lorex publish` requires a registry.
:::

---

## `lorex registry`

Interactively update the connected registry's publish policy.

```bash
lorex registry
```

Prompts you to choose a new publish mode, base branch, and PR branch prefix.

**If the current policy is `direct`:** the new policy is committed and pushed immediately.

**If the current policy is `pull-request`:** Lorex creates a review branch for the policy change. The existing policy stays in effect until that PR is merged and `lorex sync` is run.

::: info Registry required
`lorex registry` requires a registry.
:::

---

## `lorex refresh`

Re-project skills into native agent locations without fetching from the registry.

```bash
lorex refresh [--target <adapter>]
```

### Flags

| Flag | Short | Description |
| :--- | :--- | :--- |
| `--target <adapter>` | `-t` | Re-project only a single adapter instead of all. |
| `--help` | `-h` | Show command-specific help and exit. |

### When to run it

- After editing a skill file (symlink adapters update automatically, but Cursor/Roo need regenerated rule files)
- After adding a new adapter to `lorex.json`
- After cloning a project that has `.lorex/` committed but no projections (e.g. because projections are gitignored)
- If a projection looks stale or broken

### Examples

```bash
lorex refresh                      # all adapters
lorex refresh --target cursor      # cursor only
lorex refresh -t claude            # claude only
```

---

## `lorex tap`

Manage read-only skill sources (taps). A tap is any git repository containing skills — no lorex registry setup required.

```bash
lorex tap add     <url> [--name <name>] [--root <path>] [-g|--global]
lorex tap remove  <name> [-g|--global]
lorex tap list    [-g|--global]
lorex tap sync    [<name>] [-g|--global]
lorex tap promote [<name>]
```

By default, tap commands operate on the current project's `.lorex/lorex.json`. Pass `-g` / `--global` to operate on the global lorex config at `~/.lorex/` instead.

The git clone cache at `~/.lorex/taps/<slug>/` is always global and shared across all projects using the same tap URL.

### `lorex tap add`

```bash
lorex tap add <url> [--name <name>] [--root <path>] [-g|--global]
```

| Argument / Flag | Description |
| :--- | :--- |
| `<url>` | Git URL of the tap repository (HTTPS, SSH, or a local absolute path). |
| `--name <name>` | Short identifier for the tap. Defaults to the repository owner (e.g. `dotnet` from `github.com/dotnet/skills`). |
| `--root <path>` | Subdirectory within the repo to search for skills. By default lorex checks for a `skills/` subdirectory and falls back to the repo root. |
| `-g`, `--global` | Add the tap to the global lorex config (`~/.lorex/`) instead of the current project. |

On `lorex tap add`, lorex:
1. Shallow-clones the repository to `~/.lorex/taps/<slug>/`
2. Discovers all skills (validates at least one `SKILL.md` is found)
3. Records the tap in `.lorex/lorex.json` (or `~/.lorex/lorex.json` with `--global`)

If no skills are found, the command fails with an error and the config is not modified.

```bash
# Add a tap using the default name (derived from repo owner)
lorex tap add https://github.com/dotnet/skills

# Explicit name
lorex tap add https://github.com/dotnet/skills --name dotnet

# Repo where skills live in a subdirectory
lorex tap add https://github.com/my-org/monorepo --root tools/skills --name my-org

# Add a tap to the global config (available as a source in all projects)
lorex tap add https://github.com/dotnet/skills --global

# Local absolute path — monorepo, network share, or local testing
lorex tap add /path/to/skills-repo --name local-tap   # Linux / macOS
lorex tap add C:\repos\skills-repo --name local-tap   # Windows
```

### `lorex tap remove`

```bash
lorex tap remove <name> [-g|--global]
```

| Flag | Description |
| :--- | :--- |
| `-g`, `--global` | Remove the tap from the global lorex config instead of the current project. |

Removes the tap entry from `.lorex/lorex.json`. The global cache (`~/.lorex/taps/<slug>/`) is kept intact — other projects using the same tap URL continue to work without re-cloning.

### `lorex tap list`

```bash
lorex tap list [-g|--global]
```

| Flag | Description |
| :--- | :--- |
| `-g`, `--global` | List taps from the global lorex config instead of the current project. |

Shows a table of configured taps: name, URL, skill count, root, and cache status.

### `lorex tap sync`

```bash
lorex tap sync [<name>] [-g|--global]   # sync all taps (or one by name)
```

| Flag | Description |
| :--- | :--- |
| `-g`, `--global` | Sync taps from the global lorex config instead of the current project. |

Runs `git pull` on the specified tap cache(s). Skills installed from taps are symlinked, so they reflect the latest content immediately after sync.

`lorex sync` (without `tap`) also syncs all taps alongside the primary registry.

### After adding a tap

Skills from the tap immediately appear in `lorex list` and `lorex install` alongside registry skills, with a `(tapname)` attribution badge.

```bash
lorex tap add https://github.com/dotnet/skills --name dotnet
lorex list                   # see dotnet skills with (dotnet) badge
lorex install                # pick from registry + tap skills in the TUI
```

### `lorex tap promote`

```bash
lorex tap promote [<name>]
```

Adds one or more locally configured taps to the registry's `recommendedTaps` list so all connected projects are notified about them on `lorex init` and `lorex sync`.

| Argument | Description |
| :--- | :--- |
| `<name>` | Name of the tap to promote. If omitted, an interactive picker shows all local taps not yet in `recommendedTaps`. |

Requires a connected registry (not available in local-only mode). Respects the registry's publish mode:

- **`direct`** — updates `/.lorex-registry.json` immediately and pushes.
- **`pull-request`** — creates a review branch and prints a PR URL.
- **`read-only`** — blocked; the registry owner must change the policy first.

If the named tap is already in `recommendedTaps`, the command exits successfully with no changes.

To manage the full recommendations list (including removing entries), use `lorex registry` instead.

```bash
# Promote a specific tap by name
lorex tap promote dotnet

# Interactive picker — select from taps not yet recommended
lorex tap promote
```

---

## `lorex --version`

Print the installed version and exit.

```bash
lorex --version
lorex -v
```

---

## `lorex --help`

Print command usage and exit.

```bash
lorex --help
lorex -h
```

---

## Global Skills

The `--global` flag lets you install and sync skills **without being inside a project**. Skills are stored in `~/.lorex/skills/` and projected into your user-level agent directories (`~/.claude/skills`, `~/.gemini/settings.json`, etc.), so they are available in every project you open.

### Setup

```bash
# 1. Initialise — run once, from any directory
lorex init --global https://github.com/your-org/ai-skills.git --adapters claude,gemini

# 2. Install skills
lorex install --all --global

# 3. Keep skills up to date
lorex sync --global
```

### Directory layout

```
~/.lorex/
├── lorex.json          ← global config (registry, adapters, installed skills)
└── skills/
    ├── auth-logic/     [symlink] → ~/.lorex/cache/<registry>/skills/auth-logic
    └── api-conventions/[symlink] → ~/.lorex/cache/<registry>/skills/api-conventions

~/.claude/skills/
├── auth-logic/         [symlink] → ~/.lorex/skills/auth-logic
└── api-conventions/    [symlink] → ~/.lorex/skills/api-conventions

~/.gemini/settings.json ← updated to include global skill directories
```

### Comparison: project vs global

| | Project (`lorex init`) | Global (`lorex init --global`) |
| :--- | :--- | :--- |
| Config location | `.lorex/lorex.json` | `~/.lorex/lorex.json` |
| Skills location | `.lorex/skills/` | `~/.lorex/skills/` |
| Agent projections | `.claude/skills/`, `.gemini/settings.json`, … | `~/.claude/skills/`, `~/.gemini/settings.json`, … |
| Scope | One repository | All projects on this machine |
| Run from | Inside the repository | Any directory |

::: tip
Project skills and global skills work independently. A project can have its own `lorex init` with project-specific skills, and those are completely separate from your globally installed skills.
:::

---

## Nested Registry Layout

Skill registries support organizing skills into nested subdirectories for logical grouping. Skills are identified by their **leaf directory name** and are flattened on install.

### Registry structure

```
skills/
├── api-conventions/          ← flat (traditional)
│   └── SKILL.md
├── security/                 ← category folder (no SKILL.md)
│   ├── auth-logic/
│   │   └── SKILL.md
│   └── rbac/
│       └── SKILL.md
└── devops/
    └── ci-pipeline/
        └── SKILL.md
```

### What gets installed

All four skills are discovered regardless of nesting depth. After `lorex install --all`, the project layout is flat:

```
.lorex/skills/
├── api-conventions/  → symlink
├── auth-logic/       → symlink
├── rbac/             → symlink
└── ci-pipeline/      → symlink
```

### Rules

- A directory is recognized as a skill if it contains `SKILL.md`, `skill.md`, or `metadata.yaml`
- Category folders (directories without a skill entry file) are traversed recursively
- **Skill names must be unique across the entire registry tree.** If two nested paths share the same leaf name (e.g., `security/auth` and `legacy/auth`), only the first one found is used
- `lorex publish` places new skills flat under `skills/` — nesting is a registry-side organizational choice
- Nesting depth is unlimited, but keeping it to one or two levels is recommended
