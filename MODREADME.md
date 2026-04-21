# Spacefleet - Heat Death Modding README

Detailed reference for installing, building, and managing BepInEx mods for
Spacefleet - Heat Death.

## Game Info

```text
Game:      Spacefleet - Heat Death
Engine:    Unity 2021.3.45 (Mono)
Loader:    BepInEx 5.4.23.5
Managed:   Spacefleet - Heat Death_Data/Managed/Assembly-CSharp.dll
```

## BepInEx Installation Layout

BepInEx must be installed in the game root, next to the `.exe`:

```text
Spacefleet - Heat Death/
  Spacefleet - Heat Death.exe
  UnityPlayer.dll
  winhttp.dll              ← MUST be beside .exe
  doorstop_config.ini      ← MUST be beside .exe
  BepInEx/
    core/
      BepInEx.dll
      BepInEx.Preloader.dll
      0Harmony.dll
    plugins/
      YourMod.dll
    config/
    patchers/
    cache/
```

## First Launch Check

After installing BepInEx, launch the game once. Check for:

```text
BepInEx/LogOutput.log
BepInEx/config/BepInEx.cfg
```

Successful load includes:

```text
Preloader finished
Chainloader ready
Chainloader startup complete
```

## Steam Deck / Proton (Linux)

The game runs via Proton on Steam Deck. Proton uses its own `winhttp.dll` by
default, which prevents BepInEx's doorstop from loading. You must override this.

**Set Steam Launch Options** (right-click game → Properties → Launch Options):

```
WINEDLLOVERRIDES="winhttp=native,builtin" %command%
```

After setting this, launch the game and verify BepInEx loads by checking for
`BepInEx/LogOutput.log`.

## Included Mods

| Mod | GUID | Type | Hotkey |
|-----|------|------|--------|
| SpacefleetModMenu | local.spacefleet.mod-menu | UI + Framework | `'` |
| SpacefleetConsole | local.spacefleet.console | UI + Commands | `º` / `` ` `` |
| SpacefleetEconomyDebug | local.spacefleet.economy-debug | UI + Diagnostics | `¡` |
| SpacefleetBattleDebug | local.spacefleet.battle-debug | UI + Harmony | `+` |
| SpacefleetCoreFixes | local.spacefleet.core-fixes | Harmony | — |

## Hotkeys

```text
'             open/close Spacefleet Mod Menu
Mods button   open Mod Menu from game main menu
º / `         open/close Spacefleet Console
¡             open/close Spacefleet Economy Debug
+             open/close Spacefleet Battle Debug
```

All hotkeys are editable from the Mod Menu at runtime.

## Console Commands

```text
help, clear, close, mods, scene, time, timescale <value>,
credits, addcredits <amount>, setcredits <amount>, economy,
objects <Type>, dump <Type> <index>, get <Type> <index> <field>,
set <Type> <index> <field> <value>, invoke <Type> <index> <method>,
fleets, fleet <index>, fleetrefuel <index|all> [ratio],
fleetrepair <index|all> [ratio], fleetrecover <index|all>,
market.dump, market.stations, market.inject <resource> <qty> [idx], market.fix
```

## Config Files

Created after first launch:

```text
BepInEx/config/local.spacefleet.mod-menu.cfg
BepInEx/config/local.spacefleet.console.cfg
BepInEx/config/local.spacefleet.economy-debug.cfg
BepInEx/config/local.spacefleet.battle-debug.cfg
BepInEx/config/local.spacefleet.core-fixes.cfg
```

All configs are viewable and editable from the ModMenu's inline config editor.

## UI System

All windows share a blue-navy theme provided by ModMenu:

- Dark background, navy header strip, blue accent borders and text
- Background opacity: configurable 0–100% (default 95%)
- All windows: minimizable, resizable, draggable, closeable
- Other mods discover skin via reflection (no compile-time dependency)
- Graceful fallback to default Unity skin if ModMenu is not loaded

## Shareable Layout

```text
spacefleet-mods&docs/
  modpack-root/
    .doorstop_version
    doorstop_config.ini
    winhttp.dll
    BepInEx/
      core/
      plugins/
        SpacefleetCoreFixes.dll
        SpacefleetModMenu.dll
        SpacefleetConsole.dll
        SpacefleetEconomyDebug.dll
        SpacefleetBattleDebug.dll
```

## Building Mods

Build from the game root:

```powershell
dotnet build "Spacefleet - Heat Death.sln" -c Release
```

Or individual mods:

```powershell
dotnet build ModSource\SpacefleetModMenu\SpacefleetModMenu.csproj -c Release
```

DLLs output to `ModSource/<project>/bin/Release/<project>.dll`.

Deploy:

```powershell
Copy-Item ModSource\<project>\bin\Release\<project>.dll BepInEx\plugins\ -Force
```

Scripts:

```powershell
spacefleet-mods&docs\developer\scripts\build-all-mods.ps1
spacefleet-mods&docs\developer\scripts\install-modpack-to-game-root.ps1
spacefleet-mods&docs\developer\scripts\watch-bepinex-log.ps1
spacefleet-mods&docs\developer\scripts\clean-source-artifacts.ps1
spacefleet-mods&docs\developer\scripts\enable-debug-logging.ps1
```

## Common References

```text
BepInEx/core/BepInEx.dll
BepInEx/core/0Harmony.dll
Spacefleet - Heat Death_Data/Managed/Assembly-CSharp.dll
Spacefleet - Heat Death_Data/Managed/UnityEngine.dll
Spacefleet - Heat Death_Data/Managed/UnityEngine.CoreModule.dll
Spacefleet - Heat Death_Data/Managed/UnityEngine.IMGUIModule.dll
Spacefleet - Heat Death_Data/Managed/UnityEngine.TextRenderingModule.dll
Spacefleet - Heat Death_Data/Managed/UnityEngine.InputLegacyModule.dll
```

## Minimal Plugin Shape

```csharp
using BepInEx;
using UnityEngine;

[BepInPlugin("local.spacefleet.example", "Example Mod", "0.1.0")]
public sealed class ExamplePlugin : BaseUnityPlugin
{
    private void Awake()
    {
        Logger.LogInfo("Example mod loaded.");
    }
}
```

## Creating Mods That Work With ModMenu

See `developer/source/SpacefleetModMenu/DEVREADME.md` for the complete developer guide.

## Recommended Workflow

1. Edit mod source under `ModSource`.
2. Compile with `build-all-mods.ps1` or `dotnet build`.
3. Copy generated DLL/PDB into `BepInEx/plugins` and `modpack-root`.
4. Start the game.
5. Inspect `BepInEx/LogOutput.log`.
6. Remove `bin` and `obj` before sharing source.

## Debugging

See `developer/DEBUGGING.md` for log watching, debug builds, and common failure cases.

## Portability

- Compiled DLLs are portable inside any compatible game install
- Project files use relative paths to game/BepInEx assemblies
- Before sharing source, remove `bin/` and `obj/` folders
- Steam file verification may remove BepInEx — reinstall if needed

## Notes

- Do not edit `Assembly-CSharp.dll` directly
- Prefer Harmony patches so game files remain untouched
- Keep each mod small and test one change at a time
- Credit/fleet commands can alter real saves — test on throwaway saves
