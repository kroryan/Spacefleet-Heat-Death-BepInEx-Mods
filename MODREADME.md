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

This tells Proton to load the `winhttp.dll` from the game folder (the BepInEx
doorstop) instead of its built-in Wine version.

After setting this, launch the game and verify BepInEx loads by checking for
`BepInEx/LogOutput.log`.

## Included Mods

| Mod | GUID | Type | Hotkey |
|-----|------|------|--------|
| SpacefleetModMenu | local.spacefleet.mod-menu | UI + Framework | `'` |
| SpacefleetConsole | local.spacefleet.console | UI + Commands | `º` / `` ` `` |
| SpacefleetEconomyDebug | local.spacefleet.economy-debug | UI + Diagnostics | `¡` |
| SpacefleetBattleDebug | local.spacefleet.battle-debug | UI + Harmony | `+` |
| SpacefleetEconomyOverhaul | local.spacefleet.economy-overhaul | Harmony | — |
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
fleetrepair <index|all> [ratio], fleetrecover <index|all>
```

## Config Files

Created after first launch:

```text
BepInEx/config/local.spacefleet.mod-menu.cfg
BepInEx/config/local.spacefleet.console.cfg
BepInEx/config/local.spacefleet.economy-debug.cfg
BepInEx/config/local.spacefleet.battle-debug.cfg
BepInEx/config/local.spacefleet.economy-overhaul.cfg
BepInEx/config/local.spacefleet.core-fixes.cfg
```

All configs are viewable and editable from the ModMenu's inline config editor.

## UI System

All windows share a cyberpunk theme provided by ModMenu:

- Dark bg, green neon borders, green text
- Background opacity: configurable 0-100% (default 95%)
- All windows: minimizable, resizable, draggable, closeable
- Other mods discover skin via reflection (no compile-time dependency)
- Graceful fallback to default Unity skin if ModMenu is not loaded

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

See `ModSource/SpacefleetModMenu/DEVREADME.md` for the complete developer guide.

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
# Spacefleet - Heat Death Modding README

This folder documents the local modding setup for Spacefleet - Heat Death.

The game is a Unity Mono build, so code mods can be loaded with BepInEx 5 and
written as C# class libraries. Unity Editor is not required for basic code mods.

## Current Game Setup

Game root folder:

```text
Spacefleet - Heat Death\
```

The game executable is:

```text
Spacefleet - Heat Death.exe
```

The managed game code is mainly in:

```text
Spacefleet - Heat Death_Data\Managed\Assembly-CSharp.dll
```

Unity version reported by BepInEx:

```text
Unity 2021.3.45
```

BepInEx version currently installed:

```text
BepInEx 5.4.23.5
```

## Required Tools

For code mods:

1. .NET SDK
2. BepInEx 5 x64 for Unity Mono
3. A C# editor
4. Optional but useful: dnSpyEx, ILSpy, or dotPeek

Unity Editor is not required unless you want to build Unity assets such as
AssetBundles, prefabs, materials, or scenes. If AssetBundles are needed later,
use Unity 2021.3.x when possible because the game was built with Unity 2021.3.

## BepInEx Installation Layout

BepInEx must be installed in the game root, next to the game executable.

Correct layout:

```text
Spacefleet - Heat Death\
  Spacefleet - Heat Death.exe
  UnityPlayer.dll
  winhttp.dll
  doorstop_config.ini
  .doorstop_version
  BepInEx\
    core\
      BepInEx.dll
      BepInEx.Preloader.dll
      0Harmony.dll
      ...
    plugins\
      YourMod.dll
```

Important: `winhttp.dll` and `doorstop_config.ini` must be beside the `.exe`.
If they are inside a separate `mods` folder, the game will not load BepInEx.

## Shareable Folder Layout

This `spacefleet-mods&docs` folder is organized like this:

