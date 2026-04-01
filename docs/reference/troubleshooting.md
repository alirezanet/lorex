# Troubleshooting

## Skill not appearing in an agent

Run `lorex refresh` to re-project skills into all configured adapter locations.

If the skill still does not appear, check `lorex status` to confirm the skill is listed as installed and that the adapter is configured.

---

## Symlinks not working on Windows

Lorex requires symlink creation for registry installs and native skill projections.

**Fix:** Enable [Developer Mode](https://learn.microsoft.com/en-us/windows/apps/get-started/enable-your-device-for-development) in Windows Settings, or run the terminal as Administrator.

Lorex will detect this automatically and offer to open the Developer Mode settings page.

---

## Gemini not loading lorex skills

Confirm that `.gemini/settings.json` exists and contains:

```json
{
  "context": {
    "loadFromIncludeDirectories": true,
    "includeDirectories": [".lorex/skills/your-skill"],
    "fileName": ["SKILL.md", "skill.md"]
  }
}
```

Run `lorex refresh` to regenerate the settings file.

---

## `lorex publish` opens a branch instead of pushing directly

The registry policy is `pull-request`. This is intentional — skills must go through a PR review before becoming available to all users.

Check the current policy with `lorex status`. To change it, run `lorex registry`.

---

## `lorex publish` is blocked

The registry policy is `read-only`. Contact the registry owner to change the policy, or run `lorex registry` if you have write access to the registry.

---

## `lorex registry` opens a branch instead of changing the policy immediately

The current registry policy is `pull-request`, so the policy change itself requires a review. Merge the generated PR branch, then run `lorex sync` to apply the new policy.

---

## Published skill still shows as local after publish

Run `lorex status`. Registry-backed installs should show as `symlink`. If the skill still shows as a local directory after a `direct` publish, run `lorex sync` to resync the registry cache and reinstall the symlink.

---

## `lorex init` says the project is already initialized

`lorex init` is safe to re-run. It updates the configuration without losing installed skills. If you want to change the registry or adapters, run it again and select new values.

---

## Registry cache errors

If the registry cache is in a bad state, delete it and re-run `lorex sync`:

```bash
# The cache is stored in your home directory
# Windows:  %USERPROFILE%\.lorex\cache\
# Unix:     ~/.lorex/cache/

lorex sync
```

---

## `lorex` command not found after installation

Make sure the directory containing the `lorex` binary is in your `PATH`.

- **Quick install:** the install script adds the binary to `~/.local/bin` (Unix) or the user's local `bin` folder (Windows). Restart your terminal or re-source your shell profile.
- **.NET tool:** ensure `~/.dotnet/tools` is in your `PATH`.
