using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace SpacefleetBattleDebug
{
    [BepInPlugin("local.spacefleet.battle-debug", "Spacefleet Battle Debug", "0.2.0")]
    public sealed class BattleDebugPlugin : BaseUnityPlugin
    {
        #region Constants

        private const float RefreshInterval = 0.5f;
        private const int MaxFeed = 100;
        private const int MaxShipsPerSide = 20;
        private const int MaxShipsPerFleet = 10;
        private const int InfoWinId = 42013;
        private const int FeedWinId = 42014;

        private const string COL_OK = "#88FF88";
        private const string COL_WARN = "#FFCC44";
        private const string COL_CRIT = "#FF6644";
        private const string COL_DEAD = "#FF4444";
        private const string COL_DISINT = "#FF0000";
        private const string COL_INFO = "#AADDFF";
        private const string COL_DIM = "#999999";
        private const string COL_HEAD = "#FFFFFF";

        #endregion

        #region Static fields (Harmony access)

        private static BattleDebugPlugin instance;
        private static ConfigEntry<KeyCode> toggleKey;
        private static ConfigEntry<string> toggleChar;
        private static readonly List<FeedEntry> feed = new List<FeedEntry>();
        private static bool harmonyActive;

        private static Type tModuleType;
        private static Type shipControllerType;

        private static FieldInfo fModuleHealthRatio;
        private static FieldInfo fModuleIsDead;
        private static FieldInfo fModulePartType;
        private static FieldInfo fModuleProductName;
        private static FieldInfo fModuleArmorHealth;
        private static FieldInfo fModuleArmorMax;
        private static FieldInfo fScTrack;
        private static FieldInfo fTrackPublicName;
        private static FieldInfo fTrackFactionId;

        #endregion

        #region Instance fields

        private Harmony harmony;
        private readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();
        private readonly Dictionary<string, FieldInfo> fieldCache = new Dictionary<string, FieldInfo>();
        private readonly Dictionary<string, PropertyInfo> propCache = new Dictionary<string, PropertyInfo>();
        private readonly Dictionary<string, MethodInfo> methodCache = new Dictionary<string, MethodInfo>();

        private Rect infoRect = new Rect(40f, 40f, 720f, 620f);
        private Rect feedRect = new Rect(780f, 40f, 400f, 620f);
        private Vector2 infoScroll;
        private Vector2 feedScroll;
        private bool showInfo;
        private bool showFeed;
        private bool showWeapons;
        private bool showHeat;
        private bool isInfoMinimized;
        private bool isFeedMinimized;
        private float infoNormalHeight;
        private float feedNormalHeight;
        private bool isResizingInfo;
        private bool isResizingFeed;
        private Vector2 infoResizeOffset;
        private Vector2 feedResizeOffset;
        private GUIStyle richLabel;
        private GUIStyle sectionBox;
        private bool stylesReady;

        private static readonly Vector2 InfoMinSize = new Vector2(400f, 200f);
        private static readonly Vector2 FeedMinSize = new Vector2(300f, 200f);
        private static bool skinChecked;
        private static GUISkin sharedSkin;
        private static Texture2D bgTexture;

        private float nextRefresh;
        private bool isTactical;
        private bool wasTactical;
        private string modeLabel = "";
        private string overviewText = "";
        private readonly List<ShipData> playerShips = new List<ShipData>();
        private readonly List<ShipData> hostileShips = new List<ShipData>();
        private readonly List<FleetData> playerFleets = new List<FleetData>();
        private readonly List<FleetData> otherFleets = new List<FleetData>();

        private readonly Dictionary<int, string> lastTarget = new Dictionary<int, string>();

        #endregion

        #region Data structures

        private struct FeedEntry { public string text; }

        private struct ShipData
        {
            public string name, classType, status, targetName;
            public float healthRatio, armorThickness;
            public bool isDead;
            public int dcFree, dcTotal, materials, materialsMax, totalModules;
            public List<ModuleData> damaged;
            public List<string> destroyed;
            public List<WeaponData> weapons;
            public HeatData heat;
            public bool hasHeat;
            public int id;
        }

        private struct ModuleData
        {
            public string name, partType;
            public float healthRatio, armorRatio;
        }

        private struct WeaponData
        {
            public string name, type, status, mode;
            public int range, engaging, totalTurrets;
        }

        private struct HeatData
        {
            public float percent, netFlow, timeToOverflow;
            public bool radiatorsOpen;
        }

        private struct FleetData
        {
            public string name, side, state, navState, navTarget, flightType;
            public float dv, dvMax, navDist;
            public int factionId;
            public List<FleetShipData> ships;
        }

        private struct FleetShipData
        {
            public string name;
            public float healthRatio, fuelRatio, dv, dvMax;
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            instance = this;
            toggleKey = Config.Bind("Hotkeys", "ToggleKey", KeyCode.Plus, "Key to toggle battle debug.");
            toggleChar = Config.Bind("Hotkeys", "ToggleCharacter", "+", "Character to toggle battle debug.");
            SetupHarmonyPatches();
            Logger.LogInfo("Spacefleet Battle Debug loaded. Press " + GetHotkeySummary() + " to open.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        private void Update()
        {
            if (toggleKey.Value != KeyCode.None && Input.GetKeyDown(toggleKey.Value))
                Toggle();
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && MatchesChar(e))
            {
                Toggle();
                e.Use();
            }

            if (!showInfo && !showFeed) return;

            if (isResizingInfo)
            {
                if (e.type == EventType.MouseDrag)
                {
                    infoRect.width = Mathf.Max(InfoMinSize.x, e.mousePosition.x - infoRect.x + infoResizeOffset.x);
                    infoRect.height = Mathf.Max(InfoMinSize.y, e.mousePosition.y - infoRect.y + infoResizeOffset.y);
                }
                if (e.rawType == EventType.MouseUp) isResizingInfo = false;
            }

            if (isResizingFeed)
            {
                if (e.type == EventType.MouseDrag)
                {
                    feedRect.width = Mathf.Max(FeedMinSize.x, e.mousePosition.x - feedRect.x + feedResizeOffset.x);
                    feedRect.height = Mathf.Max(FeedMinSize.y, e.mousePosition.y - feedRect.y + feedResizeOffset.y);
                }
                if (e.rawType == EventType.MouseUp) isResizingFeed = false;
            }

            InitStyles();
            GUI.depth = -1003;

            GUISkin skin = TryGetSharedSkin();
            GUISkin prev = GUI.skin;
            if (skin != null) GUI.skin = skin;

            if (Time.unscaledTime >= nextRefresh) RefreshData();

            if (showInfo)
                infoRect = GUI.Window(InfoWinId, infoRect, DrawInfoWindow, "");
            if (showFeed && isTactical)
                feedRect = GUI.Window(FeedWinId, feedRect, DrawFeedWindow, "");

            GUI.skin = prev;
        }

        private static GUISkin TryGetSharedSkin()
        {
            if (!skinChecked)
            {
                skinChecked = true;
                try
                {
                    Type t = null;
                    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        t = asm.GetType("SpacefleetModMenu.ModMenuPlugin");
                        if (t != null) break;
                    }
                    if (t != null)
                    {
                        MethodInfo m = t.GetMethod("GetSharedSkin", BindingFlags.Public | BindingFlags.Static);
                        sharedSkin = m?.Invoke(null, null) as GUISkin;
                        MethodInfo bgm = t.GetMethod("GetBgTexture", BindingFlags.Public | BindingFlags.Static);
                        bgTexture = bgm?.Invoke(null, null) as Texture2D;
                    }
                }
                catch { }
                if (bgTexture == null)
                {
                    bgTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    var c = new Color(0.05f, 0.06f, 0.08f, 0.95f);
                    bgTexture.SetPixels(new[] { c, c, c, c });
                    bgTexture.Apply();
                    UnityEngine.Object.DontDestroyOnLoad(bgTexture);
                }
            }
            return sharedSkin;
        }

        private void Toggle()
        {
            if (showInfo || showFeed)
            {
                showInfo = false;
                showFeed = false;
            }
            else
            {
                showInfo = true;
                showFeed = true;
                nextRefresh = 0f;
            }
        }

        #endregion

        #region Harmony Setup

        private void SetupHarmonyPatches()
        {
            try
            {
                harmony = new Harmony("local.spacefleet.battle-debug");

                tModuleType = FindType("T_Module");
                shipControllerType = FindType("ShipController");

                if (tModuleType != null)
                {
                    CacheModuleFields(tModuleType);
                    PatchMethod(tModuleType, "TryDamageKinetic", nameof(DmgPrefix), nameof(DmgKineticPostfix));
                    PatchMethod(tModuleType, "TryDamagePhotonic", nameof(DmgPrefix), nameof(DmgPhotonicPostfix));
                    PatchMethod(tModuleType, "SetIsDead", nameof(ModuleDeathPrefix), nameof(ModuleDeathPostfix));
                }

                if (shipControllerType != null)
                {
                    CacheShipFields(shipControllerType);
                    PatchMethod(shipControllerType, "SoftDeath", null, nameof(OnSoftDeath));
                    PatchMethod(shipControllerType, "HardDeath", null, nameof(OnHardDeath));
                }

                harmonyActive = true;
                Logger.LogInfo("Harmony damage-tracking patches applied.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Harmony patches failed: " + ex.Message);
                harmonyActive = false;
            }
        }

        private void CacheModuleFields(Type t)
        {
            var f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            fModuleHealthRatio = t.GetField("healthRatio", f);
            fModuleIsDead = t.GetField("isDead", f);
            fModulePartType = t.GetField("partType", f);
            fModuleProductName = t.GetField("productName", f) ?? t.GetField("prefabName", f);
            fModuleArmorHealth = t.GetField("armorHealth", f);
            fModuleArmorMax = t.GetField("armorHealthMax", f);
        }

        private void CacheShipFields(Type t)
        {
            var f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            fScTrack = t.GetField("track", f);
            Type trackType = FindType("Track");
            if (trackType != null)
            {
                fTrackPublicName = trackType.GetField("publicName", f) ?? trackType.GetField("trackName", f);
                fTrackFactionId = trackType.GetField("factionID", f);
            }
        }

        private void PatchMethod(Type targetType, string methodName, string prefixName, string postfixName)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            MethodInfo original = targetType.GetMethod(methodName, flags);
            if (original == null)
            {
                Logger.LogWarning("Patch target not found: " + targetType.Name + "." + methodName);
                return;
            }
            var myType = typeof(BattleDebugPlugin);
            var bf = BindingFlags.Static | BindingFlags.NonPublic;
            HarmonyMethod pre = prefixName != null ? new HarmonyMethod(myType.GetMethod(prefixName, bf)) : null;
            HarmonyMethod post = postfixName != null ? new HarmonyMethod(myType.GetMethod(postfixName, bf)) : null;
            harmony.Patch(original, pre, post);
        }

        #endregion

        #region Harmony Patch Methods

        private static void DmgPrefix(object __instance, ref object __state)
        {
            float hr = fModuleHealthRatio != null ? SafeFloat(fModuleHealthRatio.GetValue(__instance)) : 1f;
            float ar = fModuleArmorHealth != null ? SafeFloat(fModuleArmorHealth.GetValue(__instance)) : 0f;
            __state = new float[] { hr, ar };
        }

        private static void DmgKineticPostfix(object __instance, object __state)
        {
            HandleDmgPostfix(__instance, __state, "Kinetic");
        }

        private static void DmgPhotonicPostfix(object __instance, object __state)
        {
            HandleDmgPostfix(__instance, __state, "Photonic");
        }

        private static void HandleDmgPostfix(object module, object state, string dmgType)
        {
            if (!(state is float[] prev) || prev.Length < 2) return;
            float oldHr = prev[0];
            float newHr = fModuleHealthRatio != null ? SafeFloat(fModuleHealthRatio.GetValue(module)) : 1f;
            bool nowDead = fModuleIsDead != null && SafeBool(fModuleIsDead.GetValue(module));

            if (nowDead) return;

            float drop = oldHr - newHr;
            if (drop < 0.02f) return;

            string partType = fModulePartType != null ? Convert.ToString(fModulePartType.GetValue(module)) : "?";
            string shipName = GetShipNameFromModule(module);
            string side = GetSideFromModule(module);

            AddFeedColored(side + " \"" + shipName + "\" > " + partType + " -" + PctS(drop)
                + " (now " + PctS(newHr) + ") [" + dmgType + "]",
                drop > 0.25f ? COL_CRIT : COL_WARN);
        }

        private static void ModuleDeathPrefix(object __instance, ref bool __state)
        {
            __state = fModuleIsDead == null || !SafeBool(fModuleIsDead.GetValue(__instance));
        }

        private static void ModuleDeathPostfix(object __instance, bool __state)
        {
            if (!__state) return;
            string partType = fModulePartType != null ? Convert.ToString(fModulePartType.GetValue(__instance)) : "?";
            string productName = fModuleProductName != null ? Convert.ToString(fModuleProductName.GetValue(__instance)) : null;
            string shipName = GetShipNameFromModule(__instance);
            string side = GetSideFromModule(__instance);

            string label = !string.IsNullOrEmpty(productName) && productName != partType
                ? partType + " (" + productName + ")" : partType;

            AddFeedColored(side + " \"" + shipName + "\" > " + label + " DESTROYED", COL_DEAD);
        }

        private static void OnSoftDeath(object __instance)
        {
            string name = GetShipName(__instance);
            string side = GetShipSide(__instance);
            AddFeedColored(side + " \"" + name + "\" DESTROYED", COL_DEAD);
        }

        private static void OnHardDeath(object __instance)
        {
            string name = GetShipName(__instance);
            string side = GetShipSide(__instance);
            AddFeedColored(side + " \"" + name + "\" DISINTEGRATED!", COL_DISINT);
        }

        private static string GetShipNameFromModule(object module)
        {
            try
            {
                Component comp = module as Component;
                if (comp != null && shipControllerType != null)
                {
                    Component sc = comp.GetComponentInParent(shipControllerType);
                    if (sc != null) return GetShipName(sc);
                }
            }
            catch { }
            return "Unknown";
        }

        private static string GetSideFromModule(object module)
        {
            try
            {
                Component comp = module as Component;
                if (comp != null && shipControllerType != null)
                {
                    Component sc = comp.GetComponentInParent(shipControllerType);
                    if (sc != null) return GetShipSide(sc);
                }
            }
            catch { }
            return "";
        }

        private static string GetShipName(object sc)
        {
            try
            {
                if (fScTrack == null) return "ship";
                object track = fScTrack.GetValue(sc);
                if (track != null && fTrackPublicName != null)
                {
                    string n = Convert.ToString(fTrackPublicName.GetValue(track));
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            catch { }
            return "ship";
        }

        private static string GetShipSide(object sc)
        {
            try
            {
                if (fScTrack == null) return "";
                object track = fScTrack.GetValue(sc);
                if (track != null && fTrackFactionId != null)
                {
                    int fid = SafeInt(fTrackFactionId.GetValue(track));
                    return fid == 1 ? "PLAYER" : "ENEMY";
                }
            }
            catch { }
            return "";
        }

        #endregion

        #region Drawing - Info Window

        private void DrawInfoWindow(int id)
        {
            if (bgTexture != null)
                GUI.DrawTexture(new Rect(0, 0, infoRect.width, infoRect.height), bgTexture);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b><size=14>Battle Debug</size></b>  " + C(modeLabel, COL_INFO), richLabel);
            GUILayout.FlexibleSpace();
            if (isTactical)
            {
                showWeapons = GUILayout.Toggle(showWeapons, "Weapons", GUILayout.Width(75f));
                showHeat = GUILayout.Toggle(showHeat, "Heat", GUILayout.Width(50f));
                if (!showFeed && GUILayout.Button("Feed", GUILayout.Width(50f)))
                    showFeed = true;
            }
            if (GUILayout.Button(isInfoMinimized ? "\u25a1" : "\u2014", GUILayout.Width(28f)))
            {
                if (isInfoMinimized) { infoRect.height = infoNormalHeight; isInfoMinimized = false; }
                else { infoNormalHeight = infoRect.height; infoRect.height = 36f; isInfoMinimized = true; }
            }
            if (GUILayout.Button("\u2715", GUILayout.Width(28f)))
                showInfo = false;
            GUILayout.EndHorizontal();

            if (isInfoMinimized)
            {
                GUI.DragWindow();
                return;
            }

            GUILayout.Label(overviewText, richLabel);

            infoScroll = GUILayout.BeginScrollView(infoScroll);

            if (isTactical)
                DrawTacticalContent();
            else
                DrawStrategicContent();

            GUILayout.EndScrollView();

            Rect handle = new Rect(infoRect.width - 18f, infoRect.height - 18f, 18f, 18f);
            GUI.Label(handle, "\u25e2");
            if (Event.current.type == EventType.MouseDown && handle.Contains(Event.current.mousePosition))
            {
                isResizingInfo = true;
                infoResizeOffset = new Vector2(infoRect.width, infoRect.height) - Event.current.mousePosition;
                Event.current.Use();
                return;
            }
            GUI.DragWindow();
        }

        private void DrawTacticalContent()
        {
            DrawShipSection("YOUR SHIPS", playerShips, COL_OK);
            DrawShipSection("ENEMY SHIPS", hostileShips, COL_CRIT);
        }

        private void DrawShipSection(string title, List<ShipData> ships, string titleColor)
        {
            GUILayout.Space(4f);
            GUILayout.Label(C("<b>--- " + title + " (" + ships.Count + ") ---</b>", titleColor), richLabel);

            if (ships.Count == 0)
            {
                GUILayout.Label(C("  No ships", COL_DIM), richLabel);
                return;
            }

            foreach (var ship in ships) DrawShipCard(ship);
        }

        private void DrawShipCard(ShipData s)
        {
            GUILayout.BeginVertical(sectionBox);

            string statusColor = s.isDead ? COL_DEAD
                : s.healthRatio > 0.8f ? COL_OK
                : s.healthRatio > 0.4f ? COL_WARN : COL_CRIT;

            string statusBadge = s.isDead
                ? (s.status == "disintegrated" ? C("DISINTEGRATED", COL_DISINT) : C("DESTROYED", COL_DEAD))
                : C(string.IsNullOrEmpty(s.status) ? "UNKNOWN" : s.status.ToUpperInvariant(), statusColor);

            GUILayout.Label(C("<b>" + s.name + "</b>", COL_HEAD) + " (" + s.classType + ")  " + statusBadge, richLabel);

            GUILayout.Label("  " + ColoredBar(s.healthRatio) + " " + C(Pct(s.healthRatio), statusColor)
                + "  Armor: " + Fmt(s.armorThickness) + "mm", richLabel);

            GUILayout.Label("  Target: " + C(s.targetName, COL_INFO)
                + "  |  DC: " + s.dcFree + "/" + s.dcTotal
                + "  |  Materials: " + s.materials + "/" + s.materialsMax, richLabel);

            if (s.damaged != null && s.damaged.Count > 0)
            {
                string parts = "";
                foreach (var m in s.damaged)
                {
                    string mc = m.healthRatio > 0.5f ? COL_WARN : COL_CRIT;
                    if (parts.Length > 0) parts += "  ";
                    parts += C(m.partType + " " + Pct(m.healthRatio), mc);
                }
                GUILayout.Label("  Damaged: " + parts, richLabel);
            }

            if (s.destroyed != null && s.destroyed.Count > 0)
                GUILayout.Label("  " + C("Destroyed: " + string.Join(", ", s.destroyed), COL_DEAD), richLabel);

            if ((s.damaged == null || s.damaged.Count == 0) && (s.destroyed == null || s.destroyed.Count == 0) && !s.isDead)
                GUILayout.Label(C("  All modules intact", COL_DIM), richLabel);

            if (showWeapons && s.weapons != null)
            {
                foreach (var w in s.weapons)
                {
                    string wc = w.status == "DESTROYED" ? COL_DEAD : w.status == "CEASE FIRE" ? COL_DIM : COL_OK;
                    GUILayout.Label("  " + C("[W]", COL_DIM) + " " + w.name + " [" + w.type + "] "
                        + C(w.status, wc) + " " + w.mode + " rng:" + w.range
                        + " fire:" + w.engaging + "/" + w.totalTurrets, richLabel);
                }
            }

            if (showHeat && s.hasHeat)
            {
                string hc = s.heat.percent > 0.8f ? COL_CRIT : s.heat.percent > 0.5f ? COL_WARN : COL_OK;
                string overflow = s.heat.timeToOverflow > 0f && s.heat.timeToOverflow < 30f
                    ? C(" OVERFLOW " + Fmt(s.heat.timeToOverflow) + "s!", COL_DISINT) : "";
                GUILayout.Label("  " + C("[H]", COL_DIM) + " Heat: " + C(Pct(s.heat.percent), hc)
                    + " net:" + Fmt(s.heat.netFlow) + "MW rad:"
                    + (s.heat.radiatorsOpen ? C("OPEN", COL_OK) : C("CLOSED", COL_WARN))
                    + overflow, richLabel);
            }

            GUILayout.EndVertical();
        }

        private void DrawStrategicContent()
        {
            DrawFleetSection("YOUR FLEETS", playerFleets, COL_OK);
            DrawFleetSection("OTHER FLEETS", otherFleets, COL_DIM);
        }

        private void DrawFleetSection(string title, List<FleetData> fleets, string titleColor)
        {
            GUILayout.Space(4f);
            GUILayout.Label(C("<b>=== " + title + " (" + fleets.Count + ") ===</b>", titleColor), richLabel);

            if (fleets.Count == 0)
            {
                GUILayout.Label(C("  None", COL_DIM), richLabel);
                return;
            }

            foreach (var f in fleets) DrawFleetCard(f);
        }

        private void DrawFleetCard(FleetData f)
        {
            GUILayout.BeginVertical(sectionBox);

            string stateColor = f.state == "ATTACKING" ? COL_CRIT
                : f.state == "RETREATING" ? COL_WARN : COL_INFO;

            GUILayout.Label(C("<b>" + f.side + " \"" + f.name + "\"</b>", COL_HEAD)
                + "  " + C(f.state, stateColor), richLabel);

            float dvRatio = f.dvMax > 0f ? f.dv / f.dvMax : 0f;
            GUILayout.Label("  DV: " + ColoredBar(dvRatio) + " " + Pct(dvRatio)
                + " (" + Fmt(f.dv) + "/" + Fmt(f.dvMax) + ")", richLabel);

            string navInfo = f.navState;
            if (!string.IsNullOrEmpty(f.flightType) && f.flightType != "unknown")
                navInfo += " (" + f.flightType + ")";
            navInfo += " > " + C(f.navTarget, COL_INFO);
            if (f.navDist > 0f)
                navInfo += " (" + FmtDist(f.navDist) + ")";
            GUILayout.Label("  Route: " + navInfo, richLabel);

            if (f.ships != null && f.ships.Count > 0)
            {
                GUILayout.Space(2f);
                foreach (var s in f.ships)
                {
                    string hc = s.healthRatio > 0.8f ? COL_OK
                        : s.healthRatio > 0.4f ? COL_WARN : COL_CRIT;
                    GUILayout.Label("    " + s.name
                        + "  HP:" + C(Pct(s.healthRatio), hc)
                        + "  Fuel:" + Pct(s.fuelRatio)
                        + "  DV:" + Fmt(s.dv) + "/" + Fmt(s.dvMax), richLabel);
                }
            }

            GUILayout.EndVertical();
        }

        #endregion

        #region Drawing - Feed Window

        private void DrawFeedWindow(int id)
        {
            if (bgTexture != null)
                GUI.DrawTexture(new Rect(0, 0, feedRect.width, feedRect.height), bgTexture);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Battle Feed</b>" + (harmonyActive ? "" : C(" (polling only)", COL_WARN)), richLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.Width(50f)))
                feed.Clear();
            if (GUILayout.Button(isFeedMinimized ? "\u25a1" : "\u2014", GUILayout.Width(28f)))
            {
                if (isFeedMinimized) { feedRect.height = feedNormalHeight; isFeedMinimized = false; }
                else { feedNormalHeight = feedRect.height; feedRect.height = 36f; isFeedMinimized = true; }
            }
            if (GUILayout.Button("\u2715", GUILayout.Width(28f)))
                showFeed = false;
            GUILayout.EndHorizontal();

            if (isFeedMinimized)
            {
                GUI.DragWindow();
                return;
            }

            feedScroll = GUILayout.BeginScrollView(feedScroll);
            if (feed.Count == 0)
                GUILayout.Label(C("Waiting for combat events...", COL_DIM), richLabel);
            else
                for (int i = 0; i < feed.Count; i++)
                    GUILayout.Label(feed[i].text, richLabel);
            GUILayout.EndScrollView();

            Rect handle = new Rect(feedRect.width - 18f, feedRect.height - 18f, 18f, 18f);
            GUI.Label(handle, "\u25e2");
            if (Event.current.type == EventType.MouseDown && handle.Contains(Event.current.mousePosition))
            {
                isResizingFeed = true;
                feedResizeOffset = new Vector2(feedRect.width, feedRect.height) - Event.current.mousePosition;
                Event.current.Use();
                return;
            }
            GUI.DragWindow();
        }

        #endregion

        #region Data Refresh

        private void RefreshData()
        {
            nextRefresh = Time.unscaledTime + RefreshInterval;

            playerShips.Clear();
            hostileShips.Clear();
            playerFleets.Clear();
            otherFleets.Clear();

            object cm = FindCurrent("CombatManager");
            if (cm != null)
            {
                if (!wasTactical)
                {
                    feed.Clear();
                    lastTarget.Clear();
                }
                isTactical = true;
                RefreshTactical(cm);
            }
            else
            {
                if (wasTactical)
                {
                    showFeed = false;
                    lastTarget.Clear();
                }
                isTactical = false;
                RefreshStrategic();
            }
            wasTactical = isTactical;
        }

        private void RefreshTactical(object cm)
        {
            bool over = ToBool(GetField(cm, "isCombatOver"));
            modeLabel = over ? "TACTICAL COMBAT [COMBAT OVER]" : "TACTICAL COMBAT";

            if (over)
                showFeed = false;

            IList pShips = GetField(cm, "playerShips") as IList;
            IList hShips = GetField(cm, "hostileShips") as IList;

            int pAlive = pShips != null ? pShips.Count : 0;
            int hAlive = hShips != null ? hShips.Count : 0;

            int pDestroyed = GetShipChildCount(GetField(cm, "playerShipsDestroyed"));
            int pDisint = GetShipChildCount(GetField(cm, "playerShipsDisintegrated"));
            int hDestroyed = GetShipChildCount(GetField(cm, "hostileShipsDestroyed"));
            int hDisint = GetShipChildCount(GetField(cm, "hostileShipsDisintegrated"));
            int pUnscathed = GetShipChildCount(GetField(cm, "playerShipsUnscathed"));
            int pDamaged = GetShipChildCount(GetField(cm, "playerShipsDamaged"));
            int hUnscathed = GetShipChildCount(GetField(cm, "hostileShipsUnscathed"));
            int hDamaged = GetShipChildCount(GetField(cm, "hostileShipsDamaged"));

            overviewText = C("Your Fleet: ", COL_HEAD) + C(pAlive + " alive", COL_OK)
                + " (" + pUnscathed + " ok, " + pDamaged + " damaged)";
            if (pDestroyed > 0 || pDisint > 0)
                overviewText += "  |  " + C(pDestroyed + " destroyed", COL_DEAD)
                    + (pDisint > 0 ? "  " + C(pDisint + " disintegrated", COL_DISINT) : "");
            overviewText += "\n" + C("Enemy Fleet: ", COL_HEAD) + C(hAlive + " alive", COL_CRIT)
                + " (" + hUnscathed + " ok, " + hDamaged + " damaged)";
            if (hDestroyed > 0 || hDisint > 0)
                overviewText += "  |  " + C(hDestroyed + " destroyed", COL_DEAD)
                    + (hDisint > 0 ? "  " + C(hDisint + " disintegrated", COL_DISINT) : "");

            if (pShips != null) ParseShipList(pShips, playerShips, true);
            if (hShips != null) ParseShipList(hShips, hostileShips, false);
        }

        private void ParseShipList(IList ships, List<ShipData> output, bool trackExtras)
        {
            int count = 0;
            foreach (object sc in ships)
            {
                if (sc == null || count >= MaxShipsPerSide) break;
                output.Add(ParseShipController(sc, trackExtras));
                count++;
            }
        }

        private ShipData ParseShipController(object sc, bool trackExtras)
        {
            var s = new ShipData();
            object track = GetField(sc, "track");
            s.name = Str(GetField(track, "publicName") ?? GetField(track, "trackName") ?? GetField(sc, "className"));
            s.classType = Str(GetField(sc, "classType"));
            s.status = Str(GetField(sc, "status"));
            s.healthRatio = ToFloat(GetField(sc, "totalHealthRatio"));
            s.isDead = ToBool(GetField(sc, "isDead"));
            s.armorThickness = ToFloat(GetField(sc, "armorThickness"));
            s.dcFree = ToInt(GetField(sc, "dcTeamsFree"));
            int dcBusy = ToInt(GetField(sc, "dcTeamsBusy"));
            s.dcTotal = s.dcFree + dcBusy;
            s.materials = ToInt(GetField(sc, "materials"));
            s.materialsMax = ToInt(GetField(sc, "materialsMax"));

            object curTarget = GetField(sc, "currentTarget");
            s.targetName = curTarget != null
                ? Str(GetField(curTarget, "publicName") ?? GetField(curTarget, "trackName") ?? "unknown")
                : "none";

            s.id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(sc);
            DetectTargetChange(s.id, s.name, GetFactionSide(track), s.targetName);

            IList modules = GetField(sc, "allModules") as IList;
            s.damaged = new List<ModuleData>();
            s.destroyed = new List<string>();
            s.totalModules = 0;

            if (modules != null)
            {
                s.totalModules = modules.Count;
                foreach (object mod in modules)
                {
                    if (mod == null) continue;
                    bool dead = ToBool(GetField(mod, "isDead"));
                    float hr = ToFloat(GetField(mod, "healthRatio"));
                    string pt = Str(GetField(mod, "partType"));

                    if (dead)
                    {
                        s.destroyed.Add(pt);
                    }
                    else if (hr < 0.98f)
                    {
                        float arHp = ToFloat(GetField(mod, "armorHealth"));
                        float arMax = ToFloat(GetField(mod, "armorHealthMax"));
                        s.damaged.Add(new ModuleData
                        {
                            name = Str(GetField(mod, "productName") ?? GetField(mod, "prefabName") ?? pt),
                            partType = pt,
                            healthRatio = hr,
                            armorRatio = arMax > 0f ? arHp / arMax : 1f
                        });
                    }
                }
            }

            if (trackExtras)
            {
                s.weapons = new List<WeaponData>();
                IList weaponSystems = GetField(sc, "weapons") as IList;
                if (weaponSystems != null)
                {
                    foreach (object ws in weaponSystems)
                    {
                        if (ws == null) continue;
                        bool wDead = ToBool(GetField(ws, "isDead"));
                        bool cease = ToBool(GetField(ws, "isCeaseFire"));

                        IList turrets = GetField(ws, "weapons") as IList;
                        int eng = 0;
                        int total = turrets != null ? turrets.Count : 0;
                        if (turrets != null)
                            foreach (object tw in turrets)
                                if (Str(GetField(tw, "status")) == "ENGAGING") eng++;

                        s.weapons.Add(new WeaponData
                        {
                            name = Str(GetField(ws, "weaponName")),
                            type = Str(GetField(ws, "type")),
                            status = wDead ? "DESTROYED" : cease ? "CEASE FIRE" : "ACTIVE",
                            mode = Str(GetField(ws, "currentMode")),
                            range = ToInt(GetField(ws, "range")),
                            engaging = eng,
                            totalTurrets = total
                        });
                    }
                }

                object hm = GetField(sc, "hm");
                if (hm != null)
                {
                    s.hasHeat = true;
                    s.heat = new HeatData
                    {
                        percent = ToFloat(GetField(hm, "heatPercent")),
                        netFlow = ToFloat(GetField(hm, "netHeatFlow")),
                        radiatorsOpen = ToBool(GetField(hm, "isRadiatorsExtended")),
                        timeToOverflow = ToFloat(GetField(hm, "timeToOverflow"))
                    };
                }
            }

            return s;
        }

        private void RefreshStrategic()
        {
            modeLabel = "STRATEGIC MAP";

            Type fmType = FindType("FleetManager");
            if (fmType == null)
            {
                overviewText = C("FleetManager not found.", COL_WARN);
                return;
            }

            UnityEngine.Object[] fleets = UnityEngine.Object.FindObjectsOfType(fmType);
            int playerCount = 0;

            foreach (object fleet in fleets)
            {
                var fd = ParseFleet(fleet);
                if (fd.factionId == 1) { playerCount++; playerFleets.Add(fd); }
                else otherFleets.Add(fd);
            }

            overviewText = "Fleets: " + fleets.Length + " total  |  "
                + C(playerCount + " player", COL_OK) + "  |  "
                + (fleets.Length - playerCount) + " other";
        }

        private FleetData ParseFleet(object fleet)
        {
            var fd = new FleetData();
            object track = GetField(fleet, "track");
            fd.factionId = ToInt(GetField(track, "factionID"));
            fd.name = Str(GetField(track, "publicName") ?? GetField(track, "trackName") ?? "fleet");
            fd.side = fd.factionId == 1 ? "PLAYER" : "[F" + fd.factionId + "]";
            fd.state = Str(GetField(fleet, "state"));
            fd.dv = ToFloat(GetField(fleet, "fleetDv"));
            fd.dvMax = ToFloat(GetField(fleet, "fleetDvMax"));

            object nav = GetField(fleet, "navigation");
            fd.navState = Str(GetField(nav, "currentState"));
            fd.flightType = Str(GetField(nav, "flightType"));
            fd.navDist = ToFloat(GetField(nav, "distanceToTarget"));
            object navTarget = GetField(nav, "target");
            fd.navTarget = navTarget != null ? Str(GetPropOrField(navTarget, "name") ?? "target") : "none";

            fd.ships = new List<FleetShipData>();
            IEnumerable shipList = GetField(fleet, "ships") as IEnumerable;
            if (shipList != null)
            {
                int idx = 0;
                foreach (object ship in shipList)
                {
                    if (idx >= MaxShipsPerFleet) break;
                    fd.ships.Add(new FleetShipData
                    {
                        name = Str(GetField(ship, "publicName") ?? "ship"),
                        healthRatio = InvokeFloat(ship, "GetHealthRatio", 1f),
                        fuelRatio = InvokeFloat(ship, "GetFuelRatio", 0f),
                        dv = ToFloat(GetField(ship, "dv")),
                        dvMax = ToFloat(GetField(ship, "dvMax"))
                    });
                    idx++;
                }
            }

            return fd;
        }

        private void DetectTargetChange(int scId, string shipName, string side, string curTarget)
        {
            if (lastTarget.TryGetValue(scId, out string prev))
            {
                if (prev != curTarget && curTarget != "none")
                    AddFeedColored(side + " \"" + shipName + "\" > targeting \"" + curTarget + "\"", COL_INFO);
            }
            lastTarget[scId] = curTarget;
        }

        private string GetFactionSide(object track)
        {
            if (track == null) return "";
            return ToInt(GetField(track, "factionID")) == 1 ? "PLAYER" : "ENEMY";
        }

        #endregion

        #region Feed helpers

        private static void AddFeedColored(string message, string colorHex)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            feed.Insert(0, new FeedEntry { text = C(ts, COL_DIM) + " " + C(message, colorHex) });
            while (feed.Count > MaxFeed) feed.RemoveAt(feed.Count - 1);
        }

        #endregion

        #region Style Init

        private void InitStyles()
        {
            if (stylesReady) return;
            richLabel = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                wordWrap = false,
                fontSize = 13,
                padding = new RectOffset(2, 2, 1, 1)
            };
            sectionBox = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(6, 6, 4, 4),
                margin = new RectOffset(0, 0, 2, 2)
            };
            stylesReady = true;
        }

        #endregion

        #region Reflection Helpers

        private Type FindType(string name)
        {
            if (typeCache.TryGetValue(name, out Type t)) return t;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(name);
                if (t != null) { typeCache[name] = t; return t; }
            }
            typeCache[name] = null;
            return null;
        }

        private object FindCurrent(string typeName)
        {
            Type t = FindType(typeName);
            if (t == null) return null;
            object cur = GetStaticField(t, "current");
            if (cur != null) return cur;
            PropertyInfo p = t.GetProperty("current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            cur = p != null ? p.GetValue(null, null) : null;
            return cur ?? UnityEngine.Object.FindObjectOfType(t);
        }

        private object GetField(object target, string name)
        {
            if (target == null) return null;
            Type type = target.GetType();
            string key = type.FullName + "." + name;
            if (!fieldCache.TryGetValue(key, out FieldInfo fi))
            {
                fi = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                fieldCache[key] = fi;
            }
            return fi != null ? fi.GetValue(target) : null;
        }

        private object GetStaticField(Type type, string name)
        {
            string key = type.FullName + ".s." + name;
            if (!fieldCache.TryGetValue(key, out FieldInfo fi))
            {
                fi = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                fieldCache[key] = fi;
            }
            return fi != null ? fi.GetValue(null) : null;
        }

        private object GetPropOrField(object target, string name)
        {
            if (target == null) return null;
            Type type = target.GetType();
            string pk = type.FullName + ".p." + name;
            if (!propCache.TryGetValue(pk, out PropertyInfo pi))
            {
                pi = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                propCache[pk] = pi;
            }
            if (pi != null) try { return pi.GetValue(target, null); } catch { }
            return GetField(target, name);
        }

        private float InvokeFloat(object target, string method, float fallback)
        {
            if (target == null) return fallback;
            try
            {
                Type type = target.GetType();
                string key = type.FullName + ".m." + method;
                if (!methodCache.TryGetValue(key, out MethodInfo mi))
                {
                    mi = type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    methodCache[key] = mi;
                }
                object r = mi != null ? mi.Invoke(target, null) : null;
                return r is float f ? f : fallback;
            }
            catch { return fallback; }
        }

        private static int GetShipChildCount(object obj)
        {
            if (obj is Transform t)
            {
                if (shipControllerType != null)
                {
                    int count = 0;
                    for (int i = 0; i < t.childCount; i++)
                    {
                        if (t.GetChild(i).GetComponent(shipControllerType) != null)
                            count++;
                    }
                    return count;
                }
                return t.childCount;
            }
            return 0;
        }

        #endregion

        #region Formatting Helpers

        private static string Str(object v) { return v == null ? "unknown" : Convert.ToString(v, CultureInfo.InvariantCulture); }
        private static int ToInt(object v) { return v is int i ? i : 0; }
        private static float ToFloat(object v) { return v is float f ? f : 0f; }
        private static bool ToBool(object v) { return v is bool b && b; }
        private static float SafeFloat(object v) { return v is float f ? f : 0f; }
        private static bool SafeBool(object v) { return v is bool b && b; }
        private static int SafeInt(object v) { return v is int i ? i : 0; }

        private static string Fmt(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        private static string Pct(float r) { return (r * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%"; }
        private static string PctS(float r) { return (r * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%"; }

        private static string FmtDist(float d)
        {
            if (d >= 1000000f) return (d / 1000000f).ToString("0.##", CultureInfo.InvariantCulture) + " Mm";
            if (d >= 1000f) return (d / 1000f).ToString("0.##", CultureInfo.InvariantCulture) + " km";
            return d.ToString("0.#", CultureInfo.InvariantCulture) + " m";
        }

        private static string C(string text, string color)
        {
            return "<color=" + color + ">" + text + "</color>";
        }

        private static string ColoredBar(float ratio)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(ratio * 10f), 0, 10);
            string col = ratio > 0.7f ? COL_OK : ratio > 0.35f ? COL_WARN : COL_CRIT;
            return C("[" + new string('|', filled) + new string('.', 10 - filled) + "]", col);
        }

        #endregion

        #region Hotkey API

        public static string GetHotkeySummary()
        {
            string ch = toggleChar != null ? toggleChar.Value : "";
            string k = toggleKey != null ? toggleKey.Value.ToString() : KeyCode.Plus.ToString();
            return string.IsNullOrEmpty(ch) ? k : k + " / " + ch;
        }

        public static void SetHotkey(string keyCodeName, string character)
        {
            if (toggleKey == null || toggleChar == null) return;
            if (Enum.TryParse(keyCodeName, out KeyCode pk)) toggleKey.Value = pk;
            toggleChar.Value = character ?? "";
            instance?.Config.Save();
        }

        private static bool MatchesChar(Event e)
        {
            string c = toggleChar != null ? toggleChar.Value : "";
            return !string.IsNullOrEmpty(c) && c.Length == 1 && e.character == c[0]
                && (toggleKey == null || toggleKey.Value == KeyCode.None || e.keyCode != toggleKey.Value);
        }

        #endregion
    }
}
