# Economy Modding Notes

These notes document the first local pass over the Spacefleet - Heat Death
economy code. They are based on inspection of `Assembly-CSharp.dll`.

## Main Economy Classes

Important types:

```text
Faction
FactionData
FactionsManager
MoneyPanelManager
ResourceDefinition
ResourceInventory
Market
GlobalMarket
Trader
Factory
ProductionRecipe
AllResources
AllRecipes
AudioManager
```

## Credits

Player and faction money is stored as integer credits.

Relevant fields:

```text
Faction.credits
FactionData.credits
FactionsManager.playerFaction
```

Relevant method:

```text
FactionsManager.ModPlayerCredits(string header, int mod)
```

`ModPlayerCredits` adds `mod` to the player faction credits and creates a
notification. Positive changes use a green `+` message. Negative changes use
orange text.

The money panel reads:

```text
FactionsManager.playerFaction.credits
```

Then it animates from the displayed value to the target value.

## Money Sound Bug

The money sound is triggered here:

```text
MoneyPanelManager.Update -> AudioManager.PlayMoney(bool isMoneyGoUp)
```

`MoneyPanelManager.Update` calls `PlayMoney` while the displayed credits are
still moving toward the real target credits. Big money changes can keep that
animation alive for a long time, so the sound can repeat again and again.

Current local fix:

```text
SpacefleetCoreFixes patches AudioManager.PlayMoney with Harmony.
Allow at most 2 money sound plays per 30 seconds.
Suppress further calls during that window.
```

This is safer than editing `MoneyPanelManager.Update` because every money sound
still goes through one central method.

## Resources

Resources are ScriptableObject-style definitions.

Relevant fields:

```text
ResourceDefinition.resourceName
ResourceDefinition.description
ResourceDefinition.icon
ResourceDefinition.type
ResourceDefinition.basePrice
```

An inventory stores quantities of resources:

```text
ResourceInventory.resources
ResourceInventory.storageUsed
ResourceInventory.storageMax
```

Relevant methods:

```text
ResourceInventory.GetQuantityOf(ResourceDefinition resource)
ResourceInventory.AddResource(ResourceDefinition resource, float quantity)
ResourceInventory.RemoveResource(ResourceDefinition resource, float quantity)
ResourceInventory.ModifyResource(ResourceDefinition resource, float mod)
ResourceInventory.GetFreeSpace()
```

## Markets

A `Market` owns an inventory and calculates prices from stock levels.

Important fields:

```text
Market.inventory
Market.stockRatios
Market.buyPrices
Market.buySensitivity
Market.sellSensitivity
Market.MaxFluctuationRatio
Market.idealStockRatio
Market.transactionSpread
Market.resupplyRatePerHour
```

Important methods:

```text
Market.UpdatePrices()
Market.GetCurrentPrice(ResourceDefinition resource, bool isBuying)
Market.CanBuy(ResourceDefinition resource, float quantity, int factionCredits)
Market.ExecuteTrade(ResourceDefinition resource, ref float quantity, ref int factionCredits, ref int totalCost)
Market.ModifyDemand(ResourceDefinition resource, float mod)
```

Price calculation currently depends heavily on:

```text
ResourceDefinition.basePrice
current stock quantity
minimum/ideal stock quantity
idealStockRatio
transactionSpread
```

Observed behavior:

- If a resource, base price, or inventory is invalid, price returns 0.
- Buy and sell prices are different.
- The market applies a spread around the calculated price.
- Scarcity increases price; abundance lowers price.
- `Market.UpdatePrices` rebuilds the market's buy price list from current stock.

## Trade

Relevant type:

```text
Trader
```

Important fields:

```text
Trader.currentMarket
Trader.targetMarket
Trader.targetResource
Trader.targetQuantity
Trader.totalStorageSpace
Trader.isBuying
Trader.buyPrice
Trader.bestSellPrice
Trader.profitMargin
Trader.minProfitMargin
Trader.currentMinProfitMargin
Trader.isTradeValid
```

Important methods:

```text
Trader.TradeCycle()
Trader.Trade(ref int credits, ref float realQuantity)
Trader.StartFlight()
Trader.OnArrival()
Trader.StopTrade()
```

Observed behavior:

- Traders search markets via `GlobalMarket`.
- Traders buy resources from one market and sell to another.
- Selling logic checks whether profit is still above `currentMinProfitMargin`.
- If profit falls too low, the trader reduces the minimum margin; if it becomes
  extremely low, trading stops.
- Low fleet delta-v can push the trader toward fuel buying behavior.

## Global Market

Relevant type:

```text
GlobalMarket
```

Important fields:

```text
GlobalMarket.allMarkets
GlobalMarket.buyPrices
GlobalMarket.sellPrices
GlobalMarket.supplys
GlobalMarket.demands
```

Important methods:

