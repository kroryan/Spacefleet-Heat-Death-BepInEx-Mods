# Spacefleet Economy Overhaul

`SpacefleetEconomyOverhaul` is the economy balancing mod. It fixes critical
fuel market deadlocks, adjusts prices based on real supply/demand, and ensures
trade flow remains sustainable long-term.

**Version**: 0.4.1
**GUID**: `local.spacefleet.economy-overhaul`
**Hotkey**: None (background patches)

## What It Does

### Price Balancing (v0.1.0)

Patches `Market.GetCurrentPrice` (HarmonyPostfix) to apply:

1. **Local stock pressure**: Scarcity raises prices, abundance lowers them
2. **Global supply/demand pressure**: Based on `GlobalMarket.GetSupplyOf/GetDemandOf`
3. **Base-price clamps**: Prevents absurd prices (`MinPriceRatio` to `MaxPriceRatio`)

### Fuel Market Fix (v0.2.0 → v0.4.0)

Fixes a critical vanilla bug where fuel stockpiles become untradeable.
v0.3.0 rewrote all patches to use **typed access** via direct Assembly-CSharp
reference, eliminating ~3,500 reflection calls per game hour.
v0.4.0 found and fixed the root cause: inflated fuel `stockRatios` made
`Market.GetMinStockQuantity` return values exceeding storage capacity, so
`CanBuy` computed 0 available fuel everywhere → "NO AVAILABLE FUEL".

4. **Resupply Rate Fix + Fuel StockRatio Cap** (HarmonyPostfix on `Market.Start`):
   Vanilla can leave `resupplyRatePerHour = 0` on some station prefabs,
   causing `CanBuy` and `ExecuteTrade` to cap trade at `Floor(0) = 0`.
   This patch fixes it once at load by enforcing `MinResupplyRate` (default 2.0).
   v0.4.0 also caps fuel `stockRatios` to `SurplusDumpRatio` on market init,
   preventing the inflated-reserve deadlock.

5. **CanBuy Fuel Override** (HarmonyPostfix on `Market.CanBuy`):
   v0.4.0 completely rewrites fuel availability. For fuel resources, ignores
   vanilla's broken `minStockQuantity` calculation (which uses inflated
   `stockRatios`) and instead reserves only `FuelReserveMaxRatio` (default
   10%) of current stock. This ensures fuel is always purchasable when a
   station has stock. Non-fuel resources use the vanilla result with a
   `MinimumTradeQuantity` floor.

6. **Stock Ratio Recovery + Fuel Cap** (HarmonyPostfix on `Factory.ProductionCycle`):
   Vanilla `Factory.AddResourcesDelayed` drives stockRatios for outputs to 0
   during init (calls `ModifyDemand(output, -0.1)` up to 100 times). Once at
   0, they never recover. This patch gradually increases stockRatios for
   overproduced resources, pushing surplus onto the market.
   v0.4.0 also caps fuel `stockRatios` and limits `targetRatio` to
   `min(SurplusDumpRatio, 0.5)` to prevent re-inflation.

7. **Seller Filter Relaxation** (HarmonyPostfix on `GlobalMarket.GetBestSellerFromList`):
   Vanilla only considers sellers priced at or below the average buy price.
   When all stations have inflated prices (due to broken stockRatios), this
   filter excludes every seller, creating a deadlock. This patch retries
   without the price ceiling if the original search found nothing.

8. **Smart Scarcity Detection**: When stockRatio = 0, the original
   `GetLocalStockMultiplier` treated it as extreme scarcity (+35% price
   premium), even for stations overflowing with fuel. Now uses actual
   inventory fill ratio instead.

Also patches:

- `Trader.TradeCycle` / `Trader.Trade`: Raises the *base* profit margin
  (`minProfitMargin`) to the configured minimum. Does NOT clamp
  `currentMinProfitMargin`, so vanilla's relaxation loop (halving until
  −100000) still works and traders never get stuck.

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
MinimumTradeQuantity = 0.5
MinimumAutoTraderProfitMargin = 0.05

[TradeFlow]
MinResupplyRate = 2.0
FuelResupplyMultiplier = 10.0
StockRatioRecoveryRate = 0.5
StockRatioRecoveryThreshold = 0.5
SurplusDumpRatio = 0.15
FuelReserveMaxRatio = 0.10

