# Team Registry

A team registry is a plain Git repository that holds a shared library of skills. Any project that connects to it can install skills from it and publish skills back to it.

This page walks through the full lifecycle: setting up a registry, connecting projects to it, sharing skills, and keeping everyone in sync.

---

## Setting up a registry

### 1. Create an empty Git repository

Create a new empty repo on GitHub, GitLab, Bitbucket, or any Git host. No special structure is required — Lorex initializes the registry when you first connect a project.

::: tip Private repositories
You can use a private repo as a registry. Lorex uses `git` under the hood, so any repo you can clone works.
:::

### 2. Connect your first project

```bash
cd my-project
lorex init https://github.com/your-org/ai-skills.git
```

If the registry is new (no `/.lorex-registry.json` yet), Lorex asks you to set a **publish policy**:

```
This registry does not define a lorex policy yet. Lorex can initialize
.lorex-registry.json in the registry root now.

How should contributors publish skills to this registry?
> Publish via pull request  - contributors push review branches instead of writing directly
  Direct publish            - contributors commit and push straight to the registry
  Read-only                 - skills can be installed and synced, but publishing is blocked

Initial branch name for this registry: main
```

For most teams, **pull request** is the right choice — it means every new skill gets reviewed before it is available to others. You can always change this later with `lorex registry`.

After confirming, Lorex:
1. Writes `/.lorex-registry.json` to the registry repo
2. Writes `.lorex/lorex.json` to your project with the registry URL and policy
3. Saves the registry URL globally so future projects can select it from a list

---

## Publishing a skill

You must have a locally authored skill (link type `local` in `lorex status`) before you can publish.

```bash
lorex publish checkout-flow
```

### What happens with `pull-request` policy

Lorex:
1. Pulls the latest registry cache
2. Creates a new branch: `lorex/checkout-flow-20250401120532`
3. Copies `.lorex/skills/checkout-flow/` into `skills/checkout-flow/` on that branch
4. Commits and pushes the branch
5. Prints a PR URL if the registry is on GitHub:
   ```
   ✓ Prepared checkout-flow for review on branch lorex/checkout-flow-20250401120532 targeting main.
   Open a PR: https://github.com/your-org/ai-skills/compare/main...lorex/checkout-flow-20250401120532?expand=1
   ```

Your local skill stays as a real directory until the PR is merged and you run `lorex sync`.

### What happens with `direct` policy

Lorex:
1. Pulls the latest registry cache
2. Commits and pushes the skill directly to the base branch
3. Replaces your local skill directory with a symlink to the registry cache

```
✓ Published checkout-flow directly to the registry.
```

After a direct publish, `lorex status` will show the skill as `symlink` instead of `local`.

### Publishing multiple skills at once

```bash
# Specific names
lorex publish checkout-flow auth-logic

# Interactive multi-select (shows only local, unpublished skills)
lorex publish
```

### What you cannot publish

- **Built-in skills** (like `lorex`) — these are bundled in the binary. If you want to customize one, run `lorex create my-lorex` and publish that instead.
- **Already-symlinked skills** — a skill that is already registry-backed cannot be published again as-is. Edit it, then `lorex sync` to pull it, which will turn it back into a local directory if you have local changes.

---

## Installing skills from a registry

Once the registry has skills, other projects (or teammates) can install them.

### Browse first

```bash
lorex list
```

This fetches the registry and shows a table of all available skills, their descriptions, tags, and whether each is already installed or recommended for this project:

```
Registry: https://github.com/your-org/ai-skills.git
Recommended for this project: checkout-flow, api-conventions

┌────────────────────┬──────────────────────────────┬─────────┬──────────────────┬─────────────┐
│ Skill              │ Description                  │ Version │ Tags             │ Status      │
├────────────────────┼──────────────────────────────┼─────────┼──────────────────┼─────────────┤
│ checkout-flow      │ Checkout lifecycle and rules │ 1.2.0   │ checkout,payments│ recommended │
│ api-conventions    │ REST API design rules        │ 1.0.0   │ api,rest         │ recommended │
│ security-rules     │ OWASP rules and practices    │ 2.1.0   │ security         │ available   │
│ lorex              │ How to use lorex             │ 1.0.0   │ lorex            │ installed   │
└────────────────────┴──────────────────────────────┴─────────┴──────────────────┴─────────────┘
```

Skills marked `recommended` have tags that match your repo's GitHub slug (`owner/repo`) or folder name.

### Install skills

```bash
# Install specific skills
lorex install checkout-flow api-conventions

# Interactive picker (opens after lorex list)
lorex install

# Install everything recommended for this project
lorex install --recommended

# Install every skill in the registry
lorex install --all
```

The interactive picker lets you choose between "Install recommended", "Install all", or "Choose specific skills" with a multi-select menu.

::: warning Overwrite protection
If installing a registry skill would replace a skill you already have locally, Lorex will ask you to confirm before overwriting it — per skill, not all at once. You cannot accidentally lose local work.
:::

### After installing

Lorex creates a symlink `.lorex/skills/<name>` → registry cache, registers the skill in `lorex.json`, and runs `lorex refresh`. Your AI agents can use the skill immediately.

---

## Keeping skills up to date

```bash
lorex sync
```

This pulls the latest content from the registry. Because installed skills are directory symlinks, they automatically reflect the new content as soon as the cache is updated — no reinstall needed.

If a sync would overwrite a skill you have edited locally, Lorex asks for confirmation per skill:

```
Sync will replace local skill auth-logic with the registry version. Continue?
> Yes
  No (keep existing local skill)
```

Skills you decline to overwrite are left as-is and shown as `skipped` in the output.

---

## Onboarding a new teammate

A teammate clones the project and sees `.lorex/lorex.json` already committed. They run:

```bash
lorex init
```

Lorex reads the registry URL from `lorex.json`, detects their installed AI agents, sets up adapter projections on their machine, and offers to install any recommended skills. The whole setup takes under a minute.

---

## Changing the registry policy

```bash
lorex registry
```

Opens an interactive prompt to change the publish mode, base branch, or PR branch prefix.

If the current policy is `direct`, the change is committed and pushed immediately.

If the current policy is `pull-request`, Lorex creates a review branch for the policy change itself. The existing policy stays in effect until that PR is merged and teammates run `lorex sync`.

---

## Registry layout

After a few skills are published, your registry repo looks like this:

```
.lorex-registry.json        ← publish policy
skills/
  auth-logic/
    SKILL.md
  checkout-flow/
    SKILL.md
    scripts/
      rollback.sh
  api-conventions/
    SKILL.md
```

The `skills/` directory is the only thing Lorex manages. You can add a `README.md`, CI workflows, or anything else to the repo alongside it.
