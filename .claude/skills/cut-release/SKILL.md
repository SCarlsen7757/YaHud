---
name: cut-release
description: Open the develop → main release PR for YaHud and publish the resulting release. Use whenever cutting a release, shipping develop to main, bumping the version, or publishing a draft release. Covers choosing the major/minor/patch increment, which is silent if you get it wrong.
---

# Cutting a YaHud release

A release is a PR from `develop` to `main`. Merging it triggers
`release-on-merge.yml`, which builds artifacts and creates a **draft** release.

Two steps here fail silently — no check goes red, you just get the wrong
result. They are steps 2 and 5.

## 1. Confirm develop is ready

```bash
git fetch origin
gh pr list --base develop --state open          # anything that should land first?
git log --oneline origin/main..origin/develop   # what is shipping
```

## 2. Decide the increment — this is the silent one

`main` is configured `increment: Patch`, so **the default is a patch release**.
To ship anything else, the marker goes in the **PR title**:

| You want | PR title | Result from `0.2.0` |
|----------|----------|---------------------|
| Patch | `Release 0.2.1` | `0.2.1` |
| Minor | `Release 0.3.0 +semver: minor` | `0.3.0` |
| Major | `Release 1.0.0 +semver: major` | `1.0.0` |

**The title is what matters, not the body.** This repo sets
`merge_commit_message: PR_TITLE`, so GitHub copies the PR title into the merge
commit body, which is where GitVersion reads it. Nothing else in the PR is read.

A marker can only *raise* the increment above `main`'s `increment: Patch`,
never lower it — `+semver: patch` is a no-op, and there is no way to suppress a
bump on a merge to `main`. See [Version Numbering](../../../CONTRIBUTING.md#-version-numbering)
for why.

Most `develop` → `main` releases are feature work and want `+semver: minor`.
Getting this wrong is not fatal but is annoying to undo: the version comes from
tags, so correcting it means deleting the pushed tag and release.

Check what you are about to get before opening the PR:

```bash
dotnet tool install --global GitVersion.Tool --version 6.4.*   # once
dotnet-gitversion /showvariable MajorMinorPatch
```

That reports the version for the *current* branch, so it will not reflect the
marker. Use it to confirm the baseline you are incrementing from.

## 3. Open the PR

Base is `main`, not `develop` — this is the one exception to the rule in the
`pull-request` skill.

```bash
gh pr create --base main --head develop \
  --title "Release 0.3.0 +semver: minor" \
  --body "..."
gh pr edit <number> --add-label release
```

Label it **`release`**. `.github/release.yml` excludes that label from the
generated notes; without it the release PR itself appears in its own changelog
alongside every PR it contains.

Body should summarise what is shipping — `git log --oneline origin/main..origin/develop`
is a good starting point.

## 4. Merge with a merge commit

```bash
gh pr merge <number> --merge
```

**Not `--squash`, not `--rebase`.** Squashing flattens the branch history that
GitVersion walks to find the version source and the `+semver` marker. Confirm
the marker survived:

```bash
git fetch origin main && git log -1 --format=%B origin/main
```

The body should contain your `+semver:` line. If it does not, the release will
be a patch — stop and fix it before the workflow finishes.

## 5. Publish the draft — the other silent one

`release-on-merge.yml` passes `draft: true`, so **the release and its tag are
not public until someone publishes them.** The workflow going green does not
mean the release shipped.

```bash
gh release list --limit 5              # confirm the draft and its version
gh release view v0.3.0                 # check notes and attached artifacts
gh release edit v0.3.0 --draft=false   # publish
```

(There is no `gh release publish` — clearing the draft flag is what publishes.)

Expect four artifacts (see [Artifacts Published](../../../CONTRIBUTING.md#artifacts-published)):
two Windows zips, one Linux zip, one AppImage. A release with fewer means a
build leg failed — investigate before publishing rather than shipping a partial
release.

## 6. Back-merge to develop

`main` now has a merge commit that `develop` lacks. Leaving them diverged makes
the next release's version calculation harder to reason about.

```bash
git checkout develop && git pull
git merge origin/main
git push origin develop
```

## Notes

- **Hotfixes skip all of this.** A `hotfix/*` branch PRs straight to `main` and
  needs no marker — patch is the default. Steps 5 and 6 still apply.
- **Tags must be plain `vMAJOR.MINOR.PATCH`.** A pre-release tag on `main` is
  not a valid version source, and GitVersion will silently recompute the same
  version on every subsequent release. This is what the `v0.1.0-463` through
  `v0.1.0-614` tags did.
- **`next-version:` in `GitVersion.yml`** overrides the markers while it is the
  active baseline. If a marker appears to do nothing, check whether that line is
  still set above the current tag.
