# Spacefleet Economy Overhaul

`SpacefleetEconomyOverhaul` is the economy balancing mod. It adjusts market
prices conservatively without rewriting the economy or touching saves.

**Version**: 0.1.0
**GUID**: `local.spacefleet.economy-overhaul`
**Hotkey**: None (background patches)

## What It Does

Patches `Market.GetCurrentPrice` (HarmonyPostfix) to apply:

1. **Local stock pressure**: Scarcity raises prices, abundance lowers them
2. **Global supply/demand pressure**: Based on `GlobalMarket.GetSupplyOf/GetDemandOf`
3. **Base-price clamps**: Prevents absurd prices (`MinPriceRatio` to `MaxPriceRatio`)

Also patches:

- `Market.CanBuy`: Blocks dust trades below `MinimumTradeQuantity`
- `Trader.TradeCycle` / `Trader.Trade`: Enforces minimum auto-trader profit margin

## Config

```text
BepInEx\config\local.spacefleet.economy-overhaul.cfg
```

```ini
[General]
Enabled = true

[Prices]
MinPriceRatio = 0.35
MaxPriceRatio = 4.0
ScarcityPremium = 0.35
AbundanceDiscount = 0.20

[GlobalMarket]
GlobalDemandWeight = 0.25
GlobalDemandMinMultiplier = 0.80
GlobalDemandMaxMultiplier = 1.25

[Trade]
MinimumTradeQuantity = 1.0
MinimumAutoTraderProfitMargin = 0.05

[Performance]
PriceCacheSeconds = 2.0
```

## Performance

This mod touches a hot path (`Market.GetCurrentPrice` runs many times per
frame). It optimizes by:

1. Caching `FieldInfo` and `MethodInfo`
2. Caching local multiplier per market/resource pair
3. Caching global multiplier per resource
4. Cache expires based on `PriceCacheSeconds` (default 2s)

## What It Does NOT Modify

```text
inventories, credits, saves, traders, trade routes,
production, consumption, resource lists
```

Only the numeric value returned by `Market.GetCurrentPrice` is changed.

## How It Works

- HarmonyPostfix on `Market.GetCurrentPrice`: game calculates vanilla price,
  then mod applies `localMultiplier * globalMultiplier` and clamps
- Local multiplier from `Market.GetStockRatio` vs `idealStockRatio`
- Global multiplier from `demand/supply` ratio softened by `GlobalDemandWeight`
- No UI window (background-only mod)

## Related Mods

Use with `SpacefleetEconomyDebug` to inspect impact.
Use with `SpacefleetConsole` (`economy` command) to query market state.

## Files

```text
EconomyOverhaulPlugin.cs
SpacefleetEconomyOverhaul.csproj
```
