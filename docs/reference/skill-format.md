# Skill Format

A lorex skill lives at `.lorex/skills/<name>/SKILL.md`.

---

## Full example

```markdown
---
name: checkout-flow
description: Checkout lifecycle, payment edge cases, and rules every contributor must follow
version: 1.2.0
tags: checkout, payments, orders
owner: commerce-team
---

# Checkout Flow

## Overview

The checkout module handles the full lifecycle from cart to order confirmation.
Payment is processed synchronously; fulfilment is queued asynchronously.

## Rules

- Always validate stock before creating a payment intent.
- Never expose raw Stripe errors to the client — map them to user-friendly messages.
- The `order_id` must be idempotency-keyed on the Stripe request.

## Edge cases

- Free orders (zero total) skip payment entirely — go straight to fulfilment.
- Digital-only orders skip the shipping address step.
```

---

## Frontmatter fields

| Field | Required | Description |
| :--- | :--- | :--- |
| `name` | **Yes** | Unique identifier. Must match the parent directory name. |
| `description` | **Yes** | One sentence describing the skill. Used by Cursor/Roo rule wrappers and by `lorex install --recommended` matching. |
| `version` | No | Semantic version string. Defaults to `1.0.0`. Used by `lorex sync` to detect updates. |
| `tags` | No | Comma-separated list. Used for `--recommended` matching against the repo slug (`owner/repo`) or folder name. |
| `owner` | No | Team or person responsible for this skill. |

---

## Body content

The body is free-form Markdown. Write it as instructions for an AI agent:

- **Be prescriptive.** State rules, not observations. "Always validate tokens" beats "tokens should be validated".
- **Explain constraints.** Tell the agent *why* a rule exists so it can apply it correctly in edge cases.
- **Include examples.** Code snippets, diagrams, and data shapes help the agent reason concretely.
- **Stay focused.** One skill should cover one domain. Prefer multiple focused skills over one large document.

---

## Supporting files

A skill directory can contain more than `SKILL.md`:

```
.lorex/skills/deployment/
  SKILL.md
  scripts/
    deploy.sh
    rollback.sh
  examples/
    config.staging.yaml
```

Agents that load the entire skill directory will have access to these files. They are also published to the registry alongside `SKILL.md` when you run `lorex publish`.

---

## Legacy format

Lorex still reads `skill.md` (lowercase) for backward compatibility. New skills are always scaffolded as `SKILL.md`.
