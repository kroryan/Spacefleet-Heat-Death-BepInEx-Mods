using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SpacefleetCoreFixes
{
    [BepInPlugin("local.spacefleet.core-fixes", "Spacefleet Core Fixes", "0.1.0")]
    public sealed class CoreFixesPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> EmergencyFleetRecoveryEnabled;
        internal static ConfigEntry<float> EmergencyFleetFuelRatio;
        internal static ConfigEntry<float> EmergencyFleetMinimumModuleHealth;
        internal static ConfigEntry<float> EmergencyFleetCooldownSeconds;

        private Harmony harmony;

        private void Awake()
        {
            EmergencyFleetRecoveryEnabled = Config.Bind("FleetRecovery", "Enabled", true, "Automatically recover player fleets softlocked at zero delta-v.");
            EmergencyFleetFuelRatio = Config.Bind("FleetRecovery", "EmergencyFuelRatio", 0.05f, "Fuel ratio granted to player fleets when zero delta-v would softlock them.");
            EmergencyFleetMinimumModuleHealth = Config.Bind("FleetRecovery", "MinimumFuelDriveHealth", 0.25f, "Minimum health ratio applied to damaged fuel/drive modules during emergency recovery.");
            EmergencyFleetCooldownSeconds = Config.Bind("FleetRecovery", "CooldownSeconds", 60f, "Minimum seconds between automatic recovery attempts for the same fleet.");

            Logger.LogInfo("Spacefleet Core Fixes loaded.");
            harmony = new Harmony("local.spacefleet.core-fixes");
            harmony.PatchAll();
            Logger.LogInfo("Money sound limiter active: max 2 plays per 30 seconds.");
            Logger.LogInfo("Emergency fleet recovery active for player fleets stranded at zero delta-v.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    [HarmonyPatch]
    internal static class MoneySoundLimiterPatch
    {
        private const int MoneySoundsPerWindow = 2;
        private const float MoneySoundWindowSeconds = 30f;

        private static float moneySoundWindowStart = -MoneySoundWindowSeconds;
        private static int moneySoundsThisWindow;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("AudioManager"), "PlayMoney");
        }

        [HarmonyPrefix]
        private static bool LimitMoneySound()
        {
            float now = Time.unscaledTime;

            if (now - moneySoundWindowStart >= MoneySoundWindowSeconds)
            {
                moneySoundWindowStart = now;
                moneySoundsThisWindow = 0;
            }

            if (moneySoundsThisWindow >= MoneySoundsPerWindow)
            {
                return false;
            }

            moneySoundsThisWindow++;
            return true;
        }
    }

    [HarmonyPatch]
    internal static class EmergencyFleetRecoveryPatch
    {
        private static readonly Dictionary<int, float> nextRecoveryTimes = new Dictionary<int, float>();
        private static readonly Dictionary<string, FieldInfo> fieldCache = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<string, MethodInfo> methodCache = new Dictionary<string, MethodInfo>();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("FleetManager"), "UpdateDv");
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance)
        {
            if (!CoreFixesPlugin.EmergencyFleetRecoveryEnabled.Value || __instance == null || !IsPlayerFleet(__instance))
            {
                return;
            }

            float fleetDv = GetFloat(__instance, "fleetDv", 1f);
            float fleetDvMax = GetFloat(__instance, "fleetDvMax", 0f);
            if (fleetDvMax <= 0f || fleetDv > Mathf.Max(1f, fleetDvMax * 0.001f))
            {
                return;
            }

            int fleetId = RuntimeHelpers.GetHashCode(__instance);
            float now = Time.unscaledTime;
            if (nextRecoveryTimes.TryGetValue(fleetId, out float nextAllowed) && nextAllowed > now)
            {
                return;
            }

            nextRecoveryTimes[fleetId] = now + Mathf.Max(5f, CoreFixesPlugin.EmergencyFleetCooldownSeconds.Value);

            bool changed = RecoverFleet(__instance);
            if (changed)
            {
                InvokeNoArgs(__instance, "UpdateDv");
                object navigation = GetField(__instance, "navigation");
                SetField(navigation, "isInsufficientDv", false);
                Debug.Log("[Spacefleet Core Fixes] Emergency fleet recovery applied to a player fleet stranded at zero delta-v.");
            }
        }

        private static bool RecoverFleet(object fleet)
        {
            bool changed = false;
            IEnumerable ships = GetField(fleet, "ships") as IEnumerable;
            if (ships == null)
            {
                return false;
            }

            foreach (object ship in ships)
            {
                changed |= RepairModule(GetField(ship, "fuel"));
                changed |= RepairModule(GetField(ship, "drive"));

                float missing = InvokeFloat(ship, "GetMissingFuel", 0f);
                float currentRatio = InvokeFloat(ship, "GetFuelRatio", 0f);
                float targetRatio = Mathf.Clamp01(CoreFixesPlugin.EmergencyFleetFuelRatio.Value);
                if (missing > 0f && currentRatio < targetRatio)
                {
                    InvokeOneFloat(ship, "AddFuel", missing * Mathf.Clamp01(targetRatio - currentRatio));
                    changed = true;
                }
            }

            return changed;
        }

        private static bool RepairModule(object module)
        {
            if (module == null)
            {
                return false;
            }

            object healthValue = GetField(module, "health");
            if (!(healthValue is float health))
            {
                return false;
            }

            float minimum = Mathf.Clamp01(CoreFixesPlugin.EmergencyFleetMinimumModuleHealth.Value);
            if (health >= minimum)
            {
                return false;
            }

            SetField(module, "health", minimum);
            return true;
        }

        private static bool IsPlayerFleet(object fleet)
        {
            object track = GetField(fleet, "track");
            object factionId = GetField(track, "factionID");
            return factionId is int id && id == 1;
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

        private static float GetFloat(object target, string fieldName, float fallback)
        {
            object value = GetField(target, fieldName);
            return value is float f ? f : fallback;
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

        private static float InvokeFloat(object target, string methodName, float fallback)
        {
            MethodInfo method = GetMethodInfo(target, methodName, Type.EmptyTypes);
            object value = method?.Invoke(target, null);
            return value is float f ? f : fallback;
        }

        private static void InvokeOneFloat(object target, string methodName, float value)
        {
            MethodInfo method = GetMethodInfo(target, methodName, new[] { typeof(float) });
            method?.Invoke(target, new object[] { value });
        }

        private static void InvokeNoArgs(object target, string methodName)
        {
            MethodInfo method = GetMethodInfo(target, methodName, Type.EmptyTypes);
            method?.Invoke(target, null);
        }

        private static MethodInfo GetMethodInfo(object target, string methodName, Type[] parameterTypes)
        {
            if (target == null)
            {
                return null;
            }

            Type type = target.GetType();
            string key = type.FullName + "." + methodName + "." + parameterTypes.Length;
            if (!methodCache.TryGetValue(key, out MethodInfo method))
            {
                method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null);
                methodCache[key] = method;
            }

            return method;
        }
    }
}
