# How the Linux Tray Icon Works (D-Bus StatusNotifierItem)

On Windows, YaHud's tray icon is a WinForms `NotifyIcon`. Linux has no
equivalent API — the modern desktop convention is a D-Bus protocol called
**StatusNotifierItem** (SNI), where the *application* hosts the icon as a D-Bus
object and the *panel* connects to it and draws it.

YaHud implements that protocol by hand on top of
[`Tmds.DBus.Protocol`](https://github.com/tmds/Tmds.DBus). This document
explains why, how the pieces fit together, and the traps to avoid when changing
the code.

## Files

All Linux tray code lives in [`R3E.Tray/Linux/`](../R3E.Tray/Linux/) behind
`#if LINUX`:

| File | Responsibility |
|------|----------------|
| [`TrayService.cs`](../R3E.Tray/Linux/TrayService.cs) | `IHostedService` that connects to the session bus, registers the handlers, and registers the item with the watcher. |
| [`StatusNotifierItemHandler.cs`](../R3E.Tray/Linux/StatusNotifierItemHandler.cs) | Serves `org.kde.StatusNotifierItem` at `/StatusNotifierItem` — the icon itself. |
| [`DbusTrayMenuHandler.cs`](../R3E.Tray/Linux/DbusTrayMenuHandler.cs) | Serves `com.canonical.dbusmenu` at `/MenuBar` — the right-click menu (one item: **Quit**). |
| [`PngPixelReader.cs`](../R3E.Tray/Linux/PngPixelReader.cs) | Minimal PNG decoder that turns the embedded icon into the ARGB32 byte array SNI wants. |

Platform selection happens in
[`TrayServiceExtensions.cs`](../R3E.Tray/TrayServiceExtensions.cs), called from
`Program.cs` via `builder.Services.AddTrayService()`:

```csharp
#if WINDOWS
    services.AddHostedService<Windows.TrayService>();
#elif LINUX
    services.AddHostedService<Linux.TrayService>();
#endif
```

The `WINDOWS` / `LINUX` constants are set in
[`R3E.Tray.csproj`](../R3E.Tray/R3E.Tray.csproj) from `'$(OS)'`, which has an
important consequence covered in [Testing](#testing).

## Why hand-rolled D-Bus

YaHud publishes as a self-contained single-file binary that has to work inside an
AppImage on an unknown distro. That rules out anything with native dependencies.
`Tmds.DBus.Protocol` is pure managed C# and speaks the wire protocol directly, so
the tray adds **zero native dependencies** — no GTK, no libappindicator, nothing
to bundle or `dlopen`.

Alternatives that were considered and rejected:

- **`Gtk.StatusIcon` via GtkSharp** — deprecated in GTK3, removed in GTK4, and
  drags in native GTK.
- **`NotificationIcon.NET`** — ships native GTK3 blobs.
- **`Olbrasoft.SystemTray`** — pre-release, effectively unmaintained.
- **Avalonia's tray support** — far too heavy for what is otherwise a headless
  web host.

> **Do not downgrade `Tmds.DBus.Protocol` below 0.91.1** —
> [GHSA-xrw6-gwf8-vvr9](https://github.com/advisories/GHSA-xrw6-gwf8-vvr9) is a
> high-severity advisory against earlier versions. The project currently pins
> 0.94.2.

## How SNI works

Unlike the old XEmbed system tray (where the app handed the panel a window to
embed), SNI inverts the relationship: the application publishes an object on the
session bus describing the icon, and the panel reads it. Nothing is ever drawn by
YaHud.

```
                      D-Bus session bus
  ┌──────────────────────────┐        ┌───────────────────────────────┐
  │ YaHud (:1.42)            │        │ Panel / desktop shell         │
  │                          │        │ (Plasma, Cinnamon, Xfce,      │
  │ /StatusNotifierItem      │        │  AppIndicator extension, …)   │
  │   org.kde.               │        │                               │
  │   StatusNotifierItem     │        │ org.kde.StatusNotifierWatcher │
  │                          │        │   /StatusNotifierWatcher      │
  │ /MenuBar                 │        │                               │
  │   com.canonical.dbusmenu │        │                               │
  └──────────────────────────┘        └───────────────────────────────┘
              │                                        │
              │ 1. RegisterStatusNotifierItem(":1.42") │
              ├───────────────────────────────────────►│
              │                                        │
              │ 2. Properties.GetAll(StatusNotifierItem)
              │◄───────────────────────────────────────┤
              │    → Id, Title, Status, IconPixmap, Menu = /MenuBar
              │                                        │
              │ 3. dbusmenu GetLayout(0, -1, [])       │  (on right-click)
              │◄───────────────────────────────────────┤
              │    → { Quit }                          │
              │                                        │
              │ 4. Event / EventGroup(id=1, "clicked") │  (on click)
              │◄───────────────────────────────────────┤
              │    → lifetime.StopApplication()        │
```

### 1. Startup and registration

`TrayService.StartAsync` decodes the icon, constructs both handlers, opens the
session bus, and registers:

```csharp
connection = new DBusConnection(DBusAddress.Session!);
await connection.ConnectAsync();
connection.AddMethodHandler(sniHandler);   // /StatusNotifierItem
connection.AddMethodHandler(menuHandler);  // /MenuBar
await RegisterWithWatcher();
```

Registration is a single call to
`org.kde.StatusNotifierWatcher.RegisterStatusNotifierItem`, passing the
connection's own unique bus name (e.g. `:1.42`). The watcher then knows where to
find the item.

The whole body of `StartAsync` is wrapped in a `try/catch` that logs and swallows:

```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Linux tray initialization failed. The system tray icon will not be available.");
}
```

This is deliberate. Plenty of legitimate environments have no session bus and no
watcher at all — headless servers, bare X sessions, some containers. A missing
tray must never stop the HUD from running; the icon is a convenience, not a
feature the app depends on.

### 2. Icon properties

`StatusNotifierItemHandler` implements `IPathMethodHandler` and answers three
kinds of request at `/StatusNotifierItem`:

- **`org.freedesktop.DBus.Introspectable.Introspect`** — replies with a static
  XML blob describing the interface. Some panels introspect before touching
  properties, so this is not optional.
- **`org.freedesktop.DBus.Properties.Get` / `GetAll`** — the actual icon data.
- **`Activate` / `SecondaryActivate` / `ContextMenu` / `Scroll`** — accepted and
  acknowledged with an empty reply. YaHud has no left-click behaviour, but a
  panel that gets an error back may decide the item is broken.

The property values are fixed:

| Property | Value | Note |
|----------|-------|------|
| `Category` | `ApplicationStatus` | Places the icon in the normal application area. |
| `Id`, `Title` | `YaHud` | |
| `Status` | `Active` | `Passive` would hide the icon. |
| `IconName` | `""` | Empty — YaHud isn't installed into an icon theme, so it supplies raw pixels instead. |
| `IconPixmap` | `a(iiay)` with one 128×128 entry | See [Icon pixels](#icon-pixels). |
| `ItemIsMenu` | `false` | |
| `Menu` | `/MenuBar` | Object path of the dbusmenu, in the same connection. |

### 3. The menu

`DbusTrayMenuHandler` serves `com.canonical.dbusmenu` at `/MenuBar` with exactly
two nodes: root (`id 0`) and **Quit** (`id 1`).

`GetLayout` returns `u(ia{sv}av)` — a revision plus a recursive
`(id, properties, children)` tuple. The root carries
`children-display = "submenu"`; Quit carries `label = "Quit"` and
`enabled = true`. `GetGroupProperties` returns the same property sets in the flat
`a(ia{sv})` shape some panels prefer. `AboutToShow`/`AboutToShowGroup` report
"no update needed" because the menu is static.

> ### Clicks arrive two different ways
>
> dbusmenu defines **both** a single `Event(isvu)` method and a batched
> `EventGroup(a(isvu))` method. Which one a desktop uses is not something you can
> pick: Cinnamon and Xfce use `EventGroup`, others use `Event`.
>
> **Both handlers must invoke the quit callback.** This shipped broken once —
> `HandleEventGroup` acknowledged the call but never acted on it, so on Linux
> Mint the Quit menu item did precisely nothing while working fine elsewhere.
> `scripts/test-tray-dbus.sh` now asserts both paths.

Quitting itself goes through `IHostApplicationLifetime`, passed in as a callback
so the D-Bus layer stays unaware of the host:

```csharp
var menuHandler = new DbusTrayMenuHandler(() => lifetime.StopApplication());
```

`StopApplication()` only signals the host's stopping token — it returns
immediately rather than blocking until shutdown completes — so the D-Bus reply
still gets written either way. `HandleEventGroup` nonetheless replies *before*
calling it, which is the safer order to follow if that ever changes: a handler
that tears the connection down before replying leaves the panel waiting on a
reply that never arrives.

### Icon pixels

SNI's `IconPixmap` is `a(iiay)` — an array of (width, height, bytes) where the
bytes are **ARGB32 in network byte order (big-endian)**.

The icon ships as an embedded resource, `R3E.Tray.Assets.trayfavicon.png`
(128×128 RGBA). Getting from PNG to ARGB32 normally means an imaging library;
`System.Drawing.Common` is Windows-only these days and ImageSharp/SkiaSharp are
sizeable dependencies for one 128×128 image. So `PngPixelReader` does it
directly — roughly 200 lines: walk the chunks for `IHDR` and `IDAT`, inflate the
concatenated IDAT payload with `DeflateStream` (skipping the 2-byte zlib header),
reverse the per-scanline PNG filters (including Paeth), then reorder each pixel
to `A, R, G, B`.

It is intentionally minimal: 8-bit depth only, colour types 0/2/4/6
(grayscale, RGB, gray+alpha, RGBA), and it ignores interlacing and palettes. That
covers the icon the repo ships. **If you replace `trayfavicon.png`, save it as
8-bit non-interlaced** — a 16-bit, palettised, or Adam7-interlaced PNG throws at
startup, which the `try/catch` turns into "tray unavailable" with a logged
exception rather than a crash.

## Tmds.DBus.Protocol gotchas

These are the things that will bite you when editing the handlers. Two of them
fail at *runtime*, not compile time.

**1. `MessageWriter` is a `ref struct`.** It cannot be captured, boxed, or live
across an `await`, and a `using` local cannot be passed by `ref`. So where a
writer is handed to a helper, it's disposed manually:

```csharp
var writer = context.CreateReplyWriter("v");
try
{
    WritePropertyAsVariant(ref writer, propertyName);
    context.Reply(writer.CreateMessage());
}
finally
{
    writer.Dispose();
}
```

The same constraint shapes `TrayService.CreateRegisterMessage`: the message is
built and the writer disposed in a separate synchronous method, and only the
resulting `MessageBuffer` is carried into the `await`.

**2. Variant generic arguments must be D-Bus types.** `Array<byte>`, not
`byte[]`; `Struct<...>`, not a tuple. Passing a CLR array compiles fine and then
throws when the message is marshalled:

```csharp
// Correct — Array<byte>, even though the source data is a byte[]
var pixmapArray = new Array<Struct<int, int, Array<byte>>>
{
    new(iconWidth, iconHeight, new Array<byte>(new List<byte>(iconArgbData)))
};
```

**3. Reading arrays of structs re-aligns for you.** For `a(isvu)`,
`ReadArrayStart(DBusType.Struct)` plus `while (reader.HasNext(arrayEnd))` handles
struct alignment — no manual `AlignStruct()`. Every field must still be consumed
in order, including ones you don't care about, or the reader desynchronises:

```csharp
var id = reader.ReadInt32();
var eventId = reader.ReadString();
reader.ReadVariantValue(); // data (unused, but must be consumed)
reader.ReadUInt32();       // timestamp (unused, but must be consumed)
```

**4. Answer unknown members explicitly.** `context.ReplyUnknownMethodError()` for
unknown methods and an `UnknownInterface` error for unknown interfaces. Silently
returning without replying leaves the panel waiting for a reply that never comes.

## Testing

### The Windows-build trap

`R3E.Tray.csproj` defines `LINUX` only when `'$(OS)' == 'Unix'`, and
`Tmds.DBus.Protocol` is referenced only there. **A green Windows build proves
nothing about this code** — it isn't compiled at all. That is why
[`pr-validation.yml`](../.github/workflows/pr-validation.yml) builds a
`[windows-latest, ubuntu-latest]` matrix. Don't remove the Ubuntu leg.

### Protocol test script

[`scripts/test-tray-dbus.sh`](../scripts/test-tray-dbus.sh) exercises the whole
D-Bus surface headlessly, with no desktop panel involved. It starts a private
session bus via `dbus-run-session`, stands up a Python
`StatusNotifierWatcher` stub, launches YaHud, and asserts:

1. `RegisterStatusNotifierItem` is called, capturing the bus name.
2. `/StatusNotifierItem` introspects successfully.
3. `Properties.GetAll` marshals and includes `IconPixmap` and `Menu` — this is
   what catches the `Array<byte>` mistake.
4. `GetLayout` returns the Quit item.
5. Quit works via `EventGroup` — and, against a fresh instance, via `Event`.

```bash
# On Linux / WSL, after publishing or building for linux-x64
scripts/test-tray-dbus.sh ./publish/R3E.YaHud-linux-x64/YaHud
# or point it at the .dll and it will use `dotnet`
scripts/test-tray-dbus.sh ./R3E.YaHud/bin/Debug/net10.0/R3E.YaHud.dll
```

Dependencies: `dbus` (for `dbus-run-session`), `busctl` (systemd),
`python3-dbus`, `python3-gi`. The script checks for all of them up front.

### Manual inspection

With YaHud running on a real session bus:

```bash
# Find the item's bus name
busctl --user list | grep -i yahud

# Read every SNI property (replace :1.42)
busctl --user call :1.42 /StatusNotifierItem \
  org.freedesktop.DBus.Properties GetAll s org.kde.StatusNotifierItem

# Dump the menu layout
busctl --user -- call :1.42 /MenuBar com.canonical.dbusmenu GetLayout iias 0 -1 0

# Trigger Quit without touching the panel
busctl --user call :1.42 /MenuBar com.canonical.dbusmenu Event isvu 1 clicked s "" 0
```

`monitor` is useful when a panel misbehaves — it shows exactly which methods that
desktop actually calls:

```bash
dbus-monitor --session "path=/StatusNotifierItem"
```

## Desktop support

| Desktop | Works | Note |
|---------|-------|------|
| KDE Plasma | ✅ | SNI is a KDE protocol; native support. |
| Linux Mint (Cinnamon) | ✅ | Uses `EventGroup` for clicks. |
| Xfce | ✅ | Uses `EventGroup` for clicks. |
| LXQt | ✅ | |
| Ubuntu GNOME | ✅ | Ubuntu ships AppIndicator support by default. |
| Vanilla GNOME | ⚠️ | Needs the [AppIndicator extension](https://extensions.gnome.org/extension/615/appindicator-support/). |
| No session bus / headless | ➖ | Registration fails, is logged, and the HUD runs without an icon. |

## Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| No icon; log says "Linux tray initialization failed" | No session bus, or no `StatusNotifierWatcher` on it. Read the logged exception. |
| No icon; no error logged | Registration succeeded but nothing is displaying items — vanilla GNOME without the AppIndicator extension. |
| Icon slot is present but blank | `IconPixmap` failed to marshal, or `PngPixelReader` produced nothing usable. Check `Properties.GetAll` with `busctl`. |
| Right-click menu is empty | `GetLayout` reply is malformed. Call it directly with `busctl` and compare against the `u(ia{sv}av)` signature. |
| Quit does nothing on one specific desktop | That desktop delivers clicks through the path you didn't wire up. Both `Event` and `EventGroup` must call `quitCallback`. |
| Panel logs a timeout when Quit is clicked | A handler returned without replying, or tore the connection down before the reply was written. |

## References

- [StatusNotifierItem specification](https://www.freedesktop.org/wiki/Specifications/StatusNotifierItem/StatusNotifierItem/)
- [com.canonical.dbusmenu specification](https://github.com/AyatanaIndicators/libdbusmenu/blob/master/libdbusmenu-glib/dbus-menu.xml)
- [D-Bus specification — type system and marshalling](https://dbus.freedesktop.org/doc/dbus-specification.html)
- [Tmds.DBus](https://github.com/tmds/Tmds.DBus)
