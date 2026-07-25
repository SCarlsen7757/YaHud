# How the YaHud AppImage Is Built

The Linux release ships as `YaHud-v{version}-x86_64.AppImage` — a single
executable file that runs on any reasonably modern x86_64 distribution with no
installation, no package manager, and no .NET runtime on the host.

This document explains what goes into that file, how CI assembles it, and how to
reproduce or debug the build locally.

## Why an AppImage

`R3E.YaHud` is already published self-contained (`--self-contained true
-p:PublishSingleFile=true`), so the plain `R3E.YaHud-linux-x64` zip works too.
The AppImage adds three things on top of it:

- **One file.** Nothing to extract, no directory to keep tidy.
- **Desktop integration.** The bundled `.desktop` entry and icon let
  desktop environments and tools like [AppImageLauncher](https://github.com/TheAssassin/AppImageLauncher)
  show YaHud in the application menu with a proper name and icon.
- **A stable working directory.** ASP.NET Core resolves its content root
  relative to the process working directory. Launching the plain binary from an
  unrelated directory can break static-asset resolution; the AppImage's `AppRun`
  fixes the working directory before exec (see below).

## The pieces

### Source files (`packaging/appimage/`)

| File | Role |
|------|------|
| `AppRun` | Entry point. The AppImage runtime executes this after mounting the bundle. |
| `YaHud.desktop` | Desktop entry. Required by `appimagetool`; supplies name, icon, and category. |
| `yahud.png` | 128×128 RGBA application icon. Copied twice — as `yahud.png` and as `.DirIcon`. |

`AppRun` is the only interesting one:

```sh
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
cd "$HERE/usr/bin" || exit 1
exec ./YaHud "$@"
```

`$0` inside a running AppImage points into the read-only SquashFS mount (something
like `/tmp/.mount_YaHudXXXXXX/AppRun`), so `readlink -f` plus `dirname` yields the
mounted AppDir root. Changing into `usr/bin` before `exec` is what gives ASP.NET a
predictable content root. `exec` replaces the shell rather than forking, so signals
and the exit code pass straight through to YaHud — `Ctrl+C` and
`systemd`-style termination behave as expected. `"$@"` forwards any CLI arguments.

> **Line endings matter.** `.gitattributes` pins `AppRun` and `*.desktop` to `LF`
> (`packaging/appimage/AppRun text eol=lf`). A `CRLF` shebang makes the kernel
> look for an interpreter literally named `/bin/sh\r`, and the AppImage fails at
> launch with a confusing "no such file or directory". Don't remove those rules.

### AppDir layout

`appimagetool` takes a directory tree (an "AppDir") and turns it into the final
file. CI assembles this shape:

```
AppDir/
├── AppRun              # executable entry point (mode 755)
├── YaHud.desktop       # desktop entry
├── yahud.png           # icon referenced by the desktop entry (Icon=yahud)
├── .DirIcon            # same image; thumbnailers/file managers read this
└── usr/
    └── bin/
        ├── YaHud       # self-contained single-file publish output
        └── ...         # remaining publish artifacts (wwwroot, etc.)
```

The `Icon=yahud` key in the desktop entry resolves against `yahud.png` at the
AppDir root — extensionless by convention, so keep the key and the filename in
sync if the icon is ever renamed.

## The CI build

The `build-yahud-appimage` job in
[`.github/workflows/build-artifacts.yml`](../.github/workflows/build-artifacts.yml)
runs on `ubuntu-latest` and does four things.

**1. Publish into the AppDir.**

```bash
dotnet publish ./R3E.YaHud/R3E.YaHud.csproj -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=true -o ./AppDir/usr/bin
```

Publishing straight into `AppDir/usr/bin` avoids a separate copy step. `fetch-depth: 0`
on the checkout is required so GitVersion.MsBuild can compute the assembly version
from Git history.

**2. Assemble the AppDir.**

```bash
install -m 755 packaging/appimage/AppRun ./AppDir/AppRun
cp packaging/appimage/YaHud.desktop ./AppDir/
cp packaging/appimage/yahud.png ./AppDir/
cp packaging/appimage/yahud.png ./AppDir/.DirIcon
```

`install -m 755` rather than `cp` because the execute bit on `AppRun` is
mandatory — a non-executable `AppRun` produces an AppImage that mounts and then
immediately fails.

**3. Fetch pinned tooling and verify checksums.**

```bash
wget -q "https://github.com/AppImage/appimagetool/releases/download/${APPIMAGETOOL_VERSION}/appimagetool-x86_64.AppImage"
wget -q "https://github.com/AppImage/type2-runtime/releases/download/${RUNTIME_VERSION}/runtime-x86_64"
echo "${APPIMAGETOOL_SHA256}  appimagetool-x86_64.AppImage" | sha256sum -c -
echo "${RUNTIME_SHA256}  runtime-x86_64" | sha256sum -c -
```

Both the tool and the runtime are pinned to an exact version *and* an exact
SHA-256, set as `env` on the step. Downloading a build tool from the network on
every release is a supply-chain risk; the checksum check is what makes the build
fail loudly instead of silently packaging something unexpected.

**4. Build the AppImage.**

```bash
chmod +x appimagetool-x86_64.AppImage
ARCH=x86_64 ./appimagetool-x86_64.AppImage --appimage-extract-and-run \
  --runtime-file runtime-x86_64 ./AppDir ./YaHud-v${VERSION}-x86_64.AppImage
```

Three flags carry weight here:

- `ARCH=x86_64` — `appimagetool` refuses to guess the target architecture and
  errors out without it.
- `--appimage-extract-and-run` — `appimagetool` is itself an AppImage, and
  mounting one needs FUSE. GitHub runners don't reliably provide `libfuse2`, so
  this makes it self-extract to a temp directory instead.
- `--runtime-file runtime-x86_64` — uses the runtime we downloaded and verified
  rather than letting `appimagetool` fetch one at build time. This is what keeps
  the build hermetic and the checksum meaningful.

The resulting file is uploaded as the `R3E.YaHud-appimage` artifact, and
[`create-release.yml`](../.github/workflows/create-release.yml) attaches
`./artifacts/**/*.AppImage` to the GitHub release.

## Building it locally

Requires a Linux machine (or WSL) with the .NET 10 SDK and `wget`. From the repo
root:

```bash
VERSION=local

# 1. Publish into the AppDir
rm -rf ./AppDir
dotnet publish ./R3E.YaHud/R3E.YaHud.csproj -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=true -o ./AppDir/usr/bin

# 2. Assemble
install -m 755 packaging/appimage/AppRun ./AppDir/AppRun
cp packaging/appimage/YaHud.desktop ./AppDir/
cp packaging/appimage/yahud.png ./AppDir/
cp packaging/appimage/yahud.png ./AppDir/.DirIcon

# 3. Tooling (versions must match build-artifacts.yml)
wget -q https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage
wget -q https://github.com/AppImage/type2-runtime/releases/download/20251108/runtime-x86_64
chmod +x appimagetool-x86_64.AppImage

# 4. Package
ARCH=x86_64 ./appimagetool-x86_64.AppImage --appimage-extract-and-run \
  --runtime-file runtime-x86_64 ./AppDir ./YaHud-v${VERSION}-x86_64.AppImage

# 5. Run
chmod +x ./YaHud-v${VERSION}-x86_64.AppImage
./YaHud-v${VERSION}-x86_64.AppImage
```

Then browse to <http://localhost:5019/>.

Useful inspection commands:

```bash
# Unpack the finished AppImage to check the tree that actually shipped
./YaHud-v${VERSION}-x86_64.AppImage --appimage-extract
ls -l squashfs-root/

# Confirm AppRun's shebang has no CR
head -c 20 packaging/appimage/AppRun | od -c | head -2
```

## Bumping the pinned versions

1. Pick the new release of [appimagetool](https://github.com/AppImage/appimagetool/releases)
   and/or [type2-runtime](https://github.com/AppImage/type2-runtime/releases).
2. Compute the checksum of the exact asset:
   ```bash
   sha256sum appimagetool-x86_64.AppImage runtime-x86_64
   ```
3. Update `APPIMAGETOOL_VERSION` / `APPIMAGETOOL_SHA256` and
   `RUNTIME_VERSION` / `RUNTIME_SHA256` in the `Build AppImage` step of
   `build-artifacts.yml`, plus the versions in the local-build snippet above.
4. Verify the produced AppImage actually launches before merging — a checksum
   match only proves you downloaded what you expected, not that it works.

## Troubleshooting

| Symptom | Cause |
|---------|-------|
| `AppRun: not found` or `no such file or directory` on launch | `AppRun` has CRLF line endings, or lost its execute bit. |
| `Error: no such file or directory` for a static asset, blank HUD page | Working directory isn't `usr/bin`; check `AppRun` wasn't modified. |
| `AppImages require FUSE to run` | Host lacks `libfuse2`. Either install it or run with `--appimage-extract-and-run`. |
| `appimagetool` exits complaining about architecture | `ARCH=x86_64` not set in the environment. |
| `sha256sum -c` fails in CI | Upstream re-tagged a release, or the pinned version was bumped without updating the checksum. Recompute; don't delete the check. |
| Tray icon missing when run from the AppImage | Unrelated to packaging — see the tray icon notes in the [README](../README.md#linux-via-relay). |

## See also

- [`packaging/appimage/`](../packaging/appimage/) — the source files
- [`.github/workflows/build-artifacts.yml`](../.github/workflows/build-artifacts.yml) — the `build-yahud-appimage` job
- [AppImage type 2 specification](https://github.com/AppImage/AppImageSpec/blob/master/draft.md)
