using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace SpacefleetEconomyDebug
{
    [BepInPlugin("local.spacefleet.economy-debug", "Spacefleet Economy Debug", "0.2.0")]
    public sealed class EconomyDebugPlugin : BaseUnityPlugin
    {
        private const float RefreshInterval = 1f;
        private const int   WinId = 42012;

        private const string COL_OK   = "#88FF88";
        private const string COL_WARN = "#FFCC44";
        private const string COL_CRIT = "#FF6644";
        private const string COL_INFO = "#AADDFF";
        private const string COL_DIM  = "#778899";
        private const string COL_HEAD = "#CCDDF8";
        private const string COL_GOLD = "#FFD966";

        private enum Tab { Overview, Resources, Markets, Traders, Stations }

        // ── statics ──────────────────────────────────────────────────
        private static EconomyDebugPlugin instance;
        private static ConfigEntry<KeyCode> toggleKey;
        private static ConfigEntry<string>  toggleCharacter;
        private static bool       skinChecked;
        private static GUISkin    sharedSkin;
        private static Texture2D  bgTex;
        private static Texture2D  headerTex;
        private static Texture2D  sepTex;
        private static Texture2D  barBgTex;
        private static Texture2D  barFillTex;

        // ── window state ─────────────────────────────────────────────
        private Rect    windowRect   = new Rect(60f, 50f, 960f, 680f);
        private bool    isOpen;
        private bool    isMinimized;
        private float   normalHeight;
        private bool    isResizing;
        private Vector2 resizeOffset;
        private static readonly Vector2 MinSize = new Vector2(520f, 220f);

        private Tab activeTab = Tab.Overview;

        // ── styles ───────────────────────────────────────────────────
        private GUIStyle richLabel;
        private GUIStyle sectionHeader;
        private GUIStyle tabOn;
        private GUIStyle tabOff;
        private GUIStyle stationBtn;
        private bool     stylesReady;

        // ── scrolls ──────────────────────────────────────────────────
        private Vector2 mainScroll;
        private Vector2 stationListScroll;
        private Vector2 stationDetailScroll;

        // ── reflection caches ────────────────────────────────────────
        private readonly Dictionary<string, Type>       typeCache   = new Dictionary<string, Type>();
        private readonly Dictionary<string, FieldInfo>  fieldCache  = new Dictionary<string, FieldInfo>();
        private readonly Dictionary<string, MethodInfo> methodCache = new Dictionary<string, MethodInfo>();

        // ── data lists ───────────────────────────────────────────────
        private readonly List<string> overviewLines      = new List<string>();
        private readonly List<string> recommendationLines = new List<string>();
        private readonly List<string> resourceSignals    = new List<string>();
        private readonly List<string> marketSignals      = new List<string>();
        private readonly List<string> traderSignals      = new List<string>();

        // ── station inspector ────────────────────────────────────────
        private readonly List<StationEntry> stations = new List<StationEntry>();
        private int    selectedStation = -1;
        private string stationSearch   = "";

        private float  nextRefreshTime;
        private string lastRefreshText = "--:--:--";

        private struct StationEntry
        {
            public string            name;
            public string            faction;
            public int               credits;
            public float             storageUsed;
            public float             storageMax;
            public List<ResourceRow> resources;
        }

        private struct ResourceRow
        {
            public string name;
            public float  quantity;
            public float  minStock;
            public int    buyPrice;
            public int    basePrice;
        }

        // ── lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            instance        = this;
            toggleKey       = Config.Bind("Hotkeys", "ToggleKey",       KeyCode.None, "Key to toggle the economy debug window.");
            toggleCharacter = Config.Bind("Hotkeys", "ToggleCharacter", "¡",          "Character to toggle the economy debug window.");
            Logger.LogInfo("Economy Debug v0.2 loaded. Hotkey: " + GetHotkeySummary());
        }

        private void Update()
        {
            if (toggleKey.Value != KeyCode.None && Input.GetKeyDown(toggleKey.Value))
                ToggleOpen();
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && MatchesCharacter(e)) { ToggleOpen(); e.Use(); }
            if (!isOpen) return;

            HandleResize(e);
            InitSkin();
            EnsureStyles();

            GUISkin prev = GUI.skin;
            if (sharedSkin != null) GUI.skin = sharedSkin;
            GUI.depth  = -1002;
            windowRect = GUI.Window(WinId, windowRect, DrawWindow, GUIContent.none);
            GUI.skin   = prev;
        }

        private void HandleResize(Event e)
        {
            if (!isResizing) return;
            if (e.type == EventType.MouseDrag)
            {
                windowRect.width  = Mathf.Max(MinSize.x, e.mousePosition.x - windowRect.x + resizeOffset.x);
                windowRect.height = Mathf.Max(MinSize.y, e.mousePosition.y - windowRect.y + resizeOffset.y);
            }
            if (e.rawType == EventType.MouseUp) isResizing = false;
        }

        private static void InitSkin()
        {
            if (skinChecked) return;
            skinChecked = true;
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("SpacefleetModMenu.ModMenuPlugin");
                    if (t == null) continue;
                    sharedSkin = t.GetMethod("GetSharedSkin", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null) as GUISkin;
                    bgTex      = t.GetMethod("GetBgTexture",  BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null) as Texture2D;
                    break;
                }
            }
            catch { }

            if (bgTex == null) { bgTex = MakeTex(new Color(0.05f, 0.06f, 0.09f, 0.97f)); DontDestroy(bgTex); }
            headerTex  = MakeTex(new Color(0.03f, 0.04f, 0.07f, 1f));   DontDestroy(headerTex);
            sepTex     = MakeTex(new Color(0.18f, 0.22f, 0.30f, 1f));   DontDestroy(sepTex);
            barBgTex   = MakeTex(new Color(0.10f, 0.12f, 0.16f, 1f));   DontDestroy(barBgTex);
            barFillTex = MakeTex(new Color(0.22f, 0.50f, 0.82f, 1f));   DontDestroy(barFillTex);
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }
        private static void DontDestroy(UnityEngine.Object o) => UnityEngine.Object.DontDestroyOnLoad(o);

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            richLabel = new GUIStyle(GUI.skin.label)
            {
                richText = true, wordWrap = false, fontSize = 12,
                padding  = new RectOffset(2, 2, 1, 1),
            };
            richLabel.normal.textColor = new Color(0.82f, 0.86f, 0.93f);

            sectionHeader = new GUIStyle(richLabel)
            {
                fontStyle = FontStyle.Bold, fontSize = 12,
            };
            sectionHeader.normal.textColor = new Color(0.68f, 0.80f, 1f);

            tabOn = new GUIStyle(GUI.skin.button) { richText = true, fontStyle = FontStyle.Bold, fontSize = 11 };
            tabOn.normal.textColor = new Color(0.80f, 0.92f, 1f);
            tabOn.hover.textColor  = new Color(0.90f, 0.96f, 1f);

            tabOff = new GUIStyle(GUI.skin.button) { richText = true, fontSize = 11 };
            tabOff.normal.textColor = new Color(0.50f, 0.55f, 0.63f);
            tabOff.hover.textColor  = new Color(0.70f, 0.76f, 0.84f);

            stationBtn = new GUIStyle(GUI.skin.button)
            {
                richText  = true, alignment = TextAnchor.MiddleLeft,
                fontSize  = 11,   wordWrap  = false,
                padding   = new RectOffset(6, 4, 3, 3),
            };
        }

        private void ToggleOpen()
        {
            isOpen         = !isOpen;
            nextRefreshTime = 0f;
            stylesReady    = false;
        }

        // ── window ───────────────────────────────────────────────────

        private void DrawWindow(int id)
        {
            float w = windowRect.width;
            float h = windowRect.height;

            if (bgTex     != null) GUI.DrawTexture(new Rect(0, 0, w, h),    bgTex);
            if (headerTex != null) GUI.DrawTexture(new Rect(0, 0, w, 26f),  headerTex);
            if (sepTex    != null) GUI.DrawTexture(new Rect(0, 26f, w, 1f), sepTex);

            GUI.Label(new Rect(8f, 4f, 260f, 18f),
                C("ECONOMY DEBUG", COL_HEAD) + "  " + C("v0.2", COL_DIM),
                richLabel ?? GUI.skin.label);
            GUI.Label(new Rect(w - 195f, 5f, 130f, 16f),
                C(lastRefreshText, COL_DIM), richLabel ?? GUI.skin.label);

            if (GUI.Button(new Rect(w - 52f, 3f, 22f, 20f), isMinimized ? "\u25a1" : "\u2014"))
            {
                if (isMinimized) { windowRect.height = normalHeight; isMinimized = false; }
                else             { normalHeight = h; windowRect.height = 26f; isMinimized = true; }
            }
            if (GUI.Button(new Rect(w - 26f, 3f, 22f, 20f), "\u2715"))
                isOpen = false;

            if (isMinimized) { GUI.DragWindow(new Rect(0, 0, w, 26f)); return; }

            DrawTabStrip(w);
            if (sepTex != null) GUI.DrawTexture(new Rect(0, 52f, w, 1f), sepTex);

            float contentY = 56f;
            float contentH = h - contentY - 18f;
            GUILayout.BeginArea(new Rect(4f, contentY, w - 8f, contentH));

            if (Time.unscaledTime >= nextRefreshTime)
                RefreshData();

            if (activeTab == Tab.Stations)
                DrawStationsTab(w - 8f, contentH);
            else
                DrawSignalsTab();

            GUILayout.EndArea();

            // Resize handle
            Rect handle = new Rect(w - 16f, h - 16f, 16f, 16f);
            GUI.Label(handle, C("\u25e2", COL_DIM), richLabel ?? GUI.skin.label);
            if (Event.current.type == EventType.MouseDown && handle.Contains(Event.current.mousePosition))
            {
                isResizing   = true;
                resizeOffset = new Vector2(w, h) - Event.current.mousePosition;
                Event.current.Use();
            }

            GUI.DragWindow(new Rect(0, 0, w, 26f));
        }

        private void DrawTabStrip(float w)
        {
            string[] labels = { "OVERVIEW", "RESOURCES", "MARKETS", "TRADERS", "STATIONS" };
            Tab[]    tabs   = { Tab.Overview, Tab.Resources, Tab.Markets, Tab.Traders, Tab.Stations };
            float x = 4f;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = activeTab == tabs[i];
                if (GUI.Button(new Rect(x, 28f, 108f, 22f), labels[i], active ? tabOn : tabOff))
                    activeTab = tabs[i];
                x += 110f;
            }
        }

        // ── signals tabs ─────────────────────────────────────────────

        private void DrawSignalsTab()
        {
            mainScroll = GUILayout.BeginScrollView(mainScroll);
            switch (activeTab)
            {
                case Tab.Overview:
                    DrawSection("ECONOMY OVERVIEW",        overviewLines);
                    DrawSection("BALANCE RECOMMENDATIONS", recommendationLines);
                    break;
                case Tab.Resources: DrawSection("RESOURCE SIGNALS",    resourceSignals); break;
                case Tab.Markets:   DrawSection("MARKET STRESS",       marketSignals);   break;
                case Tab.Traders:   DrawSection("TRADER DIAGNOSTICS",  traderSignals);   break;
            }
            GUILayout.EndScrollView();
        }

        private void DrawSection(string title, List<string> lines)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(C(title, COL_HEAD), sectionHeader ?? GUI.skin.label);
            Rect sepR = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(1f), GUILayout.ExpandWidth(true));
            if (sepTex != null) GUI.DrawTexture(sepR, sepTex);
            GUILayout.Space(2f);
            if (lines.Count == 0)
                GUILayout.Label(C("All clear.", COL_DIM), richLabel ?? GUI.skin.label);
            else
                foreach (string l in lines)
                    GUILayout.Label(l, richLabel ?? GUI.skin.label);
            GUILayout.EndVertical();
        }

        // ── stations tab ─────────────────────────────────────────────

        private void DrawStationsTab(float areaW, float areaH)
        {
            const float listW = 220f;
            GUILayout.BeginHorizontal(GUILayout.Height(areaH));

            // Left: station list
            GUILayout.BeginVertical(GUILayout.Width(listW));
            DrawStationList();
            GUILayout.EndVertical();

            // Separator
            Rect sepR = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            if (sepTex != null) GUI.DrawTexture(sepR, sepTex);
            GUILayout.Space(4f);

            // Right: detail
            GUILayout.BeginVertical();
            DrawStationDetail();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawStationList()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(22f));
            GUILayout.Label(C("Search", COL_DIM), richLabel ?? GUI.skin.label, GUILayout.Width(46f));
            stationSearch = GUILayout.TextField(stationSearch);
            GUILayout.EndHorizontal();

            Rect sepR = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(1f), GUILayout.ExpandWidth(true));
            if (sepTex != null) GUI.DrawTexture(sepR, sepTex);

            stationListScroll = GUILayout.BeginScrollView(stationListScroll);

            if (stations.Count == 0)
            {
                GUILayout.Label(C("No stations loaded.\nOpen a save first.", COL_DIM), richLabel ?? GUI.skin.label);
            }
            else
            {
                string filter = stationSearch.ToLowerInvariant();
                for (int i = 0; i < stations.Count; i++)
                {
                    StationEntry s = stations[i];
                    if (!string.IsNullOrEmpty(filter)
                        && !s.name.ToLowerInvariant().Contains(filter)
                        && !s.faction.ToLowerInvariant().Contains(filter))
                        continue;

                    bool isSel = i == selectedStation;
                    string label = C(isSel ? "\u25b6 " : "  ", isSel ? COL_INFO : COL_DIM)
                                 + C(s.name, isSel ? COL_OK : COL_HEAD)
                                 + "\n" + C("  " + s.faction, COL_DIM);

                    if (GUILayout.Button(label, stationBtn ?? GUI.skin.button, GUILayout.Height(36f)))
                    {
                        selectedStation      = i;
                        stationDetailScroll  = Vector2.zero;
                    }
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawStationDetail()
        {
            if (selectedStation < 0 || selectedStation >= stations.Count)
            {
                GUILayout.Label(C("\u2190  Select a station from the list.", COL_DIM), richLabel ?? GUI.skin.label);
                return;
            }

            StationEntry s = stations[selectedStation];
            stationDetailScroll = GUILayout.BeginScrollView(stationDetailScroll);

            GUILayout.Label(C(s.name, COL_HEAD), sectionHeader ?? GUI.skin.label);
            GUILayout.Label(
                C("Faction  ", COL_DIM) + C(s.faction, COL_INFO)
              + "     " + C("Credits  ", COL_DIM) + C(s.credits.ToString("#,0"), COL_GOLD),
                richLabel ?? GUI.skin.label);

            GUILayout.Space(4f);

            float fill    = s.storageMax > 0f ? s.storageUsed / s.storageMax : 0f;
            string fillCol = fill >= 0.95f ? COL_CRIT : fill >= 0.75f ? COL_WARN : COL_OK;
            GUILayout.Label(
                C("Storage  ", COL_DIM)
              + C(Format(s.storageUsed) + " / " + Format(s.storageMax), fillCol)
              + C("  (" + Mathf.RoundToInt(fill * 100f) + "%)", COL_DIM),
                richLabel ?? GUI.skin.label);
            DrawBar(fill);

            GUILayout.Space(6f);
            Rect sepR = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(1f), GUILayout.ExpandWidth(true));
            if (sepTex != null) GUI.DrawTexture(sepR, sepTex);
            GUILayout.Label(C("RESOURCES", COL_HEAD), sectionHeader ?? GUI.skin.label);

            // Column headers
            GUILayout.BeginHorizontal();
            GUILayout.Label(C("Name",     COL_DIM), richLabel ?? GUI.skin.label, GUILayout.Width(120f));
            GUILayout.Label(C("Qty",      COL_DIM), richLabel ?? GUI.skin.label, GUILayout.Width(64f));
            GUILayout.Label(C("Min",      COL_DIM), richLabel ?? GUI.skin.label, GUILayout.Width(64f));
            GUILayout.Label(C("Buy",      COL_DIM), richLabel ?? GUI.skin.label, GUILayout.Width(60f));
            GUILayout.Label(C("Base",     COL_DIM), richLabel ?? GUI.skin.label, GUILayout.Width(50f));
            GUILayout.EndHorizontal();

            Rect sepR2 = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(1f), GUILayout.ExpandWidth(true));
            if (sepTex != null) GUI.DrawTexture(sepR2, sepTex);

            if (s.resources == null || s.resources.Count == 0)
            {
                GUILayout.Label(C("No resource data.", COL_DIM), richLabel ?? GUI.skin.label);
            }
            else
            {
                foreach (ResourceRow r in s.resources)
                {
                    string qCol = r.quantity <= 0f ? COL_CRIT
                                : r.quantity < r.minStock ? COL_WARN : COL_OK;
                    string pCol = r.buyPrice > (int)(r.basePrice * 1.6f) ? COL_CRIT
                                : r.buyPrice > r.basePrice ? COL_WARN : COL_OK;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(C(r.name, r.quantity <= 0f ? COL_DIM : COL_HEAD), richLabel ?? GUI.skin.label, GUILayout.Width(120f));
                    GUILayout.Label(C(Format(r.quantity),        qCol),    richLabel ?? GUI.skin.label, GUILayout.Width(64f));
                    GUILayout.Label(C(Format(r.minStock),        COL_DIM), richLabel ?? GUI.skin.label, GUILayout.Width(64f));
                    GUILayout.Label(C(r.buyPrice.ToString(),     pCol),    richLabel ?? GUI.skin.label, GUILayout.Width(60f));
                    GUILayout.Label(C(r.basePrice.ToString(),    COL_DIM), richLabel ?? GUI.skin.label, GUILayout.Width(50f));
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawBar(float ratio)
        {
            Rect r = GUILayoutUtility.GetRect(1f, 6f, GUILayout.ExpandWidth(true));
            if (barBgTex   != null) GUI.DrawTexture(r, barBgTex);
            if (barFillTex != null)
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(ratio), r.height), barFillTex);
        }

        // ── data refresh ─────────────────────────────────────────────

        private void RefreshData()
        {
            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            lastRefreshText = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            overviewLines.Clear(); recommendationLines.Clear();
            resourceSignals.Clear(); marketSignals.Clear(); traderSignals.Clear();

            object gm       = FindCurrent("GlobalMarket");
            object fm       = FindCurrent("FactionsManager");
            object player   = GetField(fm, "playerFaction");

            ICollection marketsCol = GetField(gm, "allMarkets") as ICollection;
            IEnumerable markets    = GetField(gm, "allMarkets") as IEnumerable;
            object      allResObj  = GetField(gm, "allResources") ?? FindObject("AllResources");
            IEnumerable resources  = GetField(allResObj, "resources") as IEnumerable;

            overviewLines.Add(C("Player credits   ", COL_DIM) + C(FormatCredits(GetField(player, "credits")), COL_GOLD));
            overviewLines.Add(C("Global market    ", COL_DIM) + C(gm != null ? "online" : "offline", gm != null ? COL_OK : COL_CRIT));
            overviewLines.Add(C("Tracked markets  ", COL_DIM) + C((marketsCol?.Count ?? 0).ToString(), COL_INFO));

            int s1 = AnalyzeResources(gm, resources);
            int s2 = AnalyzeMarkets(markets);
            int s3 = AnalyzeTraders();
            int tot = s1 + s2 + s3;

            string health    = tot >= 6 ? "Stressed" : tot >= 2 ? "Watch" : "Stable";
            string healthCol = health == "Stressed" ? COL_CRIT : health == "Watch" ? COL_WARN : COL_OK;

            overviewLines.Insert(0, C("Economy status   ", COL_DIM) + C(health, healthCol));
            overviewLines.Add(C("Resource issues  ", COL_DIM) + C(s1.ToString(), s1 > 0 ? COL_WARN : COL_OK));
            overviewLines.Add(C("Market issues    ", COL_DIM) + C(s2.ToString(), s2 > 0 ? COL_WARN : COL_OK));
            overviewLines.Add(C("Trader issues    ", COL_DIM) + C(s3.ToString(), s3 > 0 ? COL_WARN : COL_OK));

            if (recommendationLines.Count == 0)
                recommendationLines.Add(C("No urgent issues detected.", COL_DIM));

            RefreshStations(gm, markets);
        }

        private void RefreshStations(object gm, IEnumerable markets)
        {
            string prevName = selectedStation >= 0 && selectedStation < stations.Count
                ? stations[selectedStation].name : null;

            stations.Clear();
            if (markets == null) return;

            foreach (object market in markets)
            {
                if (market == null) continue;

                object faction   = GetField(market, "thisFaction");
                object inventory = GetField(market, "inventory");

                string factionName = Convert.ToString(
                    GetField(faction, "factionName") ?? GetField(faction, "factionID") ?? "Unknown");
                string stationName = TryGetTrackName(market) ?? (factionName + " Market");

                int   credits = ToInt(GetField(faction, "credits"));
                float used    = ToFloat(GetField(inventory, "storageUsed"));
                float maxS    = ToFloat(GetField(inventory, "storageMax"));

                var rows = new List<ResourceRow>();
                IEnumerable allStock = GetField(inventory, "resources") as IEnumerable;
                if (allStock != null)
                {
                    foreach (object rq in allStock)
                    {
                        if (rq == null) continue;
                        object resDef = GetField(rq, "resource");
                        if (resDef == null) continue;
                        float  qty    = ToFloat(GetField(rq, "quantity"));
                        string rName  = Convert.ToString(GetField(resDef, "resourceName") ?? "?");
                        int    basePr = ToInt(GetField(resDef, "basePrice"));
                        float  minSt  = InvokeFloat(market, "GetMinStockQuantity", resDef);
                        int    buyPr  = InvokeInt(market, "GetCurrentPrice", resDef, (object)true);

                        rows.Add(new ResourceRow
                        {
                            name = rName, quantity = qty, minStock = minSt,
                            buyPrice = buyPr, basePrice = basePr,
                        });
                    }
                }

                stations.Add(new StationEntry
                {
                    name = stationName, faction = factionName, credits = credits,
                    storageUsed = used, storageMax = maxS, resources = rows,
                });
            }

            // Restore selection by name
            if (prevName != null)
                for (int i = 0; i < stations.Count; i++)
                    if (stations[i].name == prevName) { selectedStation = i; return; }

            if (selectedStation >= stations.Count) selectedStation = -1;
        }

        private string TryGetTrackName(object market)
        {
            try
            {
                if (!(market is UnityEngine.Component comp)) return null;
                Type trackType = AccessType("Track");
                if (trackType == null) return null;
                UnityEngine.Component track = comp.GetComponent(trackType);
                if (track == null) return null;
                FieldInfo f = track.GetType().GetField("publicName", BindingFlags.Public | BindingFlags.Instance);
                string name = f?.GetValue(track) as string;
                if (!string.IsNullOrEmpty(name)) return name;
                f = track.GetType().GetField("id", BindingFlags.Public | BindingFlags.Instance);
                string id = f?.GetValue(track) as string;
                return string.IsNullOrEmpty(id) ? null : id;
            }
            catch { return null; }
        }

        // ── analysis ─────────────────────────────────────────────────

        private int AnalyzeResources(object gm, IEnumerable resources)
        {
            if (resources == null)
            {
                resourceSignals.Add(C("Not available — load a save first.", COL_DIM));
                return 1;
            }

            int issues = 0, shown = 0;
            foreach (object res in resources)
            {
                if (res == null) continue;
                string name  = Convert.ToString(GetField(res, "resourceName") ?? "?");
                int    basePr = ToInt(GetField(res, "basePrice"));
                float  supply = InvokeFloat(gm, "GetSupplyOf", res);
                float  demand = InvokeFloat(gm, "GetDemandOf", res);
                int    avgBuy = InvokeInt(gm, "GetAverageBuyPriceOf", res);
                int    avgSel = InvokeInt(gm, "GetAverageSellPriceOf", res);
                float  ratio  = supply <= 0.01f ? (demand > 0.01f ? 99f : 1f) : demand / supply;
                float  bvb    = basePr > 0 ? avgBuy / (float)basePr : 1f;
                float  svb    = basePr > 0 ? avgSel / (float)basePr : 1f;

                bool flagged = false;
                if (ratio >= 2.5f)
                {
                    resourceSignals.Add(C("\u25b2 Shortage: ", COL_CRIT)  + C(name, COL_HEAD) + C("  D/S=" + Format(ratio), COL_DIM));
                    recommendationLines.Add(C("+ ", COL_OK)   + "Raise supply of " + C(name, COL_HEAD));
                    issues++; flagged = true;
                }
                else if (ratio <= 0.4f && supply > 0.01f)
                {
                    resourceSignals.Add(C("\u25bc Oversupply: ", COL_WARN) + C(name, COL_HEAD) + C("  D/S=" + Format(ratio), COL_DIM));
                    recommendationLines.Add(C("- ", COL_WARN) + "Add sinks for " + C(name, COL_HEAD));
                    issues++; flagged = true;
                }
                if (bvb >= 3.5f || svb >= 3.5f)
                {
                    resourceSignals.Add(C("\u25b2 Price spike: ", COL_CRIT)    + C(name, COL_HEAD));
                    issues++; flagged = true;
                }
                else if ((avgBuy > 0 && bvb <= 0.4f) || (avgSel > 0 && svb <= 0.4f))
                {
                    resourceSignals.Add(C("\u25bc Price collapse: ", COL_WARN) + C(name, COL_HEAD));
                    issues++; flagged = true;
                }

                if (flagged && ++shown >= 12) { resourceSignals.Add(C("  (first 12 shown)", COL_DIM)); break; }
            }
            return issues;
        }

        private int AnalyzeMarkets(IEnumerable markets)
        {
            if (markets == null)
            {
                marketSignals.Add(C("Not available.", COL_DIM));
                return 1;
            }

            int issues = 0, shown = 0;
            foreach (object market in markets)
            {
                object faction = GetField(market, "thisFaction");
                object inv     = GetField(market, "inventory");
                string fname   = Convert.ToString(GetField(faction, "factionName") ?? GetField(faction, "factionID") ?? "?");
                int    creds   = ToInt(GetField(faction, "credits"));
                float  used    = ToFloat(GetField(inv, "storageUsed"));
                float  maxS    = ToFloat(GetField(inv, "storageMax"));
                float  fill    = maxS > 0.01f ? used / maxS : 0f;

                if (creds <= 0)
                {
                    marketSignals.Add(C("\u25cf Bankrupt: ",   COL_CRIT) + C(fname, COL_HEAD) + C(" — 0 credits", COL_DIM));
                    recommendationLines.Add(C("! ", COL_CRIT) + "Bankrupt market: " + C(fname, COL_HEAD));
                    issues++; shown++;
                }
                else if (creds < 500)
                {
                    marketSignals.Add(C("\u25cf Low credits: ", COL_WARN) + C(fname, COL_HEAD) + C(" — " + creds, COL_DIM));
                    issues++; shown++;
                }
                if (fill >= 0.95f)
                {
                    marketSignals.Add(C("\u25cf Full: ", COL_WARN) + C(fname, COL_HEAD) + C(" — " + Mathf.RoundToInt(fill * 100f) + "%", COL_DIM));
                    recommendationLines.Add(C("! ", COL_WARN) + "Storage full: " + C(fname, COL_HEAD));
                    issues++; shown++;
                }
                if (shown >= 12) { marketSignals.Add(C("  (first 12 shown)", COL_DIM)); break; }
            }
            return issues;
        }

        private int AnalyzeTraders()
        {
            Type tt = AccessType("Trader");
            if (tt == null) { traderSignals.Add(C("Trader type unavailable.", COL_DIM)); return 1; }

            UnityEngine.Object[] traders = UnityEngine.Object.FindObjectsOfType(tt);
            int active = 0, buying = 0, issues = 0, shown = 0;

            for (int i = 0; i < traders.Length; i++)
            {
                object tr      = traders[i];
                bool   trading = ToBool(GetField(tr, "isTrading"));
                bool   bid     = ToBool(GetField(tr, "isBuying"));
                object tRes    = GetField(tr, "targetResource");
                object tMkt    = GetField(tr, "targetMarket");
                float  qty     = ToFloat(GetField(tr, "targetQuantity"));
                float  margin  = ToFloat(GetField(tr, "currentMinProfitMargin"));
                string rn      = Convert.ToString(GetField(tRes, "resourceName") ?? "none");

                if (trading) active++;
                if (bid)     buying++;

                if      (trading && tMkt == null)          { traderSignals.Add(C("\u25cf No market: ",   COL_WARN) + C("#" + i + " " + rn, COL_DIM));                    issues++; shown++; }
                else if (trading && tRes == null)          { traderSignals.Add(C("\u25cf No resource: ", COL_WARN) + C("#" + i,            COL_DIM));                    issues++; shown++; }
                else if (trading && qty > 0f && qty < 1f) { traderSignals.Add(C("\u25cf Dust trade: ",  COL_WARN) + C("#" + i + " " + Format(qty) + " " + rn, COL_DIM)); issues++; shown++; }
                else if (trading && margin < 0f)           { traderSignals.Add(C("\u25cf Neg margin: ",  COL_CRIT) + C("#" + i + " m=" + Format(margin), COL_DIM));        issues++; shown++; }

                if (shown >= 12) { traderSignals.Add(C("  (first 12 shown)", COL_DIM)); break; }
            }

            overviewLines.Add(
                C("Traders active   ", COL_DIM)
              + C(active + "/" + traders.Length, active > 0 ? COL_OK : COL_DIM)
              + C("   buy " + buying + "  sell " + (active - buying), COL_DIM));
            return issues;
        }

        // ── reflection ───────────────────────────────────────────────

        private object FindCurrent(string typeName)
        {
            Type type = AccessType(typeName);
            if (type == null) return null;
            object cur = GetStaticField(type, "current");
            if (cur != null) return cur;
            cur = type.GetProperty("current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null, null);
            return cur ?? UnityEngine.Object.FindObjectOfType(type);
        }

        private object FindObject(string typeName)
        {
            Type type = AccessType(typeName);
            return type != null ? UnityEngine.Object.FindObjectOfType(type) : null;
        }

        private Type AccessType(string typeName)
        {
            if (typeCache.TryGetValue(typeName, out Type cached)) return cached;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(typeName);
                if (t != null) { typeCache[typeName] = t; return t; }
            }
            typeCache[typeName] = null;
            return null;
        }

        private object GetField(object target, string name)
        {
            if (target == null) return null;
            try
            {
                Type type = target.GetType();
                string key = type.FullName + "." + name;
                if (!fieldCache.TryGetValue(key, out FieldInfo field))
                {
                    field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    fieldCache[key] = field;
                }
                return field?.GetValue(target);
            }
            catch { return null; }
        }

        private object GetStaticField(Type type, string name)
        {
            string key = type.FullName + "." + name;
            if (!fieldCache.TryGetValue(key, out FieldInfo field))
            {
                field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                fieldCache[key] = field;
            }
            return field?.GetValue(null);
        }

        private object Invoke(object target, string methodName, params object[] args)
        {
            if (target == null) return null;
            try
            {
                Type type = target.GetType();
                string key = type.FullName + "." + methodName;
                if (!methodCache.TryGetValue(key, out MethodInfo method))
                {
                    method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    methodCache[key] = method;
                }
                return method?.Invoke(target, args);
            }
            catch { return null; }
        }

        private int   InvokeInt(object t,   string m, params object[] a) { object v = Invoke(t, m, a); return v is int   i ? i : 0; }
        private float InvokeFloat(object t, string m, params object[] a) { object v = Invoke(t, m, a); return v is float f ? f : 0f; }

        // ── formatting ───────────────────────────────────────────────

        private static string C(string text, string hex) => "<color=" + hex + ">" + text + "</color>";
        private static string Format(float v)            => v.ToString("0.##", CultureInfo.InvariantCulture);
        private static string FormatCredits(object raw)
        {
            if (raw is int i)   return i.ToString("#,0");
            if (raw is float f) return f.ToString("#,0");
            return "?";
        }
        private static int   ToInt(object v)  => v is int   i ? i : 0;
        private static float ToFloat(object v) => v is float f ? f : 0f;
        private static bool  ToBool(object v)  => v is bool  b && b;

        // ── ModMenu hotkey API ────────────────────────────────────────

        public static string GetHotkeySummary()
        {
            string c = toggleCharacter?.Value ?? "";
            string k = toggleKey?.Value.ToString() ?? KeyCode.None.ToString();
            return string.IsNullOrEmpty(c) ? k : k + " / " + c;
        }

        public static void SetHotkey(string keyCodeName, string character)
        {
            if (toggleKey == null || toggleCharacter == null) return;
            if (Enum.TryParse(keyCodeName, out KeyCode k)) toggleKey.Value = k;
            toggleCharacter.Value = character ?? "";
            instance?.Config.Save();
        }

        private static bool MatchesCharacter(Event e)
        {
            string cfg = toggleCharacter?.Value ?? "";
            return !string.IsNullOrEmpty(cfg) && cfg.Length == 1 && e.character == cfg[0]
                && (toggleKey == null || toggleKey.Value == KeyCode.None || e.keyCode != toggleKey.Value);
        }
    }
}
