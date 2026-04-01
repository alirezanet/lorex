# Registry Policy

A registry policy controls how contributors publish skills to a shared registry. The policy is stored in `/.lorex-registry.json` at the root of the registry repository.

---

## Publish modes

### `pull-request` (recommended for teams)

```json
{
  "publishMode": "pull-request",
  "baseBranch": "main",
  "prBranchPrefix": "lorex/"
}
```

`lorex publish` creates a new branch (e.g. `lorex/auth-logic-20250401120000`), copies the skill, commits, pushes, and prints a pull request URL. The skill stays as a local directory until the PR is merged and `lorex sync` is run.

Best for: teams where skills should be reviewed before they become available to all users.

### `direct`

```json
{
  "publishMode": "direct",
  "baseBranch": "main",
  "prBranchPrefix": "lorex/"
}
```

`lorex publish` commits and pushes the skill straight to `baseBranch`. The local skill directory is immediately converted to a symlink pointing at the registry cache.

Best for: solo developers or small trusted teams.

### `read-only`

```json
{
  "publishMode": "read-only",
  "baseBranch": "main",
  "prBranchPrefix": "lorex/"
}
```

`lorex publish` is blocked entirely. Skills can be installed and synced but not contributed back.

Best for: curated registries where only maintainers publish.

---

## Policy fields

| Field | Default | Description |
| :--- | :--- | :--- |
| `publishMode` | `pull-request` | One of `direct`, `pull-request`, or `read-only` |
| `baseBranch` | `main` | The branch `lorex install`/`sync` reads from, and the base for PR branches |
| `prBranchPrefix` | `lorex/` | Prefix for branches created by `lorex publish` in `pull-request` mode |

---

## Changing the policy

```bash
lorex registry
```

This opens an interactive prompt. What happens next depends on the *current* mode:

- **Current mode is `direct`** — the new policy is committed and pushed immediately.
- **Current mode is `pull-request`** — Lorex creates a branch for the policy change and prints a PR URL. The existing policy remains in effect until that PR is merged and `lorex sync` is run.

---

## Where the policy is stored

The policy is stored in the registry repository, not in the project. All projects connected to the same registry share the same policy. Lorex caches a copy of the policy in `.lorex/lorex.json` and refreshes it on `lorex sync` and `lorex init`.