```text
spacefleet-mods&docs\
  MODREADME.md
  ECONOMY_MODDING_NOTES.md
  modpack-root\
    .doorstop_version
    doorstop_config.ini
    winhttp.dll
    BepInEx\
      core\
      plugins\
        SpacefleetCoreFixes.dll
        SpacefleetCoreFixes.pdb
        SpacefleetModMenu.dll
        SpacefleetModMenu.pdb
        SpacefleetConsole.dll
        SpacefleetConsole.pdb
        SpacefleetEconomyDebug.dll
        SpacefleetEconomyDebug.pdb
        SpacefleetEconomyOverhaul.dll
        SpacefleetEconomyOverhaul.pdb
        SpacefleetBattleDebug.dll
        SpacefleetBattleDebug.pdb
  developer\
    DEBUGGING.md
    scripts\
    source\
      SpacefleetCoreFixes\
    templates\
      BasicBepInExMod\
```

To share the current mod setup with another copy of the game, share:

```text
spacefleet-mods&docs\modpack-root
```

The receiver copies the contents of `modpack-root` into the game root, beside:

```text
Spacefleet - Heat Death.exe
```

That installs BepInEx plus the current core fixes plugin.

## Doorstop Config

The important lines in `doorstop_config.ini` are:

```ini
enabled = true
target_assembly=BepInEx\core\BepInEx.Preloader.dll
```

That path is relative to the game root. If `BepInEx` is moved somewhere else,
this path must be changed too.

## First Launch Check

After installing BepInEx, launch the game once and close it. BepInEx should
create:

```text
BepInEx\LogOutput.log
BepInEx\config\BepInEx.cfg
BepInEx\cache\
BepInEx\patchers\
```

The log should include lines like:

```text
Preloader finished
Chainloader ready
Chainloader started
Chainloader startup complete
```

If the log says `0 plugins to load`, BepInEx is working but no mods are
installed yet.

## Installing A Mod

Install mod DLLs here:

```text
BepInEx\plugins\
```

Example:

```text
BepInEx\plugins\SpacefleetCoreFixes.dll
```

Launch the game and check:

```text
BepInEx\LogOutput.log
```

If the plugin loaded, the log will include the plugin name or its own log
messages.

## Local Core Fixes Mod

The active local fixes mod source in the game root is:

```text
ModSource\SpacefleetCoreFixes\
```

The shareable copy of the source is in:

```text
spacefleet-mods&docs\developer\source\SpacefleetCoreFixes\
```

The installed compiled DLL is:

```text
BepInEx\plugins\SpacefleetCoreFixes.dll
```

Build command:

```powershell
dotnet build .\ModSource\SpacefleetCoreFixes\SpacefleetCoreFixes.csproj -c Release
```

Install command:

```powershell
Copy-Item -LiteralPath ".\ModSource\SpacefleetCoreFixes\bin\Release\SpacefleetCoreFixes.dll" -Destination ".\BepInEx\plugins\SpacefleetCoreFixes.dll" -Force
```

The shareable source can be built with:

```powershell
.\spacefleet-mods&docs\developer\scripts\build-core-fixes.ps1
```

That updates:

```text
spacefleet-mods&docs\modpack-root\BepInEx\plugins\
```

To install the whole modpack into this game root:

```powershell
.\spacefleet-mods&docs\developer\scripts\install-modpack-to-game-root.ps1
```

Build every packaged mod:

```powershell
.\spacefleet-mods&docs\developer\scripts\build-all-mods.ps1
```

Clean generated `bin` and `obj` folders from source trees:

```powershell
.\spacefleet-mods&docs\developer\scripts\clean-source-artifacts.ps1
```

`bin` and `obj` are generated by .NET and can contain absolute local paths.
They should not be treated as source or shared as the standard mod template.

## Included Mods

Current packaged plugins:

```text
SpacefleetCoreFixes
SpacefleetModMenu
SpacefleetConsole
SpacefleetEconomyDebug
SpacefleetEconomyOverhaul
SpacefleetBattleDebug
```

