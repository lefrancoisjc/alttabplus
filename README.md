# AltTabPlus

A Windows Alt+Tab replacement for a specific problem: when several windows
are snapped together (Snap Layouts / Snap Groups), the native switcher shows
them both as a group *and* as individual windows. That clutters the picker
instead of simplifying it, and Windows has no official setting to fix it
([Microsoft Community thread](https://techcommunity.microsoft.com/discussions/windows11/snapped-window-groups-and-individual-windows/3842896)).

AltTabPlus replaces the switcher entirely: a snapped group becomes **one
tile**, with a badge for how many windows it contains.

The UI follows the Windows display language (English by default, French when
the OS UI is French).

## How it works

Windows exposes no public API for “which windows belong to the same Snap
Group” — that bookkeeping lives in the shell (`twinui.pcshell.dll`) with no
documented interface. The project infers groups from geometry:

1. **`Hooking/KeyboardHook.cs`** — a low-level keyboard hook
   (`WH_KEYBOARD_LL`) intercepts Alt+Tab so it never reaches the native
   switcher. Same approach as tools like AltTabTerminator or GoToWindow;
   there is no clean official way to disable the built-in picker.
2. **`Windows/WindowEnumerator.cs`** — enumerates Alt-Tab-eligible windows
   (visible, no owner, not tool windows, not cloaked).
3. **`Grouping/SnapGroupDetector.cs`** — groups windows by monitor, links
   rectangles that touch edge-to-edge, and keeps a group only if it covers
   enough of the monitor work area (the geometric signature of a Snap Layout
   rather than two windows that happen to sit next to each other).
4. **`UI/SwitcherOverlayForm.cs` + `UI/DwmThumbnail.cs`** — the overlay,
   with live thumbnails via `DwmRegisterThumbnail` (the same mechanism as
   taskbar previews). The DWM destination must be the top-level window: a
   child Panel is rejected with `E_INVALIDARG`.
5. **`Switching/SwitchTarget.cs`** — one tile is either a single window or
   a whole group; `Activate()` brings every window in the group forward.

## Build

The project targets `net8.0-windows` (WinForms). `dotnet build` must run
**on Windows** — WinForms and the user32/dwmapi P/Invokes only work there:

```
dotnet build AltTabPlus.sln
```

or open `AltTabPlus.sln` in Visual Studio / Rider.

### Release

From the repo root, on Windows, with the .NET 8 SDK:

```
powershell -ExecutionPolicy Bypass -File scripts\build-release.ps1
powershell -ExecutionPolicy Bypass -File scripts\publish-release.ps1
```

`publish-release.ps1` writes a self-contained 64-bit exe (runtime included)
to `dist\`:

```
dist\AltTabPlus-1.0.0-win-x64\AltTabPlus.exe
dist\AltTabPlus-1.0.0-win-x64.zip
dist\AltTabPlus-1.0.0-win-x64.sha256
```

Options: `-Version 1.0.1`, `-Mode framework-dependent` (smaller, needs the
.NET 8 Desktop x64 runtime), `-SkipZip`.

The GitHub Actions workflow (`.github/workflows/ci.yml`) runs those scripts
on `windows-latest` for every push and PR. Zips show up under **Actions**.
A `v1.2.3` tag also publishes a GitHub Release.

For debug, run Visual Studio (or `dotnet run`) **as administrator** if you
need Alt+Tab inside elevated windows (classic UIPI: a non-admin keyboard
hook does not see keys destined for an admin window).

## Known limitations (v0)

- **Heuristic detection, not guaranteed.** Two windows resized by hand so
  they touch can be treated as a group; a Snap Group resized afterwards can
  be missed. The coverage threshold (`MinWorkAreaCoverage` in
  `SnapGroupDetector.cs`) is adjustable.
- **No installer** — it is a standalone executable for now. Settings live in
  `%LOCALAPPDATA%\AltTabPlus\settings.json` and the tray / settings window.
- **No advanced multi-desktop management** beyond the current filters.
- No telemetry, no network access — everything stays local.

## Possible roadmap

- [x] Persisted settings (alternate hotkey, launch at startup, taskbar)
- [x] DWM thumbnails for each window in a group (mini-grid in the tile)
- [ ] Detection threshold and theme in settings
- [ ] Explicit Snap Layout API if it ever becomes public, with the
      heuristic as fallback
- [ ] Installer (MSIX or a signed setup)
- [ ] Unit tests for `SnapGroupDetector` with simulated window geometry
      (no Win32 dependency required)

## Debug

The app has no console and no error UI, so everything is traced to
`%LOCALAPPDATA%\AltTabPlus\log.txt` (created on first launch): hook install,
window/group counts each time the switcher opens, HRESULT from
`DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties`, and each
activation attempt. If something fails, start there.

Two classic Win32 traps already handled in the code:

- **`GetWindowRect` lies slightly.** It includes an invisible resize-border
  that Windows 10/11 adds around most windows, so two snapped windows look
  like they overlap instead of touching. `WindowEnumerator` uses
  `DWMWA_EXTENDED_FRAME_BOUNDS` for the real visual rect.
- **`SetForegroundWindow` is ignored from a background process.** Windows
  blocks focus stealing; a direct call from our hook often does nothing.
  `ForegroundActivator` uses `AttachThreadInput`, the usual workaround for
  third-party Alt+Tab replacements.

## License

[PolyForm Noncommercial 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0)
— see [`LICENSE`](./LICENSE).

Personal, non-profit, and research use is allowed.
Commercialization (sale, paid licensing, inclusion in a paid product, etc.)
is reserved to Jean-Charles Lefrançois. Commercial use requires written
permission from the copyright holder.
