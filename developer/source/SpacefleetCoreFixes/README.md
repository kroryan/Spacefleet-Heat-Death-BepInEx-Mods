# Spacefleet Core Fixes

`SpacefleetCoreFixes` contains small, safe global fixes for the game.

**Version**: 0.1.0
**GUID**: `local.spacefleet.core-fixes`
**Hotkey**: None (background patches)

## What It Does

### Money Sound Limiter

Limits `AudioManager.PlayMoney` to 2 plays per 30 seconds. Prevents audio spam
from repeated credit changes without affecting actual money logic.

### Fleet Delta-V Softlock Recovery

Watches `FleetManager.UpdateDv`. If a player fleet (factionID == 1) has zero
usable delta-v while having positive max delta-v, applies emergency recovery:

- Repair fuel/drive modules to minimum health ratio
- Add small emergency fuel reserve
- Recalculate fleet delta-v
- Clear `Navigation.isInsufficientDv`

This is a rescue, not a free refuel — just enough to escape a softlock.

## Config

```text
BepInEx\config\local.spacefleet.core-fixes.cfg
```

```ini
[FleetRecovery]
Enabled = true
EmergencyFuelRatio = 0.05
MinimumFuelDriveHealth = 0.25
CooldownSeconds = 60
```

## How It Works

- Harmony patches applied via `harmony.PatchAll()` during `Awake`
- Money limiter: `HarmonyPrefix` on `AudioManager.PlayMoney`
- Fleet recovery: `HarmonyPostfix` on `FleetManager.UpdateDv`
- Uses `Time.unscaledTime` for timing (works regardless of timeScale)
- No UI window (background-only mod)

## Testing

Expected log lines:

```text
Spacefleet Core Fixes loaded.
Money sound limiter active: max 2 plays per 30 seconds.
```

Recovery log:

```text
[Spacefleet Core Fixes] Emergency fleet recovery applied to a player fleet stranded at zero delta-v.
```

## Files

```text
CoreFixesPlugin.cs
SpacefleetCoreFixes.csproj
```
