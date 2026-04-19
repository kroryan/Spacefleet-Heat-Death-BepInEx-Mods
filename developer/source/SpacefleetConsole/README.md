# Spacefleet Console

`SpacefleetConsole` adds an in-game IMGUI console with diagnostic commands and
developer tools. It integrates with the ModMenu cyberpunk skin system.

**Version**: 0.1.0
**GUID**: `local.spacefleet.console`
**Hotkey**: `º` / `` ` `` (backquote)

## What It Does

Provides a scrollback console with input field, command history (Up/Down), and
a set of diagnostic/cheat commands for debugging game state and recovering
broken saves.

## Features (v0.3.0 UI)

- **Cyberpunk skin**: Automatically picks up ModMenu's shared skin via reflection
- **Minimizable**: Collapse to title bar with — button
- **Resizable**: Drag ◢ handle at bottom-right
- **Opaque background**: Draws explicit `GUI.DrawTexture` fill
- **Fallback**: Works without ModMenu (uses default Unity skin + dark bg)

## Commands

```text
help                          Show command list
clear                         Clear console buffer
close                         Close the console window
mods                          List loaded BepInEx plugins
scene                         Print active Unity scene
time                          Print timeScale and TimeManager data
timescale <value>             Change Time.timeScale
credits                       Show player credits
addcredits <amount>           Add credits via FactionsManager
setcredits <amount>           Set credits directly
economy                       Print GlobalMarket snapshot
objects <TypeName>            List active Unity objects of a type
dump <TypeName> <index>       Print all fields of an object
get <TypeName> <index> <field>
set <TypeName> <index> <field> <value>
invoke <TypeName> <index> <method>
fleets                        List FleetManager objects with delta-v
fleet <index>                 Print fleet detail and per-ship data
fleetrefuel <index|all> [ratio]
fleetrepair <index|all> [ratio]
fleetrecover <index|all>      Emergency recovery for stranded fleets
```

## Controls

```text
º / `         open or close the console
Enter         run current command
Up / Down     navigate command history
```

Hotkey changeable from ModMenu's "Editable Hotkeys" section.

## Config

```text
BepInEx\config\local.spacefleet.console.cfg
```

## How It Works

- IMGUI window with `GUI.Window(42011, ...)`, `GUI.depth = -1001`
- Skin discovered via `TryGetSharedSkin()` reflection on `SpacefleetModMenu.ModMenuPlugin`
- Background texture from `GetBgTexture()`, with fallback to dark 95% opacity fill
- Game systems accessed via reflection (`FactionsManager`, `GlobalMarket`, etc.)
- `FindCurrent()` tries static `current` field → property → `FindObjectOfType`

## Files

```text
ConsolePlugin.cs
SpacefleetConsole.csproj
```
