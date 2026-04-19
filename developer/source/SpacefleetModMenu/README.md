# Spacefleet Mod Menu

`SpacefleetModMenu` is the central pillar for all Spacefleet mods. It provides
a unified cyberpunk-themed UI skin, auto-detection of mods, editable hotkeys,
mod enable/disable management, and per-mod config editing.

**Version**: 0.3.0
**GUID**: `local.spacefleet.mod-menu`
**Hotkey**: `'` (single quote)

## What It Does

### Shared Cyberpunk UI Skin

The Mod Menu creates a centralized `GUISkin` with a cyberpunk aesthetic that
matches the game's visual style:

- Dark background (#0D0F14) with green neon borders (#00AA44)
- Bright green text (#00FF88) for headers and active elements
- Muted green (#B0C4B0) for body text, dim grey (#667766) for secondary info
- All UI elements themed: windows, buttons, text fields, toggles, scrollbars
- Background opacity configurable via slider (0-100%, default 95%)

Other mods discover this skin via reflection — no compile-time dependency
required. If ModMenu is not loaded, mods fall back gracefully.

### Auto-Detection

The menu automatically discovers all loaded BepInEx plugins. Mods that implement
the hotkey API (`GetHotkeySummary()` / `SetHotkey()`) get inline hotkey editing.
No hardcoded mod references needed.

### Mod Enable/Disable

- **Disable**: Moves a mod DLL to `BepInEx/plugins/disabled/`
- **Undo**: Moves it back before restart
- **Enable**: Re-enables disabled mods
- Cannot disable ModMenu itself (self-protection)
- Changes take effect after game restart

### Config Editor

For each plugin with a BepInEx config file, an expandable inline editor lets
players view, edit, reload, and save config values in-game.

### Window Features

All mod windows share:

- **Minimize** (— / □): Collapse to title bar
- **Close** (✕): Hide the window
- **Resize** (◢): Drag bottom-right corner
- **Drag**: Reposition by dragging title bar

## Public API

Other mods access these via reflection (see DEVREADME.md for integration guide):

```csharp
GUISkin  GetSharedSkin()       // Cyberpunk GUISkin
Texture2D GetBgTexture()       // Background fill (respects opacity)
GUIStyle GetRichLabel()        // Rich text, no wrap
GUIStyle GetRichLabelWrap()    // Rich text, word wrap
GUIStyle GetHeaderStyle()      // Bold, bright green, 14pt
GUIStyle GetDimLabel()         // Muted grey text
string   GetHotkeySummary()    // Current hotkey description
void     SetHotkey(string keyCodeName, string character)
```

## Controls

```text
'             open or close the mod menu
Mods button   open from the game's main menu
BG Opacity    slider at bottom of window (0-100%)
```

## Config

```text
BepInEx\config\local.spacefleet.mod-menu.cfg
```

```ini
[Hotkeys]
ToggleKey = Quote
ToggleCharacter =

[UI]
BackgroundOpacity = 95
```

## How It Works

- Loaded by BepInEx as `[BepInPlugin("local.spacefleet.mod-menu", ...)]`
- Skin created via `Instantiate(GUI.skin)` with full style overrides
- Textures generated with `MakeTex()` / `MakeBorderTex()`, all `DontDestroyOnLoad`
- Background drawn explicitly via `GUI.DrawTexture()` in every window callback
- Auto-detection scans `Chainloader.PluginInfos` every 5 seconds
- Enable/disable moves DLLs between `plugins/` and `plugins/disabled/`
- Main Menu button detected via `FindObjectOfType` on the `MainMenu` type

## Window IDs

```text
ModMenu:      42010  (GUI.depth -1000)
Console:      42011  (GUI.depth -1001)
EconomyDebug: 42012  (GUI.depth -1002)
BattleDebug:  42013, 42014  (GUI.depth -1003)
```

## Testing

1. Start the game. Main menu should show a "Mods" button.
2. Press `'` in-game to open the mod menu.
3. Confirm all plugins listed with name, GUID, DLL, version.
4. Test hotkey editing, disable/undo, config editor.
5. Adjust BG Opacity slider.

## Files

```text
ModMenuPlugin.cs
SpacefleetModMenu.csproj
DEVREADME.md
```
