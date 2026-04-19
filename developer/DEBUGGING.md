# Debugging Spacefleet Mods

Use this when a mod does not load, crashes, or behaves unexpectedly.

## Log File

Main log:

```text
BepInEx/LogOutput.log
```

Successful load lines:

```text
Spacefleet Core Fixes loaded.
Spacefleet Mod Menu loaded. Hotkey: Quote.
Spacefleet Console loaded. Press º or BackQuote to open.
Spacefleet Economy Debug loaded. Press ¡ to open.
Spacefleet Economy Overhaul loaded.
Spacefleet Battle Debug loaded. Press + to open.
```

## Watch The Log Live

```powershell
Get-Content BepInEx\LogOutput.log -Wait -Tail 80
```

Or:

```powershell
.\spacefleet-mods&docs\developer\scripts\watch-bepinex-log.ps1
```

## Enable Debug Logging

```powershell
.\spacefleet-mods&docs\developer\scripts\enable-debug-logging.ps1
```

In `BepInEx/config/BepInEx.cfg`:

```ini
[Logging.Console]
Enabled = true
LogLevels = All

[Logging.Disk]
Enabled = true
LogLevels = All
```

## Build Debug DLLs

```powershell
dotnet build ModSource\SpacefleetModMenu\SpacefleetModMenu.csproj -c Debug
```

Copy both `.dll` and `.pdb` into `BepInEx/plugins/`. The `.pdb` maps stack
traces to source lines.

## Runtime Logging

Inside a `BaseUnityPlugin`:

```csharp
Logger.LogInfo("Loaded.");
Logger.LogWarning("Something suspicious.");
Logger.LogError("Something failed.");
```

For Harmony patches in static classes:

```csharp
BepInEx.Logging.Logger.CreateLogSource("MyMod").LogInfo("Patch ran.");
```

## Verify Harmony Patches

Add a temporary log in the patch:

```csharp
[HarmonyPrefix]
private static void Prefix()
{
    BepInEx.Logging.Logger.CreateLogSource("MyMod").LogInfo("Prefix hit.");
}
```

Watch `LogOutput.log`. Remove noisy logs when confirmed.

## Attaching A Debugger

1. Build mod in `Debug`
2. Copy `.dll` + `.pdb` to `BepInEx/plugins/`
3. Start the game
4. Attach VS, Rider, or dnSpyEx to `Spacefleet - Heat Death.exe`

For hard cases, temporarily add:

```csharp
System.Diagnostics.Debugger.Launch();
```

Remove after debugging.

## ModMenu Skin Debugging

If a window appears transparent or unstyled:

1. Check that `TryGetSharedSkin()` finds `SpacefleetModMenu.ModMenuPlugin`
2. Verify `GetBgTexture()` returns a valid `Texture2D`
3. Confirm `GUI.DrawTexture()` is called at the start of `DrawWindow`
4. Check `BackgroundOpacity` in `local.spacefleet.mod-menu.cfg` (default 95)

If ModMenu is not loaded, windows should create their own dark fallback texture.

## Common Failure Cases

**Plugin does not load:**
- DLL not in `BepInEx/plugins/`
- Wrong framework target (must be netstandard2.0)
- Missing dependency DLL
- Duplicate plugin GUID
- DLL moved to `disabled/` folder (check `BepInEx/plugins/disabled/`)

**Harmony patch does nothing:**
- Target method name wrong or overloaded
- Patch class not included in `harmony.PatchAll()`
- Code path has not run yet

**Game crashes on launch:**
- Remove the latest mod DLL from `BepInEx/plugins/`
- Restart and read `LogOutput.log`

**Window appears but transparent:**
- Skin not loaded (ModMenu not present)
- `GUI.DrawTexture()` missing from `DrawWindow`
- `bgTexture` is null

**ModMenu doesn't detect my mod:**
- Missing `public static string GetHotkeySummary()` method
- Missing `public static void SetHotkey(string, string)` method
- Both methods must be `public static` on the main plugin class

## Window ID Reference

```text
ModMenu:           42010  (GUI.depth -1000)
Console:           42011  (GUI.depth -1001)
EconomyDebug:      42012  (GUI.depth -1002)
BattleDebug Info:  42013  (GUI.depth -1003)
BattleDebug Feed:  42014  (GUI.depth -1003)
```

Pick unique IDs (42015+) for new mods to avoid conflicts.
