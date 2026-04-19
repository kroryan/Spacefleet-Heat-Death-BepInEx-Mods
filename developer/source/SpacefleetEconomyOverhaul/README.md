# Spacefleet Economy Overhaul

`SpacefleetEconomyOverhaul` is the economy balancing mod. It fixes critical
fuel market deadlocks, adjusts prices based on real supply/demand, and ensures
trade flow remains sustainable long-term.

**Version**: 0.3.0
**GUID**: `local.spacefleet.economy-overhaul`
**Hotkey**: None (background patches)

## What It Does

### Price Balancing (v0.1.0)

Patches `Market.GetCurrentPrice` (HarmonyPostfix) to apply:

1. **Local stock pressure**: Scarcity raises prices, abundance lowers them
2. **Global supply/demand pressure**: Based on `GlobalMarket.GetSupplyOf/GetDemandOf`
3. **Base-price clamps**: Prevents absurd prices (`MinPriceRatio` to `MaxPriceRatio`)

### Fuel Market Fix (v0.2.0 → v0.3.0 rewrite)

Fixes a critical vanilla bug where fuel stockpiles become untradeable.
v0.3.0 rewrote all patches to use **typed access** via direct Assembly-CSharp
reference, eliminating ~3,500 reflection calls per game hour.

4. **Resupply Rate Fix** (HarmonyPostfix on `Market.Start`):
   Vanilla can leave `resupplyRatePerHour = 0` on some station prefabs,
   causing `CanBuy` and `ExecuteTrade` to cap trade at `Floor(0) = 0`.
   This patch fixes it once at load by enforcing `MinResupplyRate` (default 2.0).

5. **CanBuy Safety Net** (HarmonyPostfix on `Market.CanBuy`):
   If vanilla returned 0 but the station has stock and credits, checks if the
   resupply rate was the bottleneck and recalculates. Also enforces
   `MinimumTradeQuantity`. Changed from Prefix (v0.2.0) to Postfix (v0.3.0)
   to avoid reimplementing vanilla logic.

6. **Stock Ratio Recovery** (HarmonyPostfix on `Factory.ProductionCycle`):
   Vanilla `Factory.AddResourcesDelayed` drives stockRatios for outputs to 0
   during init (calls `ModifyDemand(output, -0.1)` up to 100 times). Once at
   0, they never recover. This patch gradually increases stockRatios for
   overproduced resources, pushing surplus onto the market.

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
MinimumTradeQuantity = 0.5
MinimumAutoTraderProfitMargin = 0.05

[TradeFlow]
MinResupplyRate = 2.0
FuelResupplyMultiplier = 10.0
StockRatioRecoveryRate = 0.5
StockRatioRecoveryThreshold = 0.5
SurplusDumpRatio = 5.0

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
| SurplusDumpRatio | 5.0 | Target minimum stockRatio for surplus outputs |

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

## What It Does NOT Modify

```text
inventories, credits, saves, traders, trade routes,
production, consumption, resource lists
```

Only the numeric value returned by `Market.GetCurrentPrice` is changed.

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
