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
    [BepInPlugin("local.spacefleet.economy-debug", "Spacefleet Economy Debug", "0.1.0")]
    public sealed class EconomyDebugPlugin : BaseUnityPlugin
    {
        private const float RefreshIntervalSeconds = 1f;

        private static EconomyDebugPlugin instance;
        private static ConfigEntry<KeyCode> toggleKey;
        private static ConfigEntry<string> toggleCharacter;

        private readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();
        private readonly Dictionary<string, FieldInfo> fieldCache = new Dictionary<string, FieldInfo>();
        private readonly Dictionary<string, MethodInfo> methodCache = new Dictionary<string, MethodInfo>();
        private readonly List<string> overviewLines = new List<string>();
        private readonly List<string> recommendationLines = new List<string>();
        private readonly List<string> resourceSignals = new List<string>();
        private readonly List<string> marketSignals = new List<string>();
        private readonly List<string> traderSignals = new List<string>();
        private readonly List<string> technicalLines = new List<string>();

        private Rect windowRect = new Rect(80f, 70f, 940f, 680f);
        private Vector2 scroll;
        private bool isOpen;
        private bool showResources = true;
        private bool showMarkets = true;
        private bool showTraders = true;
        private bool showTechnical;
        private float nextRefreshTime;
        private string lastRefreshText = "not refreshed yet";
        private bool isMinimized;
        private float normalHeight;
        private bool isResizing;
        private Vector2 resizeOffset;
        private static readonly Vector2 MinSize = new Vector2(500f, 200f);
        private static bool skinChecked;
        private static GUISkin sharedSkin;
        private static Texture2D bgTexture;

        private void Awake()
        {
            instance = this;
            toggleKey = Config.Bind("Hotkeys", "ToggleKey", KeyCode.None, "Keyboard key that opens or closes the economy debug window.");
            toggleCharacter = Config.Bind("Hotkeys", "ToggleCharacter", "¡", "Optional typed character that opens or closes the economy debug window.");
            Logger.LogInfo("Spacefleet Economy Debug loaded. Hotkey: " + GetHotkeySummary() + ". Data refresh is throttled for performance.");
        }

        private void Update()
        {
            if (toggleKey.Value != KeyCode.None && Input.GetKeyDown(toggleKey.Value))
            {
                ToggleOpen();
            }
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && MatchesCharacter(e))
            {
                ToggleOpen();
                e.Use();
            }

            if (!isOpen)
            {
                return;
            }

            if (isResizing)
            {
                if (e.type == EventType.MouseDrag)
                {
                    windowRect.width = Mathf.Max(MinSize.x, e.mousePosition.x - windowRect.x + resizeOffset.x);
                    windowRect.height = Mathf.Max(MinSize.y, e.mousePosition.y - windowRect.y + resizeOffset.y);
                }
                if (e.rawType == EventType.MouseUp)
                {
                    isResizing = false;
                }
            }

            GUISkin skin = TryGetSharedSkin();
            GUISkin prev = GUI.skin;
            if (skin != null) GUI.skin = skin;

            GUI.depth = -1002;
            windowRect = GUI.Window(42012, windowRect, DrawWindow, "Spacefleet Economy Debug");

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

        private void ToggleOpen()
        {
            isOpen = !isOpen;
            nextRefreshTime = 0f;
        }

        private void DrawWindow(int id)
        {
            if (bgTexture != null)
                GUI.DrawTexture(new Rect(0, 0, windowRect.width, windowRect.height), bgTexture);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Economy Debug");
            GUILayout.FlexibleSpace();
            GUILayout.Label("Refresh: " + lastRefreshText);
            if (GUILayout.Button(isMinimized ? "\u25a1" : "\u2014", GUILayout.Width(28f)))
            {
                if (isMinimized) { windowRect.height = normalHeight; isMinimized = false; }
                else { normalHeight = windowRect.height; windowRect.height = 36f; isMinimized = true; }
            }
            if (GUILayout.Button("\u2715", GUILayout.Width(28f)))
            {
                isOpen = false;
            }
            GUILayout.EndHorizontal();

            if (isMinimized)
            {
                GUI.DragWindow();
                return;
            }

            GUILayout.BeginHorizontal();
            showResources = GUILayout.Toggle(showResources, "Resource Signals");
            showMarkets = GUILayout.Toggle(showMarkets, "Market Signals");
            showTraders = GUILayout.Toggle(showTraders, "Trader Signals");
            showTechnical = GUILayout.Toggle(showTechnical, "Technical Data");
            GUILayout.EndHorizontal();

            if (Time.unscaledTime >= nextRefreshTime)
            {
                RefreshData();
            }

            scroll = GUILayout.BeginScrollView(scroll);
            DrawSection("Economy Health", overviewLines);
            DrawSection("Recommended Player/Balance Actions", recommendationLines);
            if (showResources)
            {
                DrawSection("Resource Signals Beyond Global Market", resourceSignals);
            }
            if (showMarkets)
            {
                DrawSection("Market Stress Signals", marketSignals);
            }
            if (showTraders)
            {
                DrawSection("Trader Diagnostics", traderSignals);
            }
            if (showTechnical)
            {
                DrawSection("Technical Data", technicalLines);
            }
            GUILayout.EndScrollView();

            Rect handle = new Rect(windowRect.width - 18f, windowRect.height - 18f, 18f, 18f);
            GUI.Label(handle, "\u25e2");
            if (Event.current.type == EventType.MouseDown && handle.Contains(Event.current.mousePosition))
            {
                isResizing = true;
                resizeOffset = new Vector2(windowRect.width, windowRect.height) - Event.current.mousePosition;
                Event.current.Use();
                return;
            }
            GUI.DragWindow();
        }

        private static void DrawSection(string title, List<string> lines)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(title);
            if (lines.Count == 0)
            {
                GUILayout.Label("No issues detected in this category.");
            }
            else
            {
                foreach (string line in lines)
                {
                    GUILayout.Label(line);
                }
            }
            GUILayout.EndVertical();
        }

        private void RefreshData()
        {
            nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
            lastRefreshText = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            overviewLines.Clear();
            recommendationLines.Clear();
            resourceSignals.Clear();
            marketSignals.Clear();
            traderSignals.Clear();
            technicalLines.Clear();

            object gm = FindCurrent("GlobalMarket");
            object fm = FindCurrent("FactionsManager");
            object playerFaction = GetField(fm, "playerFaction");

            ICollection marketsCollection = GetField(gm, "allMarkets") as ICollection;
            IEnumerable markets = GetField(gm, "allMarkets") as IEnumerable;
            IEnumerable resources = GetField(GetField(gm, "allResources") ?? FindObject("AllResources"), "resources") as IEnumerable;

            overviewLines.Add("Player credits: " + (GetField(playerFaction, "credits") ?? "unknown"));
            overviewLines.Add("GlobalMarket: " + (gm != null ? "online" : "not available"));
            overviewLines.Add("Markets tracked: " + (marketsCollection?.Count ?? 0));

            int shortages = AnalyzeResources(gm, resources);
            int stressedMarkets = AnalyzeMarkets(markets);
            int traderIssues = AnalyzeTraders();

            string health = "Stable";
            if (shortages + stressedMarkets + traderIssues >= 6)
            {
                health = "Stressed";
            }
            else if (shortages + stressedMarkets + traderIssues >= 2)
            {
                health = "Watch";
            }

            overviewLines.Insert(0, "Overall economy state: " + health);
            overviewLines.Add("Shortage/oversupply signals: " + shortages);
            overviewLines.Add("Market stress signals: " + stressedMarkets);
            overviewLines.Add("Trader issues: " + traderIssues);

            if (recommendationLines.Count == 0)
            {
                recommendationLines.Add("No urgent balance issue detected from the sampled data.");
            }
        }

        private int AnalyzeResources(object gm, IEnumerable resources)
        {
            if (resources == null)
            {
                resourceSignals.Add("Resource list is not available yet. Load into a strategic game.");
                return 1;
            }

            int issues = 0;
            int shown = 0;
            foreach (object resource in resources)
            {
                if (resource == null)
                {
                    continue;
                }

                string name = Convert.ToString(GetField(resource, "resourceName") ?? "unknown");
                int basePrice = ToInt(GetField(resource, "basePrice"));
                int avgBuy = InvokeIntValue(gm, "GetAverageBuyPriceOf", resource);
                int avgSell = InvokeIntValue(gm, "GetAverageSellPriceOf", resource);
                float supply = InvokeFloatValue(gm, "GetSupplyOf", resource);
                float demand = InvokeFloatValue(gm, "GetDemandOf", resource);
                float ratio = supply <= 0.01f ? (demand > 0.01f ? 99f : 1f) : demand / supply;
                float buyVsBase = basePrice > 0 ? avgBuy / (float)basePrice : 1f;
                float sellVsBase = basePrice > 0 ? avgSell / (float)basePrice : 1f;

                technicalLines.Add(name + " | base " + basePrice + " | avg buy " + avgBuy + " | avg sell " + avgSell + " | supply " + Format(supply) + " | demand " + Format(demand) + " | D/S " + Format(ratio));

                bool signaled = false;
                if (ratio >= 2.5f)
                {
                    resourceSignals.Add("Shortage pressure: " + name + " demand is " + Format(ratio) + "x supply.");
                    recommendationLines.Add("Consider increasing production or imports for " + name + ".");
                    issues++;
                    signaled = true;
                }
                else if (ratio <= 0.4f && supply > 0.01f)
                {
                    resourceSignals.Add("Oversupply pressure: " + name + " demand is only " + Format(ratio) + "x supply.");
                    recommendationLines.Add("Consider adding consumption/export sinks for " + name + ".");
                    issues++;
                    signaled = true;
                }

                if (buyVsBase >= 3.5f || sellVsBase >= 3.5f)
                {
                    resourceSignals.Add("Price spike: " + name + " average price is far above base price.");
                    issues++;
                    signaled = true;
                }
                else if ((avgBuy > 0 && buyVsBase <= 0.4f) || (avgSell > 0 && sellVsBase <= 0.4f))
                {
                    resourceSignals.Add("Price collapse: " + name + " average price is far below base price.");
                    issues++;
                    signaled = true;
                }

                if (signaled)
                {
                    shown++;
                }

                if (shown >= 12)
                {
                    resourceSignals.Add("(showing first 12 resource signals)");
                    break;
                }
            }

            return issues;
        }

        private int AnalyzeMarkets(IEnumerable markets)
        {
            if (markets == null)
            {
                marketSignals.Add("Market list is not available yet.");
                return 1;
            }

            int issues = 0;
            int shown = 0;
            int index = 0;
            foreach (object market in markets)
            {
                object faction = GetField(market, "thisFaction");
                object inventory = GetField(market, "inventory");
                string factionName = Convert.ToString(GetField(faction, "factionName") ?? GetField(faction, "name") ?? GetField(faction, "factionID") ?? "unknown");
                int credits = ToInt(GetField(faction, "credits"));
                float stock = InvokeFloatValue(market, "GetTotalQuantity");
                float storageUsed = ToFloat(GetField(inventory, "storageUsed"));
                float storageMax = ToFloat(GetField(inventory, "storageMax"));
                float fill = storageMax > 0.01f ? storageUsed / storageMax : 0f;

                technicalLines.Add("Market #" + index + " " + factionName + " | credits " + credits + " | stock " + Format(stock) + " | storage " + Format(storageUsed) + "/" + Format(storageMax));

                if (credits <= 0)
                {
                    marketSignals.Add("No buyer liquidity: " + factionName + " has no credits.");
                    recommendationLines.Add("Credit-starved markets may stop buying even when demand exists: " + factionName + ".");
                    issues++;
                    shown++;
                }
                else if (credits < 500)
                {
                    marketSignals.Add("Low buyer liquidity: " + factionName + " has only " + credits + " credits.");
                    issues++;
                    shown++;
                }

                if (fill >= 0.95f)
                {
                    marketSignals.Add("Storage bottleneck: " + factionName + " storage is " + Format(fill * 100f) + "% full.");
                    recommendationLines.Add("Full markets need more storage, exports, or lower production.");
                    issues++;
                    shown++;
                }

                if (shown >= 12)
                {
                    marketSignals.Add("(showing first 12 market signals)");
                    break;
                }

                index++;
            }

            return issues;
        }

        private int AnalyzeTraders()
        {
            Type traderType = AccessType("Trader");
            if (traderType == null)
            {
                traderSignals.Add("Trader type unavailable.");
                return 1;
            }

            UnityEngine.Object[] traders = UnityEngine.Object.FindObjectsOfType(traderType);
            int active = 0;
            int buying = 0;
            int issues = 0;
            int shown = 0;

            for (int i = 0; i < traders.Length; i++)
            {
                object trader = traders[i];
                bool isTrading = ToBool(GetField(trader, "isTrading"));
                bool isBuying = ToBool(GetField(trader, "isBuying"));
                object targetMarket = GetField(trader, "targetMarket");
                object targetResource = GetField(trader, "targetResource");
                float targetQuantity = ToFloat(GetField(trader, "targetQuantity"));
                float margin = ToFloat(GetField(trader, "currentMinProfitMargin"));
                string resourceName = Convert.ToString(GetField(targetResource, "resourceName") ?? "none");

                if (isTrading)
                {
                    active++;
                }
                if (isBuying)
                {
                    buying++;
                }

                technicalLines.Add("Trader #" + i + " active=" + isTrading + " buying=" + isBuying + " target=" + resourceName + " qty=" + Format(targetQuantity) + " margin=" + Format(margin));

                if (isTrading && targetMarket == null)
                {
                    traderSignals.Add("Trader has no target market: #" + i + " resource=" + resourceName + ".");
                    issues++;
                    shown++;
                }
                else if (isTrading && targetResource == null)
                {
                    traderSignals.Add("Trader has no target resource: #" + i + ".");
                    issues++;
                    shown++;
                }
                else if (isTrading && targetQuantity > 0f && targetQuantity < 1f)
                {
                    traderSignals.Add("Dust trade detected: #" + i + " wants only " + Format(targetQuantity) + " of " + resourceName + ".");
                    recommendationLines.Add("Dust trades waste simulation time and can break trader price accounting.");
                    issues++;
                    shown++;
                }
                else if (isTrading && margin < 0f)
                {
                    traderSignals.Add("Trader accepted negative margin: #" + i + " margin=" + Format(margin) + ".");
                    issues++;
                    shown++;
                }

                if (shown >= 12)
                {
                    traderSignals.Add("(showing first 12 trader signals)");
                    break;
                }
            }

            overviewLines.Add("Traders active: " + active + "/" + traders.Length + " | buying: " + buying + " | selling: " + (active - buying));
            return issues;
        }

        private int InvokeIntValue(object target, string methodName, params object[] args)
        {
            object value = Invoke(target, methodName, args);
            return value is int i ? i : 0;
        }

        private float InvokeFloatValue(object target, string methodName, params object[] args)
        {
            object value = Invoke(target, methodName, args);
            return value is float f ? f : 0f;
        }

        private object Invoke(object target, string methodName, params object[] args)
        {
            if (target == null)
            {
                return null;
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

                return method?.Invoke(target, args);
            }
            catch
            {
                return null;
            }
        }

        private object FindCurrent(string typeName)
        {
            Type type = AccessType(typeName);
            if (type == null)
            {
                return null;
            }

            object current = GetStaticField(type, "current");
            if (current != null)
            {
                return current;
            }

            current = type.GetProperty("current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null, null);
            return current ?? UnityEngine.Object.FindObjectOfType(type);
        }

        private object FindObject(string typeName)
        {
            Type type = AccessType(typeName);
            return type != null ? UnityEngine.Object.FindObjectOfType(type) : null;
        }

        private Type AccessType(string typeName)
        {
            if (typeCache.TryGetValue(typeName, out Type cachedType))
            {
                return cachedType;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                {
                    typeCache[typeName] = type;
                    return type;
                }
            }

            typeCache[typeName] = null;
            return null;
        }

        private object GetField(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            Type type = target.GetType();
            string key = type.FullName + "." + name;
            if (!fieldCache.TryGetValue(key, out FieldInfo field))
            {
                field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                fieldCache[key] = field;
            }

            return field?.GetValue(target);
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

        private static int ToInt(object value)
        {
            return value is int i ? i : 0;
        }

        private static float ToFloat(object value)
        {
            return value is float f ? f : 0f;
        }

        private static bool ToBool(object value)
        {
            return value is bool b && b;
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string GetHotkeySummary()
        {
            string character = toggleCharacter != null ? toggleCharacter.Value : "";
            string key = toggleKey != null ? toggleKey.Value.ToString() : KeyCode.None.ToString();
            return string.IsNullOrEmpty(character) ? key : key + " / " + character;
        }

        public static void SetHotkey(string keyCodeName, string character)
        {
            if (toggleKey == null || toggleCharacter == null)
            {
                return;
            }

            if (Enum.TryParse(keyCodeName, out KeyCode parsedKey))
            {
                toggleKey.Value = parsedKey;
            }

            toggleCharacter.Value = character ?? "";
            instance?.Config.Save();
        }

        private static bool MatchesCharacter(Event e)
        {
            string configured = toggleCharacter != null ? toggleCharacter.Value : "";
            return !string.IsNullOrEmpty(configured)
                && configured.Length == 1
                && e.character == configured[0]
                && (toggleKey == null || toggleKey.Value == KeyCode.None || e.keyCode != toggleKey.Value);
        }
    }
}
