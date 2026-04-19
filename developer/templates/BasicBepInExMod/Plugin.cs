using BepInEx;
using HarmonyLib;

namespace BasicMod
{
    [BepInPlugin("local.spacefleet.basic-mod", "Basic Spacefleet Mod", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;

        private void Awake()
        {
            Logger.LogInfo("Basic mod loaded.");
            harmony = new Harmony("local.spacefleet.basic-mod");
            harmony.PatchAll();
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}