[Performance]
PriceCacheSeconds = 2.0
```

### Config Options (v0.2.0+)

| Setting | Default | Description |
|---------|---------|-------------|
| MinResupplyRate | 2.0 | Floor for `resupplyRatePerHour` (vanilla can be 0) |
| FuelResupplyMultiplier | 10.0 | Trade rate multiplier for fuel types |
| StockRatioRecoveryRate | 0.5 | How fast stockRatios recover per production cycle |
| StockRatioRecoveryThreshold | 0.5 | Inventory fill ratio that triggers recovery |
| SurplusDumpRatio | 0.15 | Max stockRatio for fuel; target for surplus recovery (was 5.0 in v0.3.0, auto-migrated) |
| FuelReserveMaxRatio | 0.10 | Fraction of fuel stock reserved (not sold) per station |

## The Fuel Deadlock Problem (Why This Fix Exists)

In vanilla, fuel-producing stations can accumulate 1000+ units of DT/DH fuel
while the global market shows nothing available. Root causes:

1. `Factory.AddResourcesDelayed` runs 100 init cycles, each calling
   `Market.ModifyDemand(output, -0.1)`. This drives output stockRatios to 0.

2. When stockRatio = 0, `Market.GetMinStockQuantity` returns 0 (station
   reserves nothing). But `GetCurrentPrice` ignores actual inventory and uses
   a fixed `idealStockRatio`, making prices disconnected from reality.

3. `Market.CanBuy` caps trade to `Floor(resupplyRatePerHour * 10)`. If
   resupplyRate is 0 (possible on some station prefabs), Floor(0) = 0 and
   NO trade can occur.

4. (v0.4.0) **The v0.3.0 SurplusDumpRatio=5.0 bug**: The stock ratio recovery
   patch was driving fuel stockRatios UP to 5.0. Since
   `GetMinStockQuantity = storageMax * stockRatio`, a stockRatio of 5.0 meant
   the station "reserved" 500% of its storage — more than it could hold.
   `CanBuy` then computed `available = inventory - minStock` as negative → 0.
   Every station returned 0 available fuel → "NO AVAILABLE FUEL" everywhere.

4. `GlobalMarket.GetBestSellerFromList` checks `CanBuy(res, 1, 1M) > 0` —
   if every station fails this check, zero sellers exist globally.

This creates a feedback loop: fuel piles up, prices don't drop, nobody can
buy, nobody can sell, the economy stalls.

## Performance

v0.3.0 eliminated ALL reflection from hot paths by referencing Assembly-CSharp
directly (typed access). Previous versions used ~8 reflection calls per
`GetCurrentPrice` invocation × 448 calls per game hour = ~3,500 reflection
calls, causing periodic stutters at high game speeds.

Now optimized via:

1. **Typed access** to Market, GlobalMarket, Factory, Trader fields
2. `AccessTools.FieldRefAccess` for private fields (one-time delegate creation)
3. Caching global demand multiplier per resource with configurable expiry
4. Cache expires based on `PriceCacheSeconds` (default 2s)

The stock ratio recovery runs once per production cycle (daily), not per frame.

## Changelog

### v0.4.1 — Trader Deadlock Fix
- **Fixed**: Trading fleets endlessly switching target market without ever
  arriving. Patch 6 (`TraderMarginPatch`) was clamping `currentMinProfitMargin`
  to the configured floor (0.05), which prevented vanilla's relaxation loop
  from reaching the −100000 "accept any trade" escape value. The trader would
  oscillate between margin 0.05 and 0.025 forever, re-evaluating targets each
  second. Fix: now raises `minProfitMargin` (the base/reset value) instead.

### v0.4.0 — Fuel Deadlock Fix
- **Root cause fix**: `SurplusDumpRatio` default changed from 5.0 → 0.15
  (auto-migrated from old configs)
- **New `FuelReserveMaxRatio`** config (default 0.10): controls how much fuel
  each station keeps in reserve
- **`CanBuy` rewritten for fuel**: ignores vanilla's broken `minStockQuantity`;
  uses simple reserve fraction instead
- **Fuel stockRatio capping** in `Market.Start` and `Factory.ProductionCycle`
- **`IsFuel()` helper**: detects VOLATILES, DT_FUEL, DH_FUEL resource types

### v0.3.0 — Typed Access Rewrite
- Eliminated all reflection from hot paths
- Direct Assembly-CSharp reference for typed field access
- `AccessTools.FieldRefAccess` for private fields

### v0.2.0 — Fuel Market Patches
- Initial fuel deadlock fixes (resupply rate, CanBuy, stock ratio recovery)

### v0.1.0 — Price Balancing
- `GetCurrentPrice` postfix with supply/demand adjustments

## What It Does NOT Modify

```text
inventories, credits, saves, traders, trade routes,
production, consumption, resource lists
```

Price adjustments are applied to `Market.GetCurrentPrice` return values.
Fuel availability is adjusted in `Market.CanBuy` for fuel types only.
Stock ratios are capped for fuel to prevent the deadlock.

## How It Works

- 6 Harmony postfixes on Market, GlobalMarket, Factory, and Trader methods
- `Market.Start`: One-time resupplyRatePerHour fix at station load
- `Market.GetCurrentPrice`: Applies `localMultiplier * globalMultiplier` and clamps
- `Market.CanBuy`: Safety net when vanilla returns 0 despite available stock
- `Factory.ProductionCycle`: Gradual stockRatio recovery for surplus outputs
- `GlobalMarket.GetBestSellerFromList`: Retry without price ceiling on no results
- `Trader.TradeCycle`/`Trader.Trade`: Minimum profit margin enforcement
- No UI window (background-only mod)
- Requires Assembly-CSharp.dll reference (included in csproj HintPath)

## Related Mods

Use with `SpacefleetEconomyDebug` to inspect impact.
Use with `SpacefleetConsole` (`economy` command) to query market state.

## Files

```text
EconomyOverhaulPlugin.cs
SpacefleetEconomyOverhaul.csproj
```
