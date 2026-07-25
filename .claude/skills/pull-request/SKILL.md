---
name: pull-request
description: Open or update a GitHub pull request for YaHud. Use whenever creating a PR, pushing a branch for review, or editing an existing PR's title, body or labels — every PR must carry at least one label or it is mislabelled in the published release notes.
---

# Opening a pull request

## Labels are mandatory

**Never open a PR without at least one label.** Add them in the same step as
creating the PR, not afterwards.

`.github/release.yml` groups auto-generated release notes by PR label, and
`.github/workflows/create-release.yml` consumes that grouping. A PR with no
label does not fail any check — it silently falls through to the `*` catch-all
and appears under "🔍 Other Changes" in the release notes users read. That is
why this is worth getting right at open time: nothing will remind you later.

If you have opened a PR and not yet labelled it, fix it immediately:

```bash
gh pr edit <number> --add-label enhancement
```

## Choosing labels

Read the current list rather than guessing — labels get added over time:

```bash
gh label list --limit 60
```

At time of writing:

| Label | Use for |
|-------|---------|
| `enhancement` | New feature or improvement |
| `widget` | New HUD widget, or changes to an existing one |
| `bug` | Fixes broken behaviour |
| `documentation` | README, docs/, CONTRIBUTING, or comment-only changes |
| `packaging` | AppImage, archives, release artifacts |
| `refactor` | Internal restructuring, no behaviour change |
| `chore` | Maintenance with no behaviour change: cleanup, tooling, config |
| `ci` | GitHub Actions workflows, build, or versioning setup |
| `dependencies`, `.NET` | Dependency bumps, runtime or target framework upgrades |
| `windows`, `linux` | Changes specific to one OS |
| `release` | The develop → main release PR (excluded from release notes) |

Apply as many as genuinely apply — most PRs earn two or three. A feature that
also updates the README is `enhancement` + `documentation`.

Categories in `.github/release.yml` are matched **top to bottom and the first
match wins**, so that PR is listed under "🚀 Features", not Documentation.
Label for what the change *is* first, then the supporting labels. Reserve
`windows`/`linux` for genuinely OS-specific work — a cross-platform change
that happens to touch both does not need either.

## Creating the PR

Base branch is `develop` (see CONTRIBUTING.md); only release PRs target `main`.

> Cutting a release (`develop` → `main`) has extra requirements that fail
> silently — the version increment goes in the PR title, and the resulting
> release is a draft. Use the `cut-release` skill for those, not this one.

```bash
gh pr create --base develop \
  --title "..." \
  --body "..."
gh pr edit <number> --add-label <label> [--add-label <label>]
```

Then confirm the labels actually landed:

```bash
gh pr view <number> --json labels --jq '.labels[].name'
```

## Body content

- Open with `Closes #<issue>` when the PR resolves an issue.
- State what was verified and what was not. This repo has **no test project**,
  so verification is manual — say which paths you actually exercised and which
  you could not (e.g. anything needing RaceRoom running).
- Call out signature or behaviour changes reviewers would otherwise have to
  diff for themselves.
