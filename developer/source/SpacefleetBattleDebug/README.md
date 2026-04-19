# Spacefleet Battle Debug

`SpacefleetBattleDebug` adds real-time combat tracking and fleet inspection
windows. Uses Harmony patches for live damage event capture. Integrates with
the ModMenu cyberpunk skin system.

**Version**: 0.1.0
**GUID**: `local.spacefleet.battle-debug`
**Hotkey**: `+`

## What It Does

Two independent windows:

### Info Window (42013)
- **Tactical mode** (CombatManager active): Player/hostile ships with health
  bars, armor, DC teams, materials, targets, weapon status, heat management,
  damaged/destroyed modules
- **Strategic mode**: Fleet overview with navigation state, flight type, target,
  distance, per-ship health/fuel/DV

### Feed Window (42014)
- Real-time damage events via Harmony patches on `T_Module` and `ShipController`
- Tracks kinetic/photonic damage, module destruction, ship soft/hard deaths
- Reverse chronological, capped at 50 entries
- Only shows in tactical mode (not on strategic map)

## Features (v0.3.0 UI)

- **Cyberpunk skin**: Picks up ModMenu's shared skin via reflection
- **Independent windows**: Info and Feed can be opened/closed/minimized separately
- **Minimizable**: Each window has its own — / □ button
- **Resizable**: Each window has its own ◢ drag handle
- **Opaque background**: Explicit `GUI.DrawTexture` fill per window
- **Toggle button**: Show Info → open Feed from Info header when in tactical
- **Feed bug fix**: Feed window gated on `showFeed && isTactical`

## Controls

```text
+             toggle Battle Debug (opens both windows; if any open, closes all)
Close (✕)     close individual window
Clear         clear the event feed log
Weapons       toggle weapon details in tactical view
Heat          toggle heat details in tactical view
Feed          open Feed window from Info header (tactical only)
```

Hotkey changeable from ModMenu.

## Harmony Patches

Patches applied on game types found via reflection:

```text
T_Module.TryDamageKinetic     → capture kinetic damage events
T_Module.TryDamagePhotonic    → capture photonic damage events
T_Module.SetIsDead            → capture module destruction
ShipController.SoftDeath      → capture ship soft death
ShipController.HardDeath      → capture ship hard death
```

Patches are safe: they only read data for the feed log and do not modify game behavior.

## Performance

- Refreshes at most every 0.5 seconds
- All reflection lookups cached
- Disabled sections not recalculated
- Feed capped at 50 entries

## Config

```text
BepInEx\config\local.spacefleet.battle-debug.cfg
```

## Game Classes Inspected

```text
CombatManager, ShipController, T_Module, WeaponSystem, TurretWeapon,
HeatManager, FleetManager, S_Ship, Navigation, Track
```

## How It Works

- Two IMGUI windows: `GUI.Window(42013, ...)` and `GUI.Window(42014, ...)`
- `GUI.depth = -1003`
- Skin + bg texture from ModMenu via reflection (with fallback)
- `InitStyles()` creates local `richLabel`, `sectionBox` from current skin
- Strategic/tactical mode detected via `CombatManager.current` singleton

## Files

```text
BattleDebugPlugin.cs
SpacefleetBattleDebug.csproj
```
# Spacefleet Battle Debug

`SpacefleetBattleDebug` adds a player-focused battle inspection window for
understanding what is happening in tactical combat and on the strategic map.
It does not change combat values. It reads data and presents it as actionable
signals.

## What It Does

The window works in two modes:

**Tactical Combat Mode**: When `CombatManager` is active, it shows real-time
data about player and hostile ships, weapons, damage, heat, and events.

**Strategic Map Mode**: When on the strategic map, it shows fleet status,
navigation orders, ship health, fuel, and delta-v.

## Sections

```text
Battle Overview
Player Ships
Hostile Ships
Weapon Status
Damage Report
Fleet Orders & Navigation
Heat Management
Event Log
```

`Battle Overview` shows the current mode, ship counts (alive/destroyed), and
overall combat state.

`Player Ships` lists each player ship with health bar, armor, damage control
teams, materials, current target, and module breakdown.

`Hostile Ships` lists each hostile ship with health, target, and module status.

`Weapon Status` shows each weapon system per ship: type, mode, range, whether
engaging, turret count, and health.

`Damage Report` lists every damaged or destroyed module across all ships with
part type and health percentage.

`Fleet Orders & Navigation` shows fleet state, navigation state, flight type,
target, distance, and per-ship health/fuel/dv on the strategic map.

`Heat Management` shows per-ship heat percentage, net heat flow, heatsink
status, radiator state, and overflow warnings.

`Event Log` tracks damage events, target changes, and ship destructions in
reverse chronological order. The log persists across refreshes until manually
cleared.

## Controls

```text
+              open or close Battle Debug
Close button   close the window
Clear Log      clear the event log
Toggle buttons show/hide individual sections
```

The hotkey can be changed from `Spacefleet Mod Menu` under `Editable Hotkeys`.
It is saved to:

```text
BepInEx\config\local.spacefleet.battle-debug.cfg
```

## Performance

The window refreshes at most every 0.5 seconds. It caches all reflection
lookups. Disabled sections are not recalculated. The event log is capped at 50
entries.

## How It Works

The plugin is loaded by BepInEx:

```csharp
[BepInPlugin("local.spacefleet.battle-debug", "Spacefleet Battle Debug", "0.1.0")]
```

It detects tactical combat by looking for `CombatManager.current`. When that
singleton exists, it reads `playerShips`, `hostileShips`, and `deadShips` to
build the tactical view. Each `ShipController` exposes `allModules`, `weapons`,
and `hm` (HeatManager).

On the strategic map, it scans `FleetManager` objects and reads navigation,
fleet state, and per-ship data from `S_Ship`.

The UI uses Unity IMGUI and does not modify game canvases or prefabs.

## Game Classes Inspected

```text
CombatManager
ShipController
T_Module
WeaponSystem
TurretWeapon
HeatManager
FleetManager
S_Ship
Navigation
Track
```

## Testing

1. Start the game with the mod installed.
2. Check the log for: `Spacefleet Battle Debug loaded.`
3. On the strategic map, press `+` to see fleet overview.
4. Enter tactical combat and press `+` to see live combat data.
5. Watch the event log for damage and target changes.
6. Toggle sections on/off to reduce clutter.

## Risks And Limits

The mod only reads data, so save risk is very low.

If the game renames `CombatManager`, `ShipController`, `T_Module`,
`WeaponSystem`, or changes their field names, the reader must be updated.

The event log tracks health changes between refreshes. Very fast damage bursts
within a single refresh interval may appear as a single event.

## Files

```text
BattleDebugPlugin.cs
SpacefleetBattleDebug.csproj
```
