# Spacefleet Economy Debug

`SpacefleetEconomyDebug` adds a player-focused economy inspection window. It
reads game data and presents it as actionable signals and recommendations.
Integrates with the ModMenu cyberpunk skin system.

**Version**: 0.1.0
**GUID**: `local.spacefleet.economy-debug`
**Hotkey**: `¡`

## What It Does

The window provides more useful information than the game's Global Market panel:

- **Economy Health**: Credits, market count, traders, health label (Stable/Watch/Stressed)
- **Recommended Actions**: Converts raw signals into player-actionable advice
- **Resource Signals**: Demand/supply ratios, price spikes, collapses
- **Market Stress**: Buyer liquidity problems, storage bottlenecks
- **Trader Diagnostics**: Missing targets, dust trades, negative margins
- **Technical Data**: Raw values for mod debugging (hidden by default)

## Features (v0.3.0 UI)

- **Cyberpunk skin**: Picks up ModMenu's shared skin via reflection
- **Minimizable**: Collapse to title bar with — button
- **Resizable**: Drag ◢ handle at bottom-right
- **Opaque background**: Explicit `GUI.DrawTexture` fill
- **Section toggles**: Resource Signals, Market Signals, Trader Signals, Technical Data
- **Throttled refresh**: At most once per second to avoid stutter

## Controls

```text
¡             open or close Economy Debug
Toggle buttons   show/hide individual sections
```

Hotkey changeable from ModMenu.

## Performance

- Cached snapshot, refreshes max once per second
- Reflection lookups cached (`typeCache`, `fieldCache`, `methodCache`)
- Disabled sections are not recalculated
- Display counts capped (12 per category)

## Config

```text
BepInEx\config\local.spacefleet.economy-debug.cfg
```

## Game Classes Inspected

```text
FactionsManager, GlobalMarket, AllResources, Market, Trader,
Inventory, ResourceDefinition
```

## How It Works

- IMGUI window `GUI.Window(42012, ...)`, `GUI.depth = -1002`
- Skin + bg texture from ModMenu via reflection (with fallback)
- Economy data read via `FindCurrent()` + field/method reflection
- Defensive invocation: broken methods show safe fallback values

## Files

```text
EconomyDebugPlugin.cs
SpacefleetEconomyDebug.csproj
```
