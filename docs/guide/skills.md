# Working with Skills

A skill is a Markdown document at `.lorex/skills/<name>/SKILL.md` that teaches an AI agent something specific about your project. This page covers the full lifecycle: creating, authoring, updating, and removing skills.

---

## Creating a skill

Always use `lorex create` to start a new skill. It creates the directory, writes a frontmatter template, registers the skill in `lorex.json`, and immediately projects it into your configured agents — all in one step.

```bash
lorex create <name> [options]
```

### Interactive mode

Running `lorex create` with no arguments opens a guided prompt:

```
Create a new skill

Skill name (kebab-case, e.g. auth-overview): auth-logic
Short description: Authentication flows, token validation, and session rules
Tags (comma-separated, e.g. auth, security): auth, security
Owner (team or individual name): platform-team
```

### Non-interactive mode

Pass everything as flags to skip the prompts:

```bash
lorex create auth-logic \
  --description "Authentication flows, token validation, and session rules" \
  --tags "auth,security" \
  --owner "platform-team"
```

Shorthand flags: `-d` for `--description`, `-t` for `--tags`, `-o` for `--owner`.

### What gets created

```
.lorex/skills/auth-logic/
  SKILL.md
```

The file starts with YAML frontmatter and a placeholder body:

```markdown
---
name: auth-logic
description: Authentication flows, token validation, and session rules
version: 1.0.0
tags: auth, security
owner: platform-team
---

# auth-logic

> Authentication flows, token validation, and session rules

<!-- Author this skill using your AI coding agent. -->
<!-- Describe architecture, constraints, flows, patterns, pitfalls. -->
```

Lorex also adds the skill to `.lorex/lorex.json` and refreshes adapter projections, so it is immediately visible to your AI agents.

---

## Authoring the skill content

### Let your AI write it

Since `lorex create` already projects the skill, your AI can see it immediately. Open your agent and ask:

> Fill in `.lorex/skills/auth-logic/SKILL.md`. Read the authentication module in `src/auth/` and document: how tokens are issued and validated, the session lifecycle, rules about token expiry, and anything an AI should know before modifying this code.

After the AI fills in the file, run `lorex refresh` to push the updated content to all adapters.

### Write it yourself

Open `.lorex/skills/<name>/SKILL.md` in your editor and replace the placeholder content. See [Skill Format](/reference/skill-format) for field details and writing tips.

---

## Updating a skill

Edit `.lorex/skills/<name>/SKILL.md` directly — no special command needed. After saving, run:

```bash
lorex refresh
```

This re-projects the skill into all adapter locations. For symlink-based adapters (Claude, Copilot, Codex, etc.) the content is read through the symlink anyway, so agents that re-read the file will see changes immediately without a refresh. Run refresh explicitly if you also use Cursor or Roo (which use generated files) or after adding a new adapter.

---

## Listing what's installed

```bash
lorex status
```

Shows every installed skill with its link type and path:

| Link type | Meaning |
| :--- | :--- |
| `local` (yellow) | A real directory you authored locally |
| `symlink` (green) | Installed from a registry; points to the registry cache |
| `missing` (red) | Directory doesn't exist; run `lorex refresh` |
| `broken symlink` (red) | Registry cache is gone; run `lorex sync` to restore |

---

## Removing a skill

```bash
lorex uninstall auth-logic
```

This:
1. Deletes `.lorex/skills/auth-logic/` (or the symlink if it is registry-backed)
2. Removes `auth-logic` from `lorex.json`
3. The next `lorex refresh` will remove it from all adapter projections

To remove multiple skills at once:

```bash
lorex uninstall auth-logic api-conventions
```

To remove everything interactively:

```bash
lorex uninstall
```

Lorex opens a multi-select picker showing all installed skills.

To remove everything without prompts:

```bash
lorex uninstall --all
```

---

## Skill directory contents

A skill folder can contain more than just `SKILL.md`. Supporting files are available to agents that read the full skill directory and are published to the registry alongside `SKILL.md`.

```
.lorex/skills/deployment/
  SKILL.md              ← required
  scripts/
    deploy.sh
    rollback.sh
  examples/
    config.staging.yaml
  docs/
    architecture.png
```

---

## Scenario: documenting a payment module

Here is an end-to-end example.

**1. Create the skill:**

```bash
lorex create checkout-flow \
  -d "Checkout lifecycle, payment rules, and edge cases" \
  -t "checkout,payments" \
  -o "commerce-team"
```

**2. Ask your AI to fill it in:**

> Fill in `.lorex/skills/checkout-flow/SKILL.md`. Read `src/checkout/` and document: the full order lifecycle from cart to fulfilment, payment intent creation and idempotency rules, how free orders are handled, and any invariants that must never be broken.

**3. Refresh:**

```bash
lorex refresh
```

**4. From now on**, any AI agent working in this project will read the skill before touching checkout code — because Lorex projected it into every configured adapter location.

**5. Later, share it with your team:**

```bash
lorex publish checkout-flow
```
