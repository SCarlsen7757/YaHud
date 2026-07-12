---
name: verify
description: Build, launch, and drive YaHud (Blazor Server HUD) on Windows to verify widget changes at the browser surface.
---

# Verifying YaHud widget changes (Windows)

## Build & launch

```bash
dotnet build                      # from repo root; fails with MSB3027 if the app is still running (kill YaHud.exe first)
cd R3E.YaHud && dotnet run --no-build   # serves http://localhost:5000 (launchSettings.json)
```

Stop it with `Get-Process YaHud | Stop-Process -Force` before rebuilding — the running exe locks `bin\Debug\net10.0\YaHud.exe`.

## Drive it

- The HUD starts **locked** (no debugger attached): no buttons visible. Unlock with the global hotkey **Ctrl+Shift+Alt+L** — it's an OS-level SharpHook hook in the server process, so PowerShell `[System.Windows.Forms.SendKeys]::SendWait('^%+l')` triggers it even from a script (headed browser must not matter; the hook is global).
- After unlock, `.icon-button-stack` appears top-left: buttons are (0) gear = settings panel, (1) eye = widget visibility, (2) flask = **test mode** (feeds every widget its `UpdateWithTestData()`), (3) info.
- Widgets may be positioned half off-screen from stored settings; for screenshots set `document.getElementById('<elementId>').style.transform = 'translate(100px, 100px)'` (positioning is transform-based, cosmetic only).

## Browser automation

Playwright browsers are not installed; use `playwright-core` (npm i in a scratch dir) with the installed Chrome:

```js
const { chromium } = require('playwright-core');
const browser = await chromium.launch({ channel: 'chrome', headless: false });
```

Useful selectors:
- Widget host wrapper: `#<ElementId>` (e.g. `#FuelWidget`) — the `id` lives on the WidgetHost wrapper only; inner widget divs must not repeat it.
- Settings UI is master/detail inside `.overlay`: gear button opens `.settings-panel.settings-master` (widget list); clicking a widget row opens `.settings-panel.settings-detail` next to it.
- Open a widget's settings: click `.widget-item:has(.widget-name:text-is("<Widget Name>"))` in the master panel (rows are grouped under `.category-container` per category).
- Each setting in the detail panel is a sibling pair, not a wrapper: `.setting-row-label:text-is("<Label>") + .setting-row-input input` (number/checkbox/slider/color variants; sliders are `input[type=range].slider`).
- Enum settings and the view-mode control (detail `.panel-header`, options Easy/Normal/Expert) are the custom `EnumSelect`, **not** a native `<select>`: click `.custom-select-toggle`, then `.custom-select-option button:text-is("<Option>")`.
- Close: `.settings-master .close-btn`, or click the `.overlay` outside the panels. Do **not** click `.panel-footer` buttons to close — they are "Clear Settings" / "Clear All Settings".

## Gotchas

- Settings changes fire `NotifyPropertyChanged` → re-render only. Data lists are rebuilt in `Update()`/`UpdateWithTestData()`, which in test mode run **only on test-mode toggle** (no telemetry events without the game). Anything derived from both data *and* settings must be computed at render time, or it goes stale when a setting changes.
- Number inputs don't clamp typed values to the attribute Min/Max — widgets must tolerate out-of-range settings.
