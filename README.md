# Spacefleet-Heat-Death-BepInEx-Mods

BepInEx 5 mod pack for **Spacefleet - Heat Death** (Unity 2021.3.45, Mono).
Includes a central mod menu with cyberpunk UI theme, in-game console, economy
tools, battle debugger, and core game fixes.

## Included Mods

| Mod | Description | Hotkey |
|-----|-------------|--------|
| **SpacefleetModMenu** | Central mod framework: cyberpunk UI skin, auto-detection, hotkey editor, enable/disable mods, config editor | `'` |
| **SpacefleetConsole** | In-game developer console with 20+ commands | `º` / `` ` `` |
| **SpacefleetEconomyDebug** | Economy health monitor with resource/market/trader signals | `¡` |
| **SpacefleetBattleDebug** | Real-time combat tracker with Harmony damage events + strategic fleet view | `+` |
| **SpacefleetEconomyOverhaul** | Conservative market price balancing (background) | — |
| **SpacefleetCoreFixes** | Money sound limiter + fleet delta-v softlock recovery (background) | — |

## Quick Install

1. Copy the contents of `modpack-root/` into the game root (next to the `.exe`)
2. Start the game
3. Check `BepInEx/LogOutput.log`

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
      SpacefleetEconomyOverhaul.dll
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
      SpacefleetEconomyOverhaul/
    templates/
      BasicBepInExMod/        ← starter template for new mods
```

## UI System

All mods with windows share a unified cyberpunk UI theme:

- Dark background with green neon borders and text
- Configurable background opacity (0-100% via ModMenu slider)
- Every window: minimizable, resizable, draggable, closeable
- Skin provided by ModMenu, consumed by other mods via reflection
- Falls back to default Unity skin if ModMenu is not loaded

See `ModSource/SpacefleetModMenu/DEVREADME.md` for the full integration guide.

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

See `ModSource/SpacefleetModMenu/DEVREADME.md` for a complete guide on creating
mods that integrate with the Mod Menu, including:

- Project setup and references
- Implementing the hotkey API for auto-detection
- Consuming the shared cyberpunk skin
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
The economy is the most sensitive area: `SpacefleetEconomyOverhaul` only adjusts
`Market.GetCurrentPrice` return values with cached calculations. It does not
modify saves, inventories, trade execution, or trader routes.
# Spacefleet Mods & Docs

This directory is the modpack and developer documentation area for Spacefleet -
Heat Death. It is meant for two jobs:

1. installing or sharing the compiled mods;
2. keeping the source code and technical notes needed to keep building mods
   without modifying the original game files.

The current mods are BepInEx 5 plugins for the Unity Mono build of the game.
Unity Editor is not required for these code mods. Unity would only be needed
later for Unity assets such as AssetBundles, prefabs, materials, or scenes.

## Folder Layout

```text
spacefleet-mods&docs\
  README.md
  MODREADME.md
  ECONOMY_MODDING_NOTES.md
  modpack-root\
  developer\
    DEBUGGING.md
    scripts\
    source\
    templates\
```

`README.md` is this high-level guide.

`MODREADME.md` explains installation, BepInEx layout, hotkeys, packaging, and
the basic workflow for creating new mods.

`ECONOMY_MODDING_NOTES.md` documents the economy investigation: important
classes, observed fields, safe patch points, risks, and the current economy
overhaul approach.

`modpack-root` contains the files that can be copied into the game root to
install BepInEx and the compiled plugins.

`developer/source` contains portable source copies for each mod. It must not
contain `bin` or `obj`, because those folders can include absolute paths from
the machine that built the mods.

`developer/scripts` contains helper scripts for building, installing, cleaning,
and reading the BepInEx log.

## Included Mods

```text
SpacefleetCoreFixes
SpacefleetModMenu
SpacefleetConsole
SpacefleetEconomyDebug
SpacefleetEconomyOverhaul
SpacefleetBattleDebug
```

`SpacefleetCoreFixes` limits the money sound effect to 2 plays per 30 seconds.
This prevents audio spam when money changes repeatedly or when debug/cheat
commands add credits many times. It also applies an emergency recovery to
player fleets softlocked at zero delta-v.

`SpacefleetModMenu` adds an in-game window that lists installed BepInEx plugins.
It opens with the single quote key, adds a `Mods` button to the game's main
menu, and lets players edit mod hotkeys at runtime. It also includes a config
viewer that lets players browse and edit BepInEx config files for each mod.

