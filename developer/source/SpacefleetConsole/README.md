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
economy                       Print GlobalMarket snapshot with supply/demand per resource
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
market.dump                   Full market analysis – inventory, stockRatios, resupply rates
market.stations               List all stations by index
market.inject <res> <qty> [stationIdx]  Inject resources into market
market.fix                    Reset broken stockRatios across all markets
```

### Market Commands (v0.2.0)

**`market.dump`** — Detailed dump of every station's market state including
resupply rate, ideal stock ratio, storage capacity, and per-resource inventory
with stock ratios. Resources with low stockRatio and high inventory are
highlighted in red.

**`market.stations`** — Quick reference list of station names and indices,
for use with `market.inject`.

**`market.inject <resource> <quantity> [stationIndex]`** — Inject resources
into station inventories. Resource names are fuzzy-matched (e.g., "dt" matches
"DT Fuel"). Without a station index, distributes evenly across all markets.

```text
market.inject "DT Fuel" 500 0    -- inject 500 DT into station 0
market.inject volatiles 200      -- inject 200 volatiles into ALL stations
```

**`market.fix`** — Emergency command to reset broken stock ratios. Scans all
markets and increases stockRatios for resources with significant inventory but
near-zero ratios. This immediately allows trade to resume.

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