`SpacefleetCoreFixes` includes emergency player-fleet recovery for zero delta-v
softlocks. It gives a stranded player fleet a small fuel/repair reserve instead
of leaving the campaign permanently stuck.

Hotkeys:

```text
Single quote  Open or close the mod menu
Mods button   Open the mod menu from the game main menu
º / `         Open or close the console
¡             Open or close the economy debug window
+             Open or close the battle debug window
```

Console commands:

```text
help
clear
close
mods
scene
time
timescale <value>
credits
addcredits <amount>
setcredits <amount>
economy
objects <TypeName>
dump <TypeName> <index>
get <TypeName> <index> <field>
set <TypeName> <index> <field> <value>
invoke <TypeName> <index> <method>
fleets
fleet <index>
fleetrefuel <index|all> [ratio]
fleetrepair <index|all> [ratio]
fleetrecover <index|all>
```

The economy debug window is safest after loading into a strategic game. If the
game is still on the main menu, some economy systems will show as unavailable.

`SpacefleetModMenu` includes an `Editable Hotkeys` section. Use `Change`, press
the desired key, and the target mod updates immediately.

`SpacefleetEconomyDebug` is now player-focused: it shows economy health,
recommendations, resource signals, market stress, trader diagnostics, and
optional technical data. It is intentionally more informative than the normal
Global Market panel.

`SpacefleetEconomyOverhaul` changes market prices conservatively. It uses local
stock levels, global supply/demand, base-price clamps, dust-trade prevention,
and a minimum auto-trader profit margin. Expensive multipliers are cached
because market price queries can run very often. Its config is created after
first launch here:

```text
BepInEx\config\local.spacefleet.economy-overhaul.cfg
```

`SpacefleetBattleDebug` adds a battle inspection window. It opens with `+` and
shows ship health, armor, weapons, heat, damage events, fleet orders, and
navigation data during tactical combat and on the strategic map. Its config is
created after first launch here:

```text
BepInEx\config\local.spacefleet.battle-debug.cfg
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

## Common References For A Code Mod

Reference these DLLs from the game folder:

```text
BepInEx\core\BepInEx.dll
BepInEx\core\0Harmony.dll
Spacefleet - Heat Death_Data\Managed\Assembly-CSharp.dll
Spacefleet - Heat Death_Data\Managed\UnityEngine.dll
Spacefleet - Heat Death_Data\Managed\UnityEngine.CoreModule.dll
Spacefleet - Heat Death_Data\Managed\UnityEngine.IMGUIModule.dll
Spacefleet - Heat Death_Data\Managed\UnityEngine.TextRenderingModule.dll
```

Only reference the Unity modules the mod actually needs.

## Recommended Workflow

1. Keep source code in `ModSource\ModName`.
2. Compile the mod with `dotnet build`.
3. Copy the compiled DLL into `BepInEx\plugins`.
4. Start the game.
5. Read `BepInEx\LogOutput.log`.
6. If the mod crashes, remove its DLL from `BepInEx\plugins` and restart.

## Debugging Mods

Read:

```text
spacefleet-mods&docs\developer\DEBUGGING.md
```

Fast debug loop:

1. Build the mod in Debug or Release.
2. Copy `.dll` and `.pdb` into `BepInEx\plugins`.
3. Start the game.
4. Watch:

   ```text
   BepInEx\LogOutput.log
   ```

5. Add `Logger.LogInfo(...)` lines around the code you are testing.

Watch the log live:

```powershell
.\spacefleet-mods&docs\developer\scripts\watch-bepinex-log.ps1
```

Enable console/debug logging:

```powershell
.\spacefleet-mods&docs\developer\scripts\enable-debug-logging.ps1
```

## Notes

- Do not edit `Assembly-CSharp.dll` directly for normal mods.
- Prefer Harmony patches so game files remain untouched.
- Keep each mod small and test one change at a time.
- Steam file verification may remove BepInEx files. If that happens, reinstall
  BepInEx and copy the mod DLLs back into `BepInEx\plugins`.