`SpacefleetConsole` adds an IMGUI console with diagnostic commands such as
`mods`, `scene`, `time`, `credits`, `addcredits`, `setcredits`, `economy`, and
developer commands for object inspection and fleet recovery.

`SpacefleetEconomyDebug` adds a player-focused economy inspection window. It
opens with `¡` and shows economy health, recommendations, resource signals,
market stress, trader diagnostics, and optional technical data. It refreshes its
data at most once per second so it does not overload `OnGUI`.

`SpacefleetEconomyOverhaul` adjusts market prices conservatively. It uses local
stock pressure, global supply/demand pressure, and base-price clamps. Expensive
calculations are cached because `Market.GetCurrentPrice` can be called very
often. It also blocks dust trades and enforces a minimum auto-trader profit
margin.

`SpacefleetBattleDebug` adds a battle inspection window. It opens with `+` and
shows ship health, armor, weapons, heat, damage events, fleet orders, and
navigation data during tactical combat and on the strategic map.

## Hotkeys

```text
'             open or close Spacefleet Mod Menu
Mods button   open Spacefleet Mod Menu from the game main menu
º / `         open or close Spacefleet Console
¡             open or close Spacefleet Economy Debug
+             open or close Spacefleet Battle Debug
```

## Quick Install

To install the pack into a game copy:

1. open `modpack-root`;
2. copy its contents into the game root, next to the `.exe`;
3. start the game once;
4. check `BepInEx/LogOutput.log`.

Correct layout:

```text
Spacefleet - Heat Death\
  Spacefleet - Heat Death.exe
  winhttp.dll
  doorstop_config.ini
  BepInEx\
    core\
    plugins\
      SpacefleetCoreFixes.dll
      SpacefleetModMenu.dll
      SpacefleetConsole.dll
      SpacefleetEconomyDebug.dll
      SpacefleetEconomyOverhaul.dll
      SpacefleetBattleDebug.dll
```

If `winhttp.dll` or `doorstop_config.ini` are inside another folder, BepInEx will
not load.

## Development Workflow

Primary working source lives in:

```text
ModSource\
```

The shareable source copy lives in:

```text
spacefleet-mods&docs\developer\source\
```

Each mod source folder has its own `README.md` explaining what the mod does, how
it works, what game classes it touches, and how to test it.

Useful scripts:

```text
spacefleet-mods&docs\developer\scripts\build-all-mods.ps1
spacefleet-mods&docs\developer\scripts\install-modpack-to-game-root.ps1
spacefleet-mods&docs\developer\scripts\watch-bepinex-log.ps1
spacefleet-mods&docs\developer\scripts\clean-source-artifacts.ps1
```

Recommended loop:

1. edit the mod source under `ModSource`;
2. compile with `build-all-mods.ps1`;
3. copy the generated DLL/PDB files into `BepInEx/plugins` and `modpack-root`;
4. start the game;
5. inspect `BepInEx/LogOutput.log`;
6. remove `bin` and `obj` before sharing source.

## Debugging

The main log is:

```text
BepInEx\LogOutput.log
```

Successful load lines should include:

```text
Spacefleet Core Fixes loaded.
Spacefleet Mod Menu loaded.
Spacefleet Console loaded.
Spacefleet Economy Debug loaded. Press ¡ to open.
Spacefleet Economy Overhaul loaded.
Spacefleet Battle Debug loaded. Press + to open.
```

If a mod does not appear in the log, check:

1. its DLL is inside `BepInEx/plugins`;
2. BepInEx is installed beside the game `.exe`;
3. the mod targets BepInEx 5 and Unity Mono;
4. all Unity and Harmony references resolve;
5. the plugin GUID is unique.

## Portability Rules

The pack must not depend on absolute paths from the development machine. The
compiled DLLs are portable inside a compatible game install. Project files use
relative paths to the game and BepInEx assemblies.

Before sharing source, run the cleanup script to remove `bin` and `obj`.

## Current Status

The current pack focuses on code mods, debugging tools, and conservative economy
balancing. The economy is the most sensitive area: `SpacefleetEconomyOverhaul`
does not modify saves, inventories, trade execution, or trader routes. It only
adjusts the value returned by `Market.GetCurrentPrice`, and it now caches the
expensive parts of that calculation to avoid stutter.
