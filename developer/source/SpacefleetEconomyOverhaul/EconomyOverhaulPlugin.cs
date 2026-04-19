using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SpacefleetEconomyOverhaul
{
    [BepInPlugin("local.spacefleet.economy-overhaul", "Spacefleet Economy Overhaul", "0.4.0")]
    public sealed class EconomyOverhaulPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> MinPriceRatio;
        internal static ConfigEntry<float> MaxPriceRatio;
        internal static ConfigEntry<float> ScarcityPremium;
        internal static ConfigEntry<float> AbundanceDiscount;
        internal static ConfigEntry<float> GlobalDemandWeight;
        internal static ConfigEntry<float> GlobalDemandMinMultiplier;
        internal static ConfigEntry<float> GlobalDemandMaxMultiplier;
        internal static ConfigEntry<float> PriceCacheSeconds;
        internal static ConfigEntry<float> MinimumTradeQuantity;
        internal static ConfigEntry<float> MinimumAutoTraderProfitMargin;

        internal static ConfigEntry<float> MinResupplyRate;
        internal static ConfigEntry<float> FuelResupplyMultiplier;
        internal static ConfigEntry<float> StockRatioRecoveryRate;
        internal static ConfigEntry<float> StockRatioRecoveryThreshold;
        internal static ConfigEntry<float> SurplusDumpRatio;
        internal static ConfigEntry<float> FuelReserveMaxRatio;

        private Harmony harmony;

        internal static bool IsFuel(ResourceDefinition resource)
        {
            return resource != null &&
                (resource.type == ResourceType.VOLATILES ||
                 resource.type == ResourceType.DT_FUEL ||
                 resource.type == ResourceType.DH_FUEL);
        }

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Enable economy price balancing.");

            MinPriceRatio = Config.Bind("Prices", "MinPriceRatio", 0.35f, "Lowest allowed price as a fraction of resource base price.");
            MaxPriceRatio = Config.Bind("Prices", "MaxPriceRatio", 4.0f, "Highest allowed price as a multiple of resource base price.");
            ScarcityPremium = Config.Bind("Prices", "ScarcityPremium", 0.35f, "Extra price pressure when a market is below ideal stock.");
            AbundanceDiscount = Config.Bind("Prices", "AbundanceDiscount", 0.20f, "Discount pressure when a market is above ideal stock.");

            GlobalDemandWeight = Config.Bind("GlobalMarket", "GlobalDemandWeight", 0.25f, "How strongly global demand/supply affects local prices.");
            GlobalDemandMinMultiplier = Config.Bind("GlobalMarket", "GlobalDemandMinMultiplier", 0.80f, "Minimum global demand multiplier.");
            GlobalDemandMaxMultiplier = Config.Bind("GlobalMarket", "GlobalDemandMaxMultiplier", 1.25f, "Maximum global demand multiplier.");

            MinimumTradeQuantity = Config.Bind("Trade", "MinimumTradeQuantity", 0.5f,
                "Reject market trades smaller than this to avoid dust trades.");
            MinimumAutoTraderProfitMargin = Config.Bind("Trade", "MinimumAutoTraderProfitMargin", 0.05f,
                "Lowest profit margin auto-traders are allowed to relax to.");

            MinResupplyRate = Config.Bind("TradeFlow", "MinResupplyRate", 2.0f,
                "Minimum resupplyRatePerHour enforced on all markets. Vanilla can be 0, which blocks ALL trade.");
            FuelResupplyMultiplier = Config.Bind("TradeFlow", "FuelResupplyMultiplier", 10.0f,
                "Multiplier for fuel trade rate (vanilla is 10x). Increase to let more fuel flow per hour.");
            StockRatioRecoveryRate = Config.Bind("TradeFlow", "StockRatioRecoveryRate", 0.5f,
                "How fast stockRatios recover per production cycle for outputs with surplus.");
            StockRatioRecoveryThreshold = Config.Bind("TradeFlow", "StockRatioRecoveryThreshold", 0.5f,
                "If inventory fill ratio exceeds this, stock ratio recovery kicks in.");
            SurplusDumpRatio = Config.Bind("TradeFlow", "SurplusDumpRatio", 0.15f,
                "Target stockRatio for surplus outputs (fraction of storageMax). v0.3.0 had 5.0 which was a bug.");
            FuelReserveMaxRatio = Config.Bind("TradeFlow", "FuelReserveMaxRatio", 0.10f,
                "Max fraction of fuel inventory a station can reserve. Rest is always available for sale.");

            // v0.3.0 -> v0.4.0 migration: fix the broken SurplusDumpRatio
            if (SurplusDumpRatio.Value > 1.0f)
            {
                Log.LogWarning("SurplusDumpRatio was " + SurplusDumpRatio.Value.ToString("F2") +
                    " (v0.3.0 bug: caused stations to hoard all fuel). Resetting to 0.15.");
                SurplusDumpRatio.Value = 0.15f;
            }

            PriceCacheSeconds = Config.Bind("Performance", "PriceCacheSeconds", 2.0f,
                "Seconds to cache global demand multipliers.");

            harmony = new Harmony("local.spacefleet.economy-overhaul");
            harmony.PatchAll();
            Logger.LogInfo("Economy Overhaul v0.4.0 loaded – fuel drought fix, stock ratio bugfix.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    // =========================================================================
    // PATCH 1: Fix resupplyRatePerHour AND fuel stockRatios at Market.Start
    // =========================================================================
    // Two problems fixed at startup:
    // 1. resupplyRatePerHour = 0 blocks ALL trade on some station prefabs
    // 2. Fuel stockRatios can be absurdly high, causing minStockQuantity to
    //    exceed actual inventory → GetQuantityAvailable returns 0 → CanBuy
    //    returns 0 → "NO AVAILABLE FUEL" for every fleet trying to refuel
    // =========================================================================
    [HarmonyPatch(typeof(Market), "Start")]
    internal static class MarketStartPatch
    {
        private static readonly AccessTools.FieldRef<Market, List<ResourceQuantity>> stockRatiosRef =
            AccessTools.FieldRefAccess<Market, List<ResourceQuantity>>("stockRatios");

        [HarmonyPostfix]
        private static void Postfix(Market __instance)
        {
            if (!EconomyOverhaulPlugin.Enabled.Value) return;

            // Fix 1: Ensure minimum resupply rate
            float min = EconomyOverhaulPlugin.MinResupplyRate.Value;
            if (__instance.resupplyRatePerHour < min)
            {
                EconomyOverhaulPlugin.Log.LogInfo(
                    "Fixed resupplyRate " + __instance.resupplyRatePerHour.ToString("F1") +
                    " -> " + min.ToString("F1") + " on " + __instance.name);
                __instance.resupplyRatePerHour = min;
            }

            // Fix 2: Cap fuel stockRatios immediately
            float fuelCap = EconomyOverhaulPlugin.FuelReserveMaxRatio.Value;
            List<ResourceQuantity> stockRatios = stockRatiosRef(__instance);
            if (stockRatios != null)
            {
                for (int i = 0; i < stockRatios.Count; i++)
                {
                    ResourceQuantity rq = stockRatios[i];
                    if (rq.resource == null) continue;

                    if (EconomyOverhaulPlugin.IsFuel(rq.resource))
                    {
                        float currentRatio = rq.quantity * 0.01f;
                        if (currentRatio > fuelCap)
                        {
                            EconomyOverhaulPlugin.Log.LogInfo(
                                "Capped fuel stockRatio " + rq.resource.name +
                                " from " + currentRatio.ToString("F2") +
                                " to " + fuelCap.ToString("F2") + " on " + __instance.name);
                            rq.quantity = fuelCap * 100f;
                        }
                    }
                }
            }
        }
    }

    // =========================================================================
    // PATCH 2: GetCurrentPrice – price balancing with direct typed access
    // =========================================================================
    // v0.2.0 used reflection for every field/method access (8+ calls per price
    // query). With Assembly-CSharp reference, we use direct typed access.
    // GetCurrentPrice is called 448+ times per game hour by UpdateAveragePrices.
    // =========================================================================
    [HarmonyPatch(typeof(Market), "GetCurrentPrice")]
    internal static class MarketGetCurrentPricePatch
    {
        private static readonly Dictionary<ResourceDefinition, CachedFloat> globalCache =
            new Dictionary<ResourceDefinition, CachedFloat>();

        // Direct field ref for private stockRatios (delegate, no per-call reflection)
        private static readonly AccessTools.FieldRef<Market, List<ResourceQuantity>> stockRatiosRef =
            AccessTools.FieldRefAccess<Market, List<ResourceQuantity>>("stockRatios");

        [HarmonyPostfix]
        private static void Postfix(Market __instance, ResourceDefinition resource, bool isBuying, ref int __result)
        {
            if (!EconomyOverhaulPlugin.Enabled.Value || resource == null || __result <= 0 || resource.basePrice <= 0)
                return;

            float price = __result;
            price *= GetLocalStockMultiplier(__instance, resource);
            price *= GetGlobalDemandMultiplier(resource);

            float min = Mathf.Max(1f, resource.basePrice * EconomyOverhaulPlugin.MinPriceRatio.Value);
            float max = Mathf.Max(min, resource.basePrice * EconomyOverhaulPlugin.MaxPriceRatio.Value);
            __result = Mathf.RoundToInt(Mathf.Clamp(price, min, max));
        }

        private static float GetLocalStockMultiplier(Market market, ResourceDefinition resource)
        {
            float idealStockRatio = Mathf.Max(0.01f, market.idealStockRatio);

            // Read stockRatio from private field (quantity * 0.01 is vanilla scaling)
            float stockRatio = 0f;
            List<ResourceQuantity> ratios = stockRatiosRef(market);
            if (ratios != null)
            {
                for (int i = 0; i < ratios.Count; i++)
                {
                    if (ratios[i].resource == resource)
                    {
                        stockRatio = ratios[i].quantity * 0.01f;
                        break;
                    }
                }
            }

            // FIX: When stockRatio ~0 (driven there by Factory.AddResourcesDelayed),
            // use actual inventory fill ratio instead of the broken zero
            if (stockRatio < 0.01f && market.inventory != null)
            {
                float qty = market.inventory.GetQuantityOf(resource);
                float storageMax = market.inventory.storageMax;
                if (storageMax > 0f)
                    stockRatio = Mathf.Clamp(qty / storageMax, 0f, 1f);
            }

            float multiplier = 1f;
            if (stockRatio < idealStockRatio)
            {
                float scarcity = Mathf.Clamp01((idealStockRatio - stockRatio) / idealStockRatio);
                multiplier += scarcity * EconomyOverhaulPlugin.ScarcityPremium.Value;
            }
            else
            {
                float abundance = Mathf.Clamp01((stockRatio - idealStockRatio) / Mathf.Max(idealStockRatio, 0.01f));
                multiplier -= abundance * EconomyOverhaulPlugin.AbundanceDiscount.Value;
            }

            return Mathf.Clamp(multiplier, 0.5f, 1.75f);
        }

        private static float GetGlobalDemandMultiplier(ResourceDefinition resource)
        {
            float now = Time.unscaledTime;
            if (globalCache.TryGetValue(resource, out CachedFloat cached) && cached.ExpiresAt > now)
                return cached.Value;

            GlobalMarket gm = GlobalMarket.current;
            if (gm == null) return 1f;

            float supply = Mathf.Max(0.01f, gm.GetSupplyOf(resource));
            float demand = Mathf.Max(0.01f, gm.GetDemandOf(resource));
            float ratio = Mathf.Clamp(demand / supply, 0.1f, 10f);
            float multiplier = Mathf.Pow(ratio, EconomyOverhaulPlugin.GlobalDemandWeight.Value);
            multiplier = Mathf.Clamp(multiplier,
                EconomyOverhaulPlugin.GlobalDemandMinMultiplier.Value,
                EconomyOverhaulPlugin.GlobalDemandMaxMultiplier.Value);

            float cacheTime = Mathf.Max(0.25f, EconomyOverhaulPlugin.PriceCacheSeconds.Value);
            globalCache[resource] = new CachedFloat(multiplier, now + cacheTime);
            return multiplier;
        }

        private struct CachedFloat
        {
            public readonly float Value;
            public readonly float ExpiresAt;
            public CachedFloat(float value, float expiresAt) { Value = value; ExpiresAt = expiresAt; }
        }
    }

    // =========================================================================
    // PATCH 3: CanBuy – cap fuel reserves so stations actually sell fuel
    // =========================================================================
    // ROOT CAUSE OF "NO AVAILABLE FUEL":
    // Vanilla CanBuy: available = inventory - (storageMax * stockRatio)
    // When stockRatio is high (e.g. 5.0 from v0.3.0 bug, or vanilla values),
    // minStockQuantity exceeds actual inventory → available = 0 → no fuel sold.
    // Also: resupplyRatePerHour caps trades per hour to a trickle.
    //
    // Fix: For fuel, cap the reserve to FuelReserveMaxRatio of actual inventory
    // and raise the effective resupply rate. This lets stations sell fuel freely.
    // =========================================================================
    [HarmonyPatch(typeof(Market), "CanBuy")]
    internal static class MarketCanBuyPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Market __instance, ResourceDefinition resource, float quantity, int factionCredits, ref float __result)
        {
            if (!EconomyOverhaulPlugin.Enabled.Value || resource == null) return;

            bool isFuel = EconomyOverhaulPlugin.IsFuel(resource);

            // FUEL FIX: Recalculate with capped reserve instead of vanilla's broken one
            if (isFuel && quantity > 0f && factionCredits > 0)
            {
                float quantityOf = __instance.GetQuantityOf(resource);
                if (quantityOf < 0.5f) { __result = 0f; return; }

                // Cap how much fuel a station can hold back
                float reserveMax = EconomyOverhaulPlugin.FuelReserveMaxRatio.Value;
                float reserved = quantityOf * reserveMax;
                float available = Mathf.Max(0f, quantityOf - reserved);

                // Effective resupply rate for fuel
                float effectiveRate = Mathf.Max(
                    __instance.resupplyRatePerHour * 10f,
                    EconomyOverhaulPlugin.MinResupplyRate.Value * EconomyOverhaulPlugin.FuelResupplyMultiplier.Value);

                int price = Mathf.Max(1, __instance.GetCurrentPrice(resource, true));
                float affordable = Mathf.Floor((float)factionCredits / (float)price);

                float result = Mathf.Floor(Mathf.Min(quantity, available, affordable, effectiveRate));
                // Only override if our result is better than vanilla's
                __result = Mathf.Max(__result, result);
            }

            // Enforce MinimumTradeQuantity
            float minTrade = EconomyOverhaulPlugin.MinimumTradeQuantity.Value;
            if (minTrade > 0f && __result > 0f && __result < minTrade)
                __result = 0f;
        }
    }

    // =========================================================================
    // PATCH 4: Stock Ratio Recovery on Factory.ProductionCycle
    // =========================================================================
    // Factory.AddResourcesDelayed drives stockRatios for outputs to 0 on init.
    // Once at 0, they never recover, which breaks GetMinStockQuantity (returns 0)
    // and makes GetCurrentPrice use idealStockRatio instead of real inventory.
    // This gradually recovers stockRatios to a sane value (0.15 by default).
    // For fuel specifically, we also cap stockRatios to prevent over-reservation.
    // =========================================================================
    [HarmonyPatch(typeof(Factory), "ProductionCycle")]
    internal static class StockRatioRecoveryPatch
    {
        private static readonly AccessTools.FieldRef<Market, List<ResourceQuantity>> stockRatiosRef =
            AccessTools.FieldRefAccess<Market, List<ResourceQuantity>>("stockRatios");

        [HarmonyPostfix]
        private static void Postfix(Factory __instance)
        {
            if (!EconomyOverhaulPlugin.Enabled.Value || __instance == null) return;

            try
            {
                RecoverStockRatios(__instance);
            }
            catch (Exception ex)
            {
                EconomyOverhaulPlugin.Log.LogWarning("StockRatioRecovery error: " + ex.Message);
            }
        }

        private static void RecoverStockRatios(Factory factory)
        {
            Market market = factory.GetComponent<Market>();
            if (market == null || market.inventory == null) return;

            float storageMax = market.inventory.storageMax;
            if (storageMax <= 0f) return;

            float threshold = EconomyOverhaulPlugin.StockRatioRecoveryThreshold.Value;
            float recoveryRate = EconomyOverhaulPlugin.StockRatioRecoveryRate.Value;
            float targetRatio = Mathf.Min(EconomyOverhaulPlugin.SurplusDumpRatio.Value, 0.5f);
            float fuelReserveCap = EconomyOverhaulPlugin.FuelReserveMaxRatio.Value;

            // Collect output resources from factory recipes
            HashSet<ResourceDefinition> outputResources = new HashSet<ResourceDefinition>();
            List<RecipeCapacity> recipes = factory.recipes;
            if (recipes == null) return;

            for (int i = 0; i < recipes.Count; i++)
            {
                ProductionRecipe recipe = recipes[i].Recipe;
                if (recipe == null) continue;

                List<ResourceQuantity> outputs = recipe.Outputs;
                if (outputs == null) continue;

                for (int j = 0; j < outputs.Count; j++)
                {
                    if (outputs[j].resource != null)
                        outputResources.Add(outputs[j].resource);
                }
            }

            if (outputResources.Count == 0) return;

            List<ResourceQuantity> stockRatios = stockRatiosRef(market);
            if (stockRatios == null) return;

            for (int i = 0; i < stockRatios.Count; i++)
            {
                ResourceQuantity rq = stockRatios[i];
                if (rq.resource == null || !outputResources.Contains(rq.resource)) continue;

                float currentStockRatio = rq.quantity * 0.01f;
                bool isFuel = EconomyOverhaulPlugin.IsFuel(rq.resource);

                // For fuel: cap stockRatio to prevent stations hoarding fuel
                if (isFuel && currentStockRatio > fuelReserveCap)
                {
                    rq.quantity = fuelReserveCap * 100f;
                    continue;
                }

                // For non-fuel outputs with 0 stockRatio: recover gradually
                if (currentStockRatio >= targetRatio) continue;

                float qty = market.inventory.GetQuantityOf(rq.resource);
                float fillRatio = qty / storageMax;

                if (fillRatio > threshold)
                {
                    float effectiveTarget = isFuel ? Mathf.Min(targetRatio, fuelReserveCap) : targetRatio;
                    float newStockRatio = Mathf.Min(currentStockRatio + recoveryRate, effectiveTarget);
                    rq.quantity = newStockRatio * 100f;
                }
            }
        }
    }

    // =========================================================================
    // PATCH 5: GetBestSellerFromList – relax price ceiling when no sellers found
    // =========================================================================
    [HarmonyPatch(typeof(GlobalMarket), "GetBestSellerFromList")]
    internal static class GlobalMarketSellerPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref Market __result, ResourceDefinition resource, List<Market> marketsToCheck)
        {
            if (!EconomyOverhaulPlugin.Enabled.Value || __result != null) return;

            // Original returned null — no seller found below average price.
            // Retry without the price ceiling to break deadlocks.
            int bestPrice = int.MaxValue;
            List<Market> candidates = new List<Market>();

            for (int i = 0; i < marketsToCheck.Count; i++)
            {
                Market m = marketsToCheck[i];
                if (m.CanBuy(resource, 1f, 1000000) <= 0f) continue;
                if (!m.HasResource(resource)) continue;

                int price = m.GetCurrentPrice(resource, true);
                if (price < bestPrice)
                {
                    bestPrice = price;
                    candidates.Clear();
                    candidates.Add(m);
                }
                else if (price == bestPrice)
                {
                    candidates.Add(m);
                }
            }

            if (candidates.Count > 0)
            {
                __result = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }
        }
    }

    // =========================================================================
    // PATCH 6: Trader profit margin floor
    // =========================================================================
    [HarmonyPatch]
    internal static class TraderMarginPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo tradeCycle = AccessTools.Method(typeof(Trader), "TradeCycle");
            if (tradeCycle != null) yield return tradeCycle;

            MethodInfo trade = AccessTools.Method(typeof(Trader), "Trade");
            if (trade != null) yield return trade;
        }

        [HarmonyPostfix]
        private static void Postfix(Trader __instance)
        {
            if (!EconomyOverhaulPlugin.Enabled.Value || __instance == null) return;

            float minimum = Mathf.Max(0f, EconomyOverhaulPlugin.MinimumAutoTraderProfitMargin.Value);
            if (__instance.currentMinProfitMargin >= 0f && __instance.currentMinProfitMargin < minimum)
            {
                __instance.currentMinProfitMargin = minimum;
            }
        }
    }
}
