# Spacefleet-Heat-Death-BepInEx-Mods

BepInEx 5 mod pack for **Spacefleet - Heat Death** (Unity 2021.3.45, Mono).
Includes a central mod menu with blue-navy UI theme, in-game console, economy
tools, battle debugger, and core game fixes.

## Included Mods

| Mod | Description | Hotkey |
|-----|-------------|--------|
| **SpacefleetModMenu** | Central mod framework: shared blue-navy skin, auto-detection, hotkey editor, enable/disable mods, config editor | `'` |
| **SpacefleetConsole** | In-game developer console with 20+ commands | `º` / `` ` `` |
| **SpacefleetEconomyDebug** | Economy health monitor with resource/market/trader/station inspector | `¡` |
| **SpacefleetBattleDebug** | Real-time combat tracker with Harmony damage events + strategic fleet view | `+` |
| **SpacefleetCoreFixes** | Money sound limiter + fleet delta-v softlock recovery (background) | — |

## Quick Install

1. Copy the contents of `modpack-root/` into the game root (next to the `.exe`)
2. Start the game
3. Check `BepInEx/LogOutput.log`

### Steam Deck / Proton

The game runs via Proton on Steam Deck. Add this as **Steam Launch Options**
(right-click game → Properties → Launch Options):

```
WINEDLLOVERRIDES="winhttp=native,builtin" %command%
```

Without this, Proton uses its own `winhttp.dll` and BepInEx will not load.

Correct layout:

```text
Spacefleet - Heat Death/
  Spacefleet - Heat Death.exe
  winhttp.dll
  doorstop_config.ini
  BepInEx/
    core/
    plugins/
      SpacefleetCoreFixes.dll
      SpacefleetModMenu.dll
      SpacefleetConsole.dll
      SpacefleetEconomyDebug.dll
      SpacefleetBattleDebug.dll
```

## Folder Layout

```text
spacefleet-mods&docs/
  README.md                   ← this file
  MODREADME.md                ← installation, BepInEx layout, hotkeys, workflow
  ECONOMY_MODDING_NOTES.md    ← economy class analysis and patch targets
  modpack-root/               ← shareable install package
    doorstop_config.ini
    winhttp.dll
    BepInEx/
      core/
      plugins/                ← compiled DLLs
  developer/
    DEBUGGING.md              ← debugging guide
    scripts/                  ← build, deploy, logging scripts
    source/                   ← portable source copies (no bin/obj)
      SpacefleetModMenu/
      SpacefleetConsole/
      SpacefleetEconomyDebug/
      SpacefleetBattleDebug/
      SpacefleetCoreFixes/
    templates/
      BasicBepInExMod/        ← starter template for new mods
```

## UI System

All mods share a unified blue-navy UI theme provided by ModMenu:

- Dark background with navy header strip and blue accent borders
- Configurable background opacity (0–100% via ModMenu slider)
- Every window: minimizable, resizable, draggable, closeable
- Skin provided by ModMenu, consumed by other mods via reflection
- Falls back to default Unity skin if ModMenu is not loaded

See `developer/source/SpacefleetModMenu/DEVREADME.md` for the full integration guide.

## Development

Primary working source:

```text
ModSource/
```

Build all mods:

```powershell
dotnet build "Spacefleet - Heat Death.sln" -c Release
```

DLLs output to `ModSource/<project>/bin/Release/<project>.dll`.

Deploy to plugins:

```powershell
Copy-Item ModSource\<project>\bin\Release\<project>.dll BepInEx\plugins\ -Force
```

Scripts:

```text
developer/scripts/build-all-mods.ps1
developer/scripts/install-modpack-to-game-root.ps1
developer/scripts/watch-bepinex-log.ps1
developer/scripts/clean-source-artifacts.ps1
developer/scripts/enable-debug-logging.ps1
```

## Creating New Mods

See `developer/source/SpacefleetModMenu/DEVREADME.md` for a complete guide on
creating mods that integrate with the Mod Menu, including:

- Project setup and references
- Implementing the hotkey API for auto-detection
- Consuming the shared blue-navy skin
- Drawing opaque IMGUI windows
- Full code examples

A starter template is available at `developer/templates/BasicBepInExMod/`.

## Debugging

See `developer/DEBUGGING.md` for:

- Log file location and live watching
- Enabling debug logging
- Attaching debuggers
- Common failure cases

## Required Tools

- .NET SDK (for building)
- BepInEx 5 x64 for Unity Mono (included in modpack-root)
- A C# editor (VS Code, Visual Studio, Rider)
- Optional: dnSpyEx, ILSpy, or dotPeek for decompilation

Unity Editor is not needed for code mods.

## Current Status

All mods are functional on Spacefleet - Heat Death with BepInEx 5.4.23.5.
The economy debug window exposes resource signals, market stress, trader
diagnostics, and a per-station resource inspector. Core game fixes (money
sound limiter, fleet delta-v recovery) run silently in the background.