```text
GlobalMarket.UpdateAveragePrices()
GlobalMarket.GetAverageBuyPriceOf(ResourceDefinition resource)
GlobalMarket.GetAverageSellPriceOf(ResourceDefinition resource)
GlobalMarket.GetBestBuyerOf(...)
GlobalMarket.GetBestSellerOf(...)
GlobalMarket.GetBestSellerAnyExceptHostile(...)
GlobalMarket.GetSellerOfTypeAbundantExceptHostile(...)
GlobalMarket.GetSupplyOf(ResourceDefinition resource)
GlobalMarket.GetDemandOf(ResourceDefinition resource)
```

This is a good patch point for economy-wide balancing because it already has
visibility over all markets.

## Production

Factories convert inputs into outputs via recipes.

Relevant fields:

```text
Factory.inventory
Factory.market
Factory.recipes
ProductionRecipe.RecipeName
ProductionRecipe.Inputs
ProductionRecipe.Outputs
ProductionRecipe.CyclesPerBatch
ProductionRecipe.alwaysConsume
```

Important methods:

```text
Factory.CanProduceBatch(ProductionRecipe recipe)
Factory.ExecuteBatchProduction(ProductionRecipe recipe)
Factory.GetDemands()
Factory.GetSupplys()
```

Observed behavior:

- Normal recipes require available input resources.
- Output must fit in storage.
- `alwaysConsume` recipes can pull inputs from the linked market by executing
  trades rather than only using factory inventory.

## Good Mod Targets

Low-risk first patches:

```text
AudioManager.PlayMoney
FactionsManager.ModPlayerCredits
Market.GetCurrentPrice
Market.CanBuy
Trader.TradeCycle
Factory.CanProduceBatch
```

Higher-risk patches:

```text
Market.ExecuteTrade
Trader.Trade
GlobalMarket.GetBestSeller...
GlobalMarket.GetBestBuyer...
Factory.ExecuteBatchProduction
```

## Ideas For A Better Economy Mod

1. Add configurable price volatility.

   Patch `Market.GetCurrentPrice` or post-process its result. Expose config
   values for scarcity strength, abundance discount, and transaction spread.

2. Add stronger logistics friction.

   Traders should consider distance, fuel, danger, and cargo capacity before
   accepting routes. The current route logic already checks markets and delta-v,
   so `Trader.TradeCycle` is the likely entry point.

3. Add smoother supply and demand memory.

   Instead of prices responding only to current stock, keep a short history of
   recent purchases/sales per market and resource. Increase demand if a market
   repeatedly buys the same resource; reduce it when supply recovers.

4. Make production affect markets more clearly.

   Factories expose `GetDemands` and `GetSupplys`. Those could feed into
   `GlobalMarket` so industrial stations create visible demand for inputs and
   visible supply of outputs.

5. Add anti-cheat or debug-money safeguards.

   Large credit changes could be collapsed into one notification and one sound.
   This prevents UI/audio spam without blocking legitimate money changes.

6. Add config-based balance profiles.

   Example profiles:

   ```text
   VanillaPlus
   RealisticScarcity
   HighTradeVolume
   SlowIndustrialGrowth
   ```

## Recommended Next Economy Patch

Implemented in `SpacefleetEconomyOverhaul`: the safest gameplay-facing economy
patch is:

```text
Postfix Market.GetCurrentPrice(ResourceDefinition resource, bool isBuying)
```

With configurable multipliers:

```text
buySpreadMultiplier
sellSpreadMultiplier
scarcityMultiplier
abundanceDiscountMultiplier
minimumPriceRatio
maximumPriceRatio
```

That lets us tune the economy without rewriting trade execution or inventory
mutation.

Current `SpacefleetEconomyOverhaul` behavior:

```text
Patch Market.GetCurrentPrice(ResourceDefinition resource, bool isBuying)
Patch Market.CanBuy(ResourceDefinition resource, float quantity, int factionCredits)
Patch Trader.TradeCycle(...)
Patch Trader.Trade(...)
Keep vanilla trade execution, inventory mutation, and trader decisions intact.
Apply additional local scarcity and abundance pressure.
Apply global demand/supply pressure.
Clamp final price between configurable base-price ratios.
Cache reflection and multiplier results so price queries do not recalculate the
whole economy every frame.
Reject dust trades below MinimumTradeQuantity.
Clamp auto-trader margin relaxation to MinimumAutoTraderProfitMargin.
```

## Economy Review Findings

The economy review focused on `Market`, `GlobalMarket`, `Trader`,
`FactionsManager`, `Faction`, `ResourceInventory`, `ResourceDefinition`, and
`AllResources`.

Important findings:

```text
Market.GetCurrentPrice is a hot path and is called by GlobalMarket averaging.
Market.CanBuy can return very small positive trade quantities.
Trader.Trade uses executed quantity in price accounting.
Trader.TradeCycle relaxes currentMinProfitMargin when no profitable route exists.
GlobalMarket.UpdateAveragePrices recomputes buy/sell averages across all markets.
```

Implemented fixes:

```text
Price multiplier caching prevents expensive work on every price query.
MinimumTradeQuantity blocks tiny positive trades before they enter execution.
MinimumAutoTraderProfitMargin keeps automated trade from degrading into bad deals.
Economy Debug now exposes actionable player-facing signals instead of raw spam.
```
