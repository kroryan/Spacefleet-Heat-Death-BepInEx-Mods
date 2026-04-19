using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SpacefleetEconomyOverhaul
{
    [BepInPlugin("local.spacefleet.economy-overhaul", "Spacefleet Economy Overhaul", "0.1.0")]
    public sealed class EconomyOverhaulPlugin : BaseUnityPlugin
    {
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

        private Harmony harmony;

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true, "Enable economy price balancing.");
            MinPriceRatio = Config.Bind("Prices", "MinPriceRatio", 0.35f, "Lowest allowed price as a fraction of resource base price.");
            MaxPriceRatio = Config.Bind("Prices", "MaxPriceRatio", 4.0f, "Highest allowed price as a multiple of resource base price.");
            ScarcityPremium = Config.Bind("Prices", "ScarcityPremium", 0.35f, "Extra price pressure when a market is below ideal stock.");
            AbundanceDiscount = Config.Bind("Prices", "AbundanceDiscount", 0.20f, "Discount pressure when a market is above ideal stock.");
            GlobalDemandWeight = Config.Bind("GlobalMarket", "GlobalDemandWeight", 0.25f, "How strongly global demand/supply affects local prices.");
            GlobalDemandMinMultiplier = Config.Bind("GlobalMarket", "GlobalDemandMinMultiplier", 0.80f, "Minimum global demand multiplier.");
            GlobalDemandMaxMultiplier = Config.Bind("GlobalMarket", "GlobalDemandMaxMultiplier", 1.25f, "Maximum global demand multiplier.");
            MinimumTradeQuantity = Config.Bind("Trade", "MinimumTradeQuantity", 1.0f, "Reject smaller market trades to avoid dust trades and trader accounting edge cases.");
            MinimumAutoTraderProfitMargin = Config.Bind("Trade", "MinimumAutoTraderProfitMargin", 0.05f, "Lowest profit margin auto-traders are allowed to relax to.");
            PriceCacheSeconds = Config.Bind("Performance", "PriceCacheSeconds", 2.0f, "Seconds to cache expensive economy multipliers.");

            harmony = new Harmony("local.spacefleet.economy-overhaul");
            harmony.PatchAll();
            Logger.LogInfo("Spacefleet Economy Overhaul loaded. Price balancing is cached and configurable in BepInEx/config/local.spacefleet.economy-overhaul.cfg.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    [HarmonyPatch]
    internal static class MarketGetCurrentPricePatch
    {
        private static readonly Dictionary<CacheKey, CachedFloat> localMultiplierCache = new Dictionary<CacheKey, CachedFloat>();
        private static readonly Dictionary<int, CachedFloat> globalMultiplierCache = new Dictionary<int, CachedFloat>();
        private static readonly Dictionary<string, FieldInfo> fieldCache = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<string, MethodInfo> methodCache = new Dictionary<string, MethodInfo>();

        private static Type globalMarketType;
        private static FieldInfo globalCurrentField;
        private static PropertyInfo globalCurrentProperty;
        private static object cachedGlobalMarket;
        private static float nextGlobalMarketLookupTime;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("Market"), "GetCurrentPrice");
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance, object[] __args, ref int __result)
        {
            if (!EconomyOverhaulPlugin.Enabled.Value || __instance == null || __args == null || __args.Length < 1)
            {
                return;
            }

            object resource = __args[0];
            if (resource == null || __result <= 0)
            {
                return;
            }

            int basePrice = GetIntField(resource, "basePrice", 0);
            if (basePrice <= 0)
            {
                return;
            }

            float price = __result;
            price *= GetLocalStockMultiplier(__instance, resource);
            price *= GetGlobalDemandMultiplier(resource);

            float min = Mathf.Max(1f, basePrice * EconomyOverhaulPlugin.MinPriceRatio.Value);
            float max = Mathf.Max(min, basePrice * EconomyOverhaulPlugin.MaxPriceRatio.Value);
            __result = Mathf.RoundToInt(Mathf.Clamp(price, min, max));
        }

        private static float GetLocalStockMultiplier(object market, object resource)
        {
            CacheKey key = new CacheKey(market, resource);
            float now = Time.unscaledTime;
            if (localMultiplierCache.TryGetValue(key, out CachedFloat cached) && cached.ExpiresAt > now)
            {
                return cached.Value;
            }

            float idealStockRatio = Mathf.Max(0.01f, GetFloatField(market, "idealStockRatio", 0.5f));
            float stockRatio = InvokeFloat(market, "GetStockRatio", idealStockRatio, resource);
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

            multiplier = Mathf.Clamp(multiplier, 0.5f, 1.75f);
            localMultiplierCache[key] = new CachedFloat(multiplier, now + GetCacheSeconds());
            return multiplier;
        }

        private static float GetGlobalDemandMultiplier(object resource)
        {
            int resourceId = RuntimeHelpers.GetHashCode(resource);
            float now = Time.unscaledTime;
            if (globalMultiplierCache.TryGetValue(resourceId, out CachedFloat cached) && cached.ExpiresAt > now)
            {
                return cached.Value;
            }

            object globalMarket = FindCurrent("GlobalMarket");
            if (globalMarket == null)
            {
                return 1f;
            }

            float supply = Mathf.Max(0.01f, InvokeFloat(globalMarket, "GetSupplyOf", 1f, resource));
            float demand = Mathf.Max(0.01f, InvokeFloat(globalMarket, "GetDemandOf", 1f, resource));
            float ratio = Mathf.Clamp(demand / supply, 0.1f, 10f);
            float multiplier = Mathf.Pow(ratio, EconomyOverhaulPlugin.GlobalDemandWeight.Value);
            multiplier = Mathf.Clamp(multiplier, EconomyOverhaulPlugin.GlobalDemandMinMultiplier.Value, EconomyOverhaulPlugin.GlobalDemandMaxMultiplier.Value);
            globalMultiplierCache[resourceId] = new CachedFloat(multiplier, now + GetCacheSeconds());
            return multiplier;
        }

        private static object FindCurrent(string typeName)
        {
            float now = Time.unscaledTime;
            if (cachedGlobalMarket != null && nextGlobalMarketLookupTime > now)
            {
                return cachedGlobalMarket;
            }

            if (globalMarketType == null)
            {
                globalMarketType = AccessTools.TypeByName(typeName);
                if (globalMarketType == null)
                {
                    return null;
                }

                globalCurrentField = globalMarketType.GetField("current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                globalCurrentProperty = globalMarketType.GetProperty("current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            }

            object current = globalCurrentField?.GetValue(null);
            if (current != null)
            {
                cachedGlobalMarket = current;
                nextGlobalMarketLookupTime = now + GetCacheSeconds();
                return current;
            }

            current = globalCurrentProperty?.GetValue(null, null);
            cachedGlobalMarket = current ?? UnityEngine.Object.FindObjectOfType(globalMarketType);
            nextGlobalMarketLookupTime = now + GetCacheSeconds();
            return cachedGlobalMarket;
        }

        private static int GetIntField(object target, string fieldName, int fallback)
        {
            object value = GetField(target, fieldName);
            return value is int intValue ? intValue : fallback;
        }

        private static float GetFloatField(object target, string fieldName, float fallback)
        {
            object value = GetField(target, fieldName);
            return value is float floatValue ? floatValue : fallback;
        }

        private static object GetField(object target, string fieldName)
        {
            if (target == null)
            {
                return null;
            }

            Type type = target.GetType();
            string key = type.FullName + "." + fieldName;
            if (!fieldCache.TryGetValue(key, out FieldInfo field))
            {
                field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                fieldCache[key] = field;
            }

            return field?.GetValue(target);
        }

        private static float InvokeFloat(object target, string methodName, float fallback, params object[] args)
        {
            if (target == null)
            {
                return fallback;
            }

            try
            {
                Type type = target.GetType();
                string key = type.FullName + "." + methodName;
                if (!methodCache.TryGetValue(key, out MethodInfo method))
                {
                    method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    methodCache[key] = method;
                }

                object value = method?.Invoke(target, args);
                return value is float floatValue ? floatValue : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static float GetCacheSeconds()
        {
            return Mathf.Max(0.25f, EconomyOverhaulPlugin.PriceCacheSeconds.Value);
        }

        private struct CacheKey : IEquatable<CacheKey>
        {
            private readonly int marketId;
            private readonly int resourceId;

            public CacheKey(object market, object resource)
            {
                marketId = RuntimeHelpers.GetHashCode(market);
                resourceId = RuntimeHelpers.GetHashCode(resource);
            }

            public bool Equals(CacheKey other)
            {
                return marketId == other.marketId && resourceId == other.resourceId;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (marketId * 397) ^ resourceId;
                }
            }
        }

        private struct CachedFloat
        {
            public readonly float Value;
            public readonly float ExpiresAt;

            public CachedFloat(float value, float expiresAt)
            {
                Value = value;
                ExpiresAt = expiresAt;
            }
        }
    }

    [HarmonyPatch]
    internal static class MarketCanBuyPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("Market"), "CanBuy");
        }

        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            if (!EconomyOverhaulPlugin.Enabled.Value || __result <= 0f)
            {
                return;
            }

            float minimum = Mathf.Max(0f, EconomyOverhaulPlugin.MinimumTradeQuantity.Value);
            if (minimum > 0f && __result < minimum)
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch]
    internal static class TraderMarginPatch
    {
        private static readonly Dictionary<string, FieldInfo> fieldCache = new Dictionary<string, FieldInfo>();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type traderType = AccessTools.TypeByName("Trader");
            if (traderType == null)
            {
                yield break;
            }

            MethodInfo tradeCycle = AccessTools.Method(traderType, "TradeCycle");
            if (tradeCycle != null)
            {
                yield return tradeCycle;
            }

            MethodInfo trade = AccessTools.Method(traderType, "Trade");
            if (trade != null)
            {
                yield return trade;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance)
        {
            if (!EconomyOverhaulPlugin.Enabled.Value || __instance == null)
            {
                return;
            }

            float minimum = Mathf.Max(0f, EconomyOverhaulPlugin.MinimumAutoTraderProfitMargin.Value);
            float current = GetFloatField(__instance, "currentMinProfitMargin", minimum);
            if (current >= 0f && current < minimum)
            {
                SetField(__instance, "currentMinProfitMargin", minimum);
            }
        }

        private static float GetFloatField(object target, string fieldName, float fallback)
        {
            object value = GetField(target, fieldName);
            return value is float floatValue ? floatValue : fallback;
        }

        private static object GetField(object target, string fieldName)
        {
            if (target == null)
            {
                return null;
            }

            FieldInfo field = GetFieldInfo(target.GetType(), fieldName);
            return field?.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }

            FieldInfo field = GetFieldInfo(target.GetType(), fieldName);
            field?.SetValue(target, value);
        }

        private static FieldInfo GetFieldInfo(Type type, string fieldName)
        {
            string key = type.FullName + "." + fieldName;
            if (!fieldCache.TryGetValue(key, out FieldInfo field))
            {
                field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                fieldCache[key] = field;
            }

            return field;
        }
    }
}
