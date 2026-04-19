using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpacefleetConsole
{
    [BepInPlugin("local.spacefleet.console", "Spacefleet Console", "0.1.0")]
    public sealed class ConsolePlugin : BaseUnityPlugin
    {
        private static ConsolePlugin instance;
        private static ConfigEntry<KeyCode> toggleKey;
        private static ConfigEntry<string> toggleCharacter;

        private readonly List<string> lines = new List<string>();
        private readonly List<string> history = new List<string>();
        private Rect windowRect = new Rect(80f, 80f, 760f, 430f);
        private Vector2 scroll;
        private string input = "";
        private bool isOpen;
        private int historyIndex = -1;
        private bool submitRequested;
        private bool isMinimized;
        private float normalHeight;
        private bool isResizing;
        private Vector2 resizeOffset;
        private static readonly Vector2 MinSize = new Vector2(400f, 200f);
        private static bool skinChecked;
        private static GUISkin sharedSkin;
        private static Texture2D bgTexture;

        private void Awake()
        {
            instance = this;
            toggleKey = Config.Bind("Hotkeys", "ToggleKey", KeyCode.BackQuote, "Keyboard key that opens or closes this console.");
            toggleCharacter = Config.Bind("Hotkeys", "ToggleCharacter", "º", "Optional typed character that opens or closes this console.");
            AddLine("Spacefleet Console loaded. Hotkey: " + GetHotkeySummary() + ".");
            Logger.LogInfo("Spacefleet Console loaded. Hotkey: " + GetHotkeySummary() + ".");
        }

        private void Update()
        {
            if (toggleKey.Value != KeyCode.None && Input.GetKeyDown(toggleKey.Value))
            {
                isOpen = !isOpen;
            }
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && MatchesCharacter(e))
            {
                isOpen = !isOpen;
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

            GUI.depth = -1001;
            windowRect = GUI.Window(42011, windowRect, DrawWindow, "Spacefleet Console");

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

        private void DrawWindow(int id)
        {
            if (bgTexture != null)
                GUI.DrawTexture(new Rect(0, 0, windowRect.width, windowRect.height), bgTexture);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Console");
            GUILayout.FlexibleSpace();
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

            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                submitRequested = true;
                e.Use();
            }

            float scrollHeight = Mathf.Max(80f, windowRect.height - 125f);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(scrollHeight));
            foreach (string line in lines)
            {
                GUILayout.Label(line);
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("ConsoleInput");
            input = GUILayout.TextField(input);
            if (GUILayout.Button("Run", GUILayout.Width(70f)) || submitRequested)
            {
                submitRequested = false;
                RunInput();
            }
            GUILayout.EndHorizontal();

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.UpArrow)
                {
                    MoveHistory(-1);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow)
                {
                    MoveHistory(1);
                    e.Use();
                }
            }

            GUI.FocusControl("ConsoleInput");

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

        private void RunInput()
        {
            string command = input.Trim();
            if (command.Length == 0)
            {
                return;
            }

            AddLine("> " + command);
            history.Add(command);
            historyIndex = history.Count;
            input = "";

            try
            {
                Execute(command);
            }
            catch (Exception ex)
            {
                AddLine("ERROR: " + ex.GetType().Name + ": " + ex.Message);
                Logger.LogError(ex);
            }

            scroll.y = float.MaxValue;
        }

        private void Execute(string commandLine)
        {
            string[] parts = SplitCommand(commandLine);
            string command = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";

            switch (command)
            {
                case "help":
                    PrintHelp();
                    break;
                case "clear":
                    lines.Clear();
                    break;
                case "close":
                    isOpen = false;
                    break;
                case "mods":
                    foreach (var plugin in Chainloader.PluginInfos.Values.OrderBy(p => p.Metadata.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        AddLine(plugin.Metadata.Name + " v" + plugin.Metadata.Version + " [" + plugin.Metadata.GUID + "]");
                    }
                    break;
                case "scene":
                    AddLine("Scene: " + SceneManager.GetActiveScene().name);
                    break;
                case "time":
                    AddLine("Unity timeScale: " + Time.timeScale.ToString("0.###", CultureInfo.InvariantCulture));
                    AddLine(ReadTimeManager());
                    break;
                case "timescale":
                    Require(parts, 2, "timescale <value>");
                    Time.timeScale = ParseFloat(parts[1]);
                    AddLine("Unity timeScale set to " + Time.timeScale.ToString("0.###", CultureInfo.InvariantCulture));
                    break;
                case "credits":
                case "money":
                    AddLine("Player credits: " + GetPlayerCredits());
                    break;
                case "addcredits":
                case "addmoney":
                    Require(parts, 2, "addcredits <amount>");
                    ModPlayerCredits("CONSOLE", ParseInt(parts[1]));
                    AddLine("Player credits: " + GetPlayerCredits());
                    break;
                case "setcredits":
                case "setmoney":
                    Require(parts, 2, "setcredits <amount>");
                    SetPlayerCredits(ParseInt(parts[1]));
                    AddLine("Player credits: " + GetPlayerCredits());
                    break;
                case "economy":
                    AddLine(EconomySnapshot());
                    break;
                case "objects":
                    Require(parts, 2, "objects <TypeName>");
                    ListObjects(parts[1]);
                    break;
                case "dump":
                    Require(parts, 3, "dump <TypeName> <index>");
                    DumpObject(parts[1], ParseInt(parts[2]));
                    break;
                case "get":
                    Require(parts, 4, "get <TypeName> <index> <field>");
                    GetObjectField(parts[1], ParseInt(parts[2]), parts[3]);
                    break;
                case "set":
                    Require(parts, 5, "set <TypeName> <index> <field> <value>");
                    SetObjectField(parts[1], ParseInt(parts[2]), parts[3], parts[4]);
                    break;
                case "invoke":
                    Require(parts, 3, "invoke <TypeName> <index> <method>");
                    InvokeObjectMethod(parts[1], ParseInt(parts[2]), parts[3]);
                    break;
                case "fleets":
                    ListFleets();
                    break;
                case "fleet":
                    Require(parts, 2, "fleet <index>");
                    PrintFleet(ParseInt(parts[1]));
                    break;
                case "fleetrefuel":
                    Require(parts, 2, "fleetrefuel <index|all> [ratio]");
                    RefuelFleetCommand(parts[1], parts.Length >= 3 ? ParseFloat(parts[2]) : 1f);
                    break;
                case "fleetrepair":
                    Require(parts, 2, "fleetrepair <index|all> [ratio]");
                    RepairFleetCommand(parts[1], parts.Length >= 3 ? ParseFloat(parts[2]) : 1f);
                    break;
                case "fleetrecover":
                    Require(parts, 2, "fleetrecover <index|all>");
                    RecoverFleetCommand(parts[1]);
                    break;
                default:
                    AddLine("Unknown command. Type: help");
                    break;
            }
        }

        private void PrintHelp()
        {
            AddLine("Core: help, clear, close, mods, scene, time, timescale <value>");
            AddLine("Money: credits, addcredits <amount>, setcredits <amount>");
            AddLine("Economy: economy");
            AddLine("Dev objects: objects <TypeName>, dump <TypeName> <index>, get <TypeName> <index> <field>, set <TypeName> <index> <field> <value>, invoke <TypeName> <index> <method>");
            AddLine("Fleet recovery: fleets, fleet <index>, fleetrefuel <index|all> [ratio], fleetrepair <index|all> [ratio], fleetrecover <index|all>");
            AddLine("Press Enter to run commands. Use Up/Down for command history.");
        }

        private static string[] SplitCommand(string value)
        {
            return value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private void MoveHistory(int delta)
        {
            if (history.Count == 0)
            {
                return;
            }

            historyIndex = Mathf.Clamp(historyIndex + delta, 0, history.Count - 1);
            input = history[historyIndex];
        }

        private void AddLine(string line)
        {
            lines.Add(line);
            while (lines.Count > 200)
            {
                lines.RemoveAt(0);
            }
        }

        private static void Require(string[] parts, int count, string usage)
        {
            if (parts.Length < count)
            {
                throw new ArgumentException("Usage: " + usage);
            }
        }

        private static int ParseInt(string value)
        {
            return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static object FindCurrent(string typeName)
        {
            Type type = AccessType(typeName);
            if (type == null)
            {
                return null;
            }

            FieldInfo currentField = type.GetField("current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            object current = currentField?.GetValue(null);
            if (current != null)
            {
                return current;
            }

            PropertyInfo currentProperty = type.GetProperty("current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            current = currentProperty?.GetValue(null, null);
            if (current != null)
            {
                return current;
            }

            return UnityEngine.Object.FindObjectOfType(type);
        }

        private static Type AccessType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private void ListObjects(string typeName)
        {
            Type type = AccessType(typeName);
            if (type == null)
            {
                AddLine("Type not found: " + typeName);
                return;
            }

            UnityEngine.Object[] objects = UnityEngine.Object.FindObjectsOfType(type);
            AddLine(type.Name + " objects: " + objects.Length);
            for (int i = 0; i < objects.Length && i < 40; i++)
            {
                AddLine("#" + i + " " + DescribeObject(objects[i]));
            }
        }

        private void DumpObject(string typeName, int index)
        {
            object target = GetObjectByIndex(typeName, index);
            if (target == null)
            {
                return;
            }

            Type type = target.GetType();
            AddLine("Dump " + type.Name + " #" + index);
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).OrderBy(f => f.Name))
            {
                AddLine(field.Name + " = " + FormatValue(field.GetValue(target)));
            }
        }

        private void GetObjectField(string typeName, int index, string fieldName)
        {
            object target = GetObjectByIndex(typeName, index);
            FieldInfo field = GetFieldInfo(target, fieldName);
            AddLine(fieldName + " = " + FormatValue(field?.GetValue(target)));
        }

        private void SetObjectField(string typeName, int index, string fieldName, string rawValue)
        {
            object target = GetObjectByIndex(typeName, index);
            FieldInfo field = GetFieldInfo(target, fieldName);
            if (field == null)
            {
                AddLine("Field not found: " + fieldName);
                return;
            }

            field.SetValue(target, ConvertString(rawValue, field.FieldType));
            AddLine(fieldName + " set to " + rawValue);
        }

        private void InvokeObjectMethod(string typeName, int index, string methodName)
        {
            object target = GetObjectByIndex(typeName, index);
            MethodInfo method = target?.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method == null)
            {
                AddLine("No parameterless method found: " + methodName);
                return;
            }

            object result = method.Invoke(target, null);
            AddLine(methodName + "() => " + FormatValue(result));
        }

        private void ListFleets()
        {
            UnityEngine.Object[] fleets = FindObjects("FleetManager");
            AddLine("Fleets: " + fleets.Length);
            for (int i = 0; i < fleets.Length; i++)
            {
                AddLine("#" + i + " " + FleetSummary(fleets[i]));
            }
        }

        private void PrintFleet(int index)
        {
            object fleet = GetObjectByIndex("FleetManager", index);
            if (fleet == null)
            {
                return;
            }

            AddLine("Fleet #" + index + ": " + FleetSummary(fleet));
            IEnumerable ships = GetField(fleet, "ships") as IEnumerable;
            int shipIndex = 0;
            if (ships != null)
            {
                foreach (object ship in ships)
                {
                    AddLine("  ship #" + shipIndex + " " + GetField(ship, "publicName") + " dv=" + FormatFloat(GetField(ship, "dv")) + "/" + FormatFloat(GetField(ship, "dvMax")) + " fuel=" + InvokeFloat(ship, "GetFuelRatio"));
                    shipIndex++;
                }
            }
        }

        private void RefuelFleetCommand(string selector, float ratio)
        {
            ForEachFleet(selector, fleet =>
            {
                RefuelFleet(fleet, ratio);
                InvokeNoArgs(fleet, "UpdateDv");
                AddLine("Refueled " + FleetSummary(fleet));
            });
        }

        private void RepairFleetCommand(string selector, float ratio)
        {
            ForEachFleet(selector, fleet =>
            {
                RepairFleet(fleet, ratio);
                InvokeNoArgs(fleet, "UpdateDv");
                AddLine("Repaired " + FleetSummary(fleet));
            });
        }

        private void RecoverFleetCommand(string selector)
        {
            ForEachFleet(selector, fleet =>
            {
                RepairFleet(fleet, 0.35f);
                RefuelFleet(fleet, 0.25f);
                InvokeNoArgs(fleet, "UpdateDv");
                SetNavigationInsufficientDv(fleet, false);
                AddLine("Recovered " + FleetSummary(fleet));
            });
        }

        private void ForEachFleet(string selector, Action<object> action)
        {
            UnityEngine.Object[] fleets = FindObjects("FleetManager");
            if (selector.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                foreach (object fleet in fleets)
                {
                    action(fleet);
                }

                return;
            }

            int index = ParseInt(selector);
            if (index < 0 || index >= fleets.Length)
            {
                AddLine("Fleet index out of range.");
                return;
            }

            action(fleets[index]);
        }

        private static void RefuelFleet(object fleet, float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            IEnumerable ships = GetField(fleet, "ships") as IEnumerable;
            if (ships == null)
            {
                return;
            }

            foreach (object ship in ships)
            {
                float missing = InvokeFloatValue(ship, "GetMissingFuel");
                float currentRatio = InvokeFloatValue(ship, "GetFuelRatio");
                if (missing > 0f && currentRatio < ratio)
                {
                    InvokeOneFloat(ship, "AddFuel", missing * Mathf.Clamp01(ratio - currentRatio));
                }
            }
        }

        private static void RepairFleet(object fleet, float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            IEnumerable ships = GetField(fleet, "ships") as IEnumerable;
            if (ships == null)
            {
                return;
            }

            foreach (object ship in ships)
            {
                RepairModule(GetField(ship, "fuel"), ratio);
                RepairModule(GetField(ship, "drive"), ratio);
                IEnumerable modules = GetField(ship, "modules") as IEnumerable;
                if (modules == null)
                {
                    continue;
                }

                foreach (object module in modules)
                {
                    RepairModule(module, ratio);
                }
            }
        }

        private static void RepairModule(object module, float ratio)
        {
            if (module == null)
            {
                return;
            }

            object healthValue = GetField(module, "health");
            if (!(healthValue is float health))
            {
                return;
            }

            float target = Mathf.Max(health, ratio);
            if (target > health)
            {
                SetField(module, "health", target);
            }
        }

        private static void SetNavigationInsufficientDv(object fleet, bool value)
        {
            object navigation = GetField(fleet, "navigation");
            SetField(navigation, "isInsufficientDv", value);
        }

        private object GetObjectByIndex(string typeName, int index)
        {
            UnityEngine.Object[] objects = FindObjects(typeName);
            if (index < 0 || index >= objects.Length)
            {
                AddLine(typeName + " index out of range.");
                return null;
            }

            return objects[index];
        }

        private static UnityEngine.Object[] FindObjects(string typeName)
        {
            Type type = AccessType(typeName);
            return type != null ? UnityEngine.Object.FindObjectsOfType(type) : new UnityEngine.Object[0];
        }

        private static FieldInfo GetFieldInfo(object target, string fieldName)
        {
            return target?.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }

        private static object ConvertString(string rawValue, Type targetType)
        {
            if (targetType == typeof(string))
            {
                return rawValue;
            }

            if (targetType == typeof(int))
            {
                return int.Parse(rawValue, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(float))
            {
                return float.Parse(rawValue, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(bool))
            {
                return bool.Parse(rawValue);
            }

            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, rawValue);
            }

            throw new ArgumentException("Unsupported field type: " + targetType.Name);
        }

        private static string DescribeObject(object target)
        {
            return target == null ? "null" : target.GetType().Name + " " + Convert.ToString(GetField(target, "name") ?? GetField(target, "publicName") ?? GetField(target, "id") ?? target);
        }

        private static string FleetSummary(object fleet)
        {
            object track = GetField(fleet, "track");
            return Convert.ToString(GetField(track, "publicName") ?? GetField(track, "trackName") ?? GetField(track, "id") ?? "fleet")
                + " state=" + FormatValue(GetField(fleet, "state"))
                + " dv=" + FormatFloat(GetField(fleet, "fleetDv")) + "/" + FormatFloat(GetField(fleet, "fleetDvMax"))
                + " limiter=" + FormatValue(GetField(fleet, "DvLimiter"));
        }

        private static string FormatValue(object value)
        {
            return value == null ? "null" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(object value)
        {
            return value is float f ? f.ToString("0.##", CultureInfo.InvariantCulture) : FormatValue(value);
        }

        private static string InvokeFloat(object target, string methodName)
        {
            return InvokeFloatValue(target, methodName).ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static float InvokeFloatValue(object target, string methodName)
        {
            MethodInfo method = target?.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            object value = method?.Invoke(target, null);
            return value is float f ? f : 0f;
        }

        private static void InvokeOneFloat(object target, string methodName, float value)
        {
            MethodInfo method = target?.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(float) }, null);
            method?.Invoke(target, new object[] { value });
        }

        private static void InvokeNoArgs(object target, string methodName)
        {
            MethodInfo method = target?.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            method?.Invoke(target, null);
        }

        private static int GetPlayerCredits()
        {
            object fm = FindCurrent("FactionsManager");
            object faction = GetField(fm, "playerFaction");
            object credits = GetField(faction, "credits");
            return credits is int value ? value : 0;
        }

        private static void SetPlayerCredits(int value)
        {
            object fm = FindCurrent("FactionsManager");
            object faction = GetField(fm, "playerFaction");
            SetField(faction, "credits", value);
        }

        private static void ModPlayerCredits(string header, int amount)
        {
            object fm = FindCurrent("FactionsManager");
            MethodInfo method = fm?.GetType().GetMethod("ModPlayerCredits", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(fm, new object[] { header, amount });
                return;
            }

            SetPlayerCredits(GetPlayerCredits() + amount);
        }

        private static string ReadTimeManager()
        {
            object tm = FindCurrent("TimeManager");
            if (tm == null)
            {
                return "TimeManager: unavailable";
            }

            object days = GetField(tm, "days");
            object hours = GetField(tm, "hours");
            object mins = GetField(tm, "mins");
            object scale = GetField(tm, "timeScale");
            return "Game time: day " + days + " " + hours + ":" + mins + " scale " + scale;
        }

        private static string EconomySnapshot()
        {
            object gm = FindCurrent("GlobalMarket");
            if (gm == null)
            {
                return "GlobalMarket unavailable. Load a strategic game first.";
            }

            ICollection markets = GetField(gm, "allMarkets") as ICollection;
            ICollection buyPrices = GetField(gm, "buyPrices") as ICollection;
            ICollection sellPrices = GetField(gm, "sellPrices") as ICollection;
            return "Economy: markets=" + (markets?.Count ?? 0) + " buyPrices=" + (buyPrices?.Count ?? 0) + " sellPrices=" + (sellPrices?.Count ?? 0);
        }

        private static object GetField(object target, string name)
        {
            return target?.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)?.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target?.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)?.SetValue(target, value);
        }

        public static string GetHotkeySummary()
        {
            string character = toggleCharacter != null ? toggleCharacter.Value : "";
            string key = toggleKey != null ? toggleKey.Value.ToString() : KeyCode.BackQuote.ToString();
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
            instance?.AddLine("Console hotkey changed to: " + GetHotkeySummary());
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
