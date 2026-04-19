using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SpacefleetModMenu
{
    [BepInPlugin("local.spacefleet.mod-menu", "Spacefleet Mod Menu", "0.3.0")]
    public sealed class ModMenuPlugin : BaseUnityPlugin
    {
        private static ModMenuPlugin instance;
        private static ConfigEntry<KeyCode> toggleKey;
        private static ConfigEntry<string> toggleCharacter;
        private static ConfigEntry<int> cfgBgAlpha;

        private readonly List<HotkeyTarget> hotkeyTargets = new List<HotkeyTarget>();
        private readonly Dictionary<string, ConfigState> configStates = new Dictionary<string, ConfigState>();
        private readonly HashSet<string> pendingDisable = new HashSet<string>();
        private readonly List<DisabledMod> disabledMods = new List<DisabledMod>();
        private Rect windowRect = new Rect(60f, 80f, 740f, 660f);
        private Vector2 scroll;
        private bool isOpen;
        private bool isMinimized;
        private float normalHeight;
        private bool isResizing;
        private Vector2 resizeOffset;
        private Type mainMenuType;
        private float nextMainMenuCheckTime;
        private bool isMainMenuVisible;
        private HotkeyTarget captureTarget;
        private float lastDetectionTime;
        private float lastDisabledScanTime;

        private static readonly Vector2 MinSize = new Vector2(420f, 200f);

        #region Cyberpunk Skin

        private static GUISkin sharedSkin;
        private static bool skinCreated;
        private static GUIStyle skinRichLabel;
        private static GUIStyle skinRichLabelWrap;
        private static GUIStyle skinHeader;
        private static GUIStyle skinDimLabel;
        private static Texture2D bgTexture;

        private static readonly Color ColBg        = new Color(0.05f, 0.06f, 0.08f, 1f);
        private static readonly Color ColPanel     = new Color(0.07f, 0.08f, 0.11f, 1f);
        private static readonly Color ColBorder    = new Color(0f, 0.67f, 0.27f, 1f);
        private static readonly Color ColBorderDim = new Color(0.15f, 0.25f, 0.18f, 1f);
        private static readonly Color ColBtnNorm   = new Color(0.06f, 0.10f, 0.08f, 1f);
        private static readonly Color ColBtnHover  = new Color(0.08f, 0.16f, 0.12f, 1f);
        private static readonly Color ColBtnPress  = new Color(0f, 0.20f, 0.10f, 1f);
        private static readonly Color ColField     = new Color(0.04f, 0.05f, 0.06f, 1f);
        private static readonly Color ColFieldFoc  = new Color(0.05f, 0.06f, 0.08f, 1f);
        private static readonly Color ColToggleOn  = new Color(0f, 0.20f, 0.10f, 1f);
        private static readonly Color ColScrollBg  = new Color(0.04f, 0.05f, 0.06f, 1f);
        private static readonly Color ColScrollTh  = new Color(0.10f, 0.25f, 0.18f, 1f);
        private static readonly Color ColText      = new Color(0.69f, 0.77f, 0.69f, 1f);
        private static readonly Color ColTextBrt   = new Color(0f, 1f, 0.53f, 1f);
        private static readonly Color ColTextDim   = new Color(0.40f, 0.47f, 0.40f, 1f);

        public static GUISkin GetSharedSkin()
        {
            if (!skinCreated) CreateSkin();
            return sharedSkin;
        }

        public static GUIStyle GetRichLabel()
        {
            if (!skinCreated) CreateSkin();
            return skinRichLabel;
        }

        public static GUIStyle GetRichLabelWrap()
        {
            if (!skinCreated) CreateSkin();
            return skinRichLabelWrap;
        }

        public static GUIStyle GetHeaderStyle()
        {
            if (!skinCreated) CreateSkin();
            return skinHeader;
        }

        public static GUIStyle GetDimLabel()
        {
            if (!skinCreated) CreateSkin();
            return skinDimLabel;
        }

        public static Texture2D GetBgTexture()
        {
            if (!skinCreated) CreateSkin();
            return bgTexture;
        }

        private static void CreateSkin()
        {
            if (skinCreated) return;
            skinCreated = true;

            GUISkin def = GUI.skin;
            sharedSkin = UnityEngine.Object.Instantiate(def);
            UnityEngine.Object.DontDestroyOnLoad(sharedSkin);

            // Window – set every state so GUI.Window always has a bg texture
            Texture2D winBg = MakeBorderTex(16, 16, ColBg, ColBorder, 2);
            sharedSkin.window = new GUIStyle(def.window);
            sharedSkin.window.normal.background   = winBg;
            sharedSkin.window.normal.textColor     = ColTextBrt;
            sharedSkin.window.onNormal.background  = winBg;
            sharedSkin.window.onNormal.textColor   = ColTextBrt;
            sharedSkin.window.focused.background   = winBg;
            sharedSkin.window.focused.textColor    = ColTextBrt;
            sharedSkin.window.onFocused.background = winBg;
            sharedSkin.window.onFocused.textColor  = ColTextBrt;
            sharedSkin.window.hover.background     = winBg;
            sharedSkin.window.hover.textColor      = ColTextBrt;
            sharedSkin.window.onHover.background   = winBg;
            sharedSkin.window.onHover.textColor    = ColTextBrt;
            sharedSkin.window.active.background    = winBg;
            sharedSkin.window.active.textColor     = ColTextBrt;
            sharedSkin.window.onActive.background  = winBg;
            sharedSkin.window.onActive.textColor   = ColTextBrt;
            sharedSkin.window.border = new RectOffset(4, 4, 4, 4);
            sharedSkin.window.padding = new RectOffset(8, 8, 22, 8);
            sharedSkin.window.contentOffset = Vector2.zero;
            sharedSkin.window.fontSize = 13;
            sharedSkin.window.alignment = TextAnchor.UpperCenter;

            // Label
            sharedSkin.label = new GUIStyle(def.label);
            sharedSkin.label.normal.textColor = ColText;
            sharedSkin.label.fontSize = 13;
            sharedSkin.label.richText = true;
            sharedSkin.label.padding = new RectOffset(2, 2, 1, 1);

            // Box
            sharedSkin.box = new GUIStyle(def.box);
            sharedSkin.box.normal.background = MakeBorderTex(8, 8, ColPanel, ColBorderDim, 1);
            sharedSkin.box.normal.textColor = ColText;
            sharedSkin.box.border = new RectOffset(2, 2, 2, 2);
            sharedSkin.box.padding = new RectOffset(6, 6, 4, 4);
            sharedSkin.box.margin = new RectOffset(2, 2, 2, 2);

            // Button
            sharedSkin.button = new GUIStyle(def.button);
            sharedSkin.button.normal.background = MakeBorderTex(8, 8, ColBtnNorm, ColBorder, 1);
            sharedSkin.button.normal.textColor = ColTextBrt;
            sharedSkin.button.hover.background = MakeBorderTex(8, 8, ColBtnHover, ColTextBrt, 1);
            sharedSkin.button.hover.textColor = ColTextBrt;
            sharedSkin.button.active.background = MakeBorderTex(8, 8, ColBtnPress, ColTextBrt, 1);
            sharedSkin.button.active.textColor = Color.white;
            sharedSkin.button.border = new RectOffset(2, 2, 2, 2);
            sharedSkin.button.padding = new RectOffset(6, 6, 3, 3);
            sharedSkin.button.fontSize = 13;

            // TextField
            sharedSkin.textField = new GUIStyle(def.textField);
            sharedSkin.textField.normal.background = MakeBorderTex(8, 8, ColField, ColBorderDim, 1);
            sharedSkin.textField.normal.textColor = ColText;
            sharedSkin.textField.focused.background = MakeBorderTex(8, 8, ColFieldFoc, ColBorder, 1);
            sharedSkin.textField.focused.textColor = ColTextBrt;
            sharedSkin.textField.border = new RectOffset(2, 2, 2, 2);
            sharedSkin.textField.padding = new RectOffset(4, 4, 2, 2);
            sharedSkin.textField.fontSize = 13;

            // Toggle
            sharedSkin.toggle = new GUIStyle(def.toggle);
            sharedSkin.toggle.normal.textColor = ColText;
            sharedSkin.toggle.onNormal.textColor = ColTextBrt;
            sharedSkin.toggle.hover.textColor = ColTextBrt;
            sharedSkin.toggle.fontSize = 13;

            // ScrollView
            sharedSkin.scrollView = new GUIStyle(def.scrollView);
            sharedSkin.scrollView.normal.background = MakeTex(ColScrollBg);

            // Vertical Scrollbar
            sharedSkin.verticalScrollbar = new GUIStyle(def.verticalScrollbar);
            sharedSkin.verticalScrollbar.normal.background = MakeTex(ColScrollBg);
            sharedSkin.verticalScrollbarThumb = new GUIStyle(def.verticalScrollbarThumb);
            sharedSkin.verticalScrollbarThumb.normal.background = MakeTex(ColScrollTh);

            // Horizontal Scrollbar
            sharedSkin.horizontalScrollbar = new GUIStyle(def.horizontalScrollbar);
            sharedSkin.horizontalScrollbar.normal.background = MakeTex(ColScrollBg);
            sharedSkin.horizontalScrollbarThumb = new GUIStyle(def.horizontalScrollbarThumb);
            sharedSkin.horizontalScrollbarThumb.normal.background = MakeTex(ColScrollTh);

            // Custom styles
            skinRichLabel = new GUIStyle(sharedSkin.label)
            {
                richText = true,
                wordWrap = false,
                fontSize = 13,
                padding = new RectOffset(2, 2, 1, 1)
            };
            skinRichLabel.normal.textColor = ColText;

            skinRichLabelWrap = new GUIStyle(skinRichLabel) { wordWrap = true };

            skinHeader = new GUIStyle(skinRichLabel)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            skinHeader.normal.textColor = ColTextBrt;

            skinDimLabel = new GUIStyle(skinRichLabel);
            skinDimLabel.normal.textColor = ColTextDim;

            RebuildBgTexture();
        }

        private static void RebuildBgTexture()
        {
            if (bgTexture != null) UnityEngine.Object.Destroy(bgTexture);
            float a = cfgBgAlpha != null ? Mathf.Clamp(cfgBgAlpha.Value, 0, 100) / 100f : 0.95f;
            bgTexture = MakeTex(new Color(ColBg.r, ColBg.g, ColBg.b, a));
        }

        private static Texture2D MakeTex(Color fill)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { fill, fill, fill, fill });
            tex.Apply();
            UnityEngine.Object.DontDestroyOnLoad(tex);
            return tex;
        }

        private static Texture2D MakeBorderTex(int w, int h, Color fill, Color border, int thick)
        {
            var pix = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pix[y * w + x] = (x < thick || x >= w - thick || y < thick || y >= h - thick) ? border : fill;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels(pix);
            tex.Apply();
            UnityEngine.Object.DontDestroyOnLoad(tex);
            return tex;
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            instance = this;
            toggleKey = Config.Bind("Hotkeys", "ToggleKey", KeyCode.Quote, "Keyboard key that opens or closes the mod menu.");
            toggleCharacter = Config.Bind("Hotkeys", "ToggleCharacter", "", "Optional typed character that opens or closes the mod menu.");
            cfgBgAlpha = Config.Bind("UI", "BackgroundOpacity", 95, "Window background opacity (0 = fully transparent, 100 = fully opaque).");
            EnsureDisabledFolder();
            RefreshHotkeyTargets();
            Logger.LogInfo("Spacefleet Mod Menu loaded. Hotkey: " + GetHotkeySummary() + ".");
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
            if (e.type == EventType.KeyDown)
            {
                if (captureTarget != null)
                {
                    CaptureHotkey(e);
                    e.Use();
                    return;
                }

                if (MatchesCharacter(e))
                {
                    isOpen = !isOpen;
                    e.Use();
                }
            }

            GUISkin skin = GetSharedSkin();
            GUISkin prev = GUI.skin;
            if (skin != null) GUI.skin = skin;

            if (!isOpen && IsMainMenuVisible() && GUI.Button(GetMainMenuButtonRect(), "Mods"))
            {
                isOpen = true;
            }

            if (!isOpen)
            {
                GUI.skin = prev;
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

            GUI.depth = -1000;
            windowRect = GUI.Window(42010, windowRect, DrawWindow, "Spacefleet Mod Menu");

            GUI.skin = prev;
        }

        #endregion

        #region Draw Window

        private void DrawWindow(int id)
        {
            if (bgTexture != null)
                GUI.DrawTexture(new Rect(0, 0, windowRect.width, windowRect.height), bgTexture);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b><color=#00FF88>MOD MENU</color></b>  <color=#667766>(" + Chainloader.PluginInfos.Count + " plugins)</color>", skinRichLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(isMinimized ? "\u25a1" : "\u2014", GUILayout.Width(28f)))
            {
                ToggleMinimize();
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

            if (Time.unscaledTime > lastDetectionTime + 5f)
            {
                RefreshHotkeyTargets();
                lastDetectionTime = Time.unscaledTime;
            }

            if (Time.unscaledTime > lastDisabledScanTime + 5f)
            {
                ScanDisabledMods();
                lastDisabledScanTime = Time.unscaledTime;
            }

            scroll = GUILayout.BeginScrollView(scroll);

            // Active plugins
            GUILayout.Label("<color=#00FF88>ACTIVE PLUGINS</color>", skinHeader);
            GUILayout.Space(2f);

            foreach (var plugin in Chainloader.PluginInfos.Values.OrderBy(p => p.Metadata.Name, StringComparer.OrdinalIgnoreCase))
            {
                GUILayout.BeginVertical(GUI.skin.box);

                bool isPending = pendingDisable.Contains(plugin.Metadata.GUID);
                string nameColor = isPending ? "#FF6644" : "#00FF88";
                GUILayout.Label("<color=" + nameColor + "><b>" + plugin.Metadata.Name + "</b></color>  <color=#667766>v" + plugin.Metadata.Version + "</color>", skinRichLabel);
                GUILayout.Label("<color=#667766>GUID: " + plugin.Metadata.GUID + "  |  " + SafeFileName(plugin.Location) + "</color>", skinDimLabel);

                if (isPending)
                {
                    GUILayout.Label("<color=#FF6644>PENDING DISABLE — restart game to apply</color>", skinRichLabel);
                }

                GUILayout.BeginHorizontal();

                // Hotkey inline
                HotkeyTarget target = FindTarget(plugin.Instance?.GetType());
                if (target != null)
                {
                    GUILayout.Label("Hotkey: <color=#00FF88>" + target.GetSummary() + "</color>", skinRichLabel, GUILayout.Width(250f));
                    if (GUILayout.Button(captureTarget == target ? "Press key..." : "Change", GUILayout.Width(90f)))
                    {
                        captureTarget = target;
                    }
                    if (GUILayout.Button("Clear", GUILayout.Width(55f)))
                    {
                        target.Set(KeyCode.None.ToString(), "");
                    }
                }

                GUILayout.FlexibleSpace();

                // Enable/Disable
                bool isSelf = plugin.Metadata.GUID == "local.spacefleet.mod-menu";
                if (!isSelf && !isPending)
                {
                    if (GUILayout.Button("Disable", GUILayout.Width(70f)))
                    {
                        DisableMod(plugin);
                    }
                }
                else if (isPending)
                {
                    if (GUILayout.Button("Undo", GUILayout.Width(70f)))
                    {
                        UndoDisable(plugin);
                    }
                }
                GUILayout.EndHorizontal();

                // Config editor
                string cfgPath = GetConfigPath(plugin.Metadata.GUID);
                if (cfgPath != null)
                {
                    if (!configStates.TryGetValue(plugin.Metadata.GUID, out ConfigState cs))
                    {
                        cs = new ConfigState();
                        configStates[plugin.Metadata.GUID] = cs;
                    }

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(cs.IsExpanded ? "Hide Config" : "Show Config", GUILayout.Width(110f)))
                    {
                        cs.IsExpanded = !cs.IsExpanded;
                        if (cs.IsExpanded) cs.Load(cfgPath);
                    }
                    if (cs.IsExpanded && GUILayout.Button("Reload", GUILayout.Width(65f)))
                    {
                        cs.Load(cfgPath);
                    }
                    GUILayout.EndHorizontal();

                    if (cs.IsExpanded)
                    {
                        DrawConfigEntries(cs, cfgPath);
                    }
                }

                GUILayout.EndVertical();
            }

            // Disabled mods
            if (disabledMods.Count > 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("<color=#FF6644>DISABLED MODS</color>  <color=#667766>(restart to apply changes)</color>", skinHeader);
                GUILayout.Space(2f);

                foreach (var dm in disabledMods)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.Label("<color=#667766>" + dm.FileName + "</color>", skinRichLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Enable", GUILayout.Width(70f)))
                    {
                        EnableMod(dm);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label("<color=#667766>Tip: press a normal key for KeyCode hotkeys, or a typed character for layout-specific keys like \u00ba or \u00a1.</color>", skinDimLabel);
            GUILayout.EndScrollView();

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("BG Opacity:", skinDimLabel, GUILayout.Width(80f));
            float newAlpha = GUILayout.HorizontalSlider(cfgBgAlpha.Value, 0f, 100f, GUILayout.Width(180f));
            int rounded = Mathf.RoundToInt(newAlpha);
            if (rounded != cfgBgAlpha.Value)
            {
                cfgBgAlpha.Value = rounded;
                RebuildBgTexture();
            }
            GUILayout.Label(" " + cfgBgAlpha.Value + "%", skinDimLabel, GUILayout.Width(50f));
            GUILayout.EndHorizontal();

            DrawResizeHandle();
        }

        #endregion

        #region Enable / Disable Mods

        private struct DisabledMod
        {
            public string FileName;
            public string FullPath;
        }

        private static string DisabledFolderPath
        {
            get { return Path.Combine(Paths.PluginPath, "disabled"); }
        }

        private static void EnsureDisabledFolder()
        {
            string path = DisabledFolderPath;
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private void ScanDisabledMods()
        {
            disabledMods.Clear();
            string folder = DisabledFolderPath;
            if (!Directory.Exists(folder)) return;

            foreach (string file in Directory.GetFiles(folder, "*.dll"))
            {
                disabledMods.Add(new DisabledMod
                {
                    FileName = Path.GetFileName(file),
                    FullPath = file
                });
            }
        }

        private void DisableMod(BepInEx.PluginInfo plugin)
        {
            try
            {
                string src = plugin.Location;
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) return;
                string dst = Path.Combine(DisabledFolderPath, Path.GetFileName(src));
                File.Move(src, dst);
                pendingDisable.Add(plugin.Metadata.GUID);
                ScanDisabledMods();
                Logger.LogInfo("Disabled mod: " + plugin.Metadata.Name + " (restart to apply)");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to disable " + plugin.Metadata.Name + ": " + ex.Message);
            }
        }

        private void UndoDisable(BepInEx.PluginInfo plugin)
        {
            try
            {
                string fileName = Path.GetFileName(plugin.Location);
                string src = Path.Combine(DisabledFolderPath, fileName);
                if (File.Exists(src))
                {
                    File.Move(src, plugin.Location);
                }
                pendingDisable.Remove(plugin.Metadata.GUID);
                ScanDisabledMods();
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to undo disable: " + ex.Message);
            }
        }

        private void EnableMod(DisabledMod dm)
        {
            try
            {
                string dst = Path.Combine(Paths.PluginPath, dm.FileName);
                File.Move(dm.FullPath, dst);
                ScanDisabledMods();
                Logger.LogInfo("Enabled mod: " + dm.FileName + " (restart to apply)");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to enable " + dm.FileName + ": " + ex.Message);
            }
        }

        #endregion

        #region Hotkey Capture

        private void CaptureHotkey(Event e)
        {
            if (captureTarget == null) return;

            KeyCode key = e.keyCode == KeyCode.None ? KeyCode.None : e.keyCode;
            string character = "";
            if (!char.IsControl(e.character) && e.character != '\0')
            {
                character = e.character.ToString();
            }

            captureTarget.Set(key.ToString(), character);
            captureTarget = null;
        }

        #endregion

        #region Auto-Detection

        private void RefreshHotkeyTargets()
        {
            hotkeyTargets.Clear();
            hotkeyTargets.Add(new HotkeyTarget("Spacefleet Mod Menu", typeof(ModMenuPlugin)));

            foreach (var pluginInfo in Chainloader.PluginInfos.Values)
            {
                Type pluginType = pluginInfo.Instance?.GetType();
                if (pluginType == null || pluginType == typeof(ModMenuPlugin)) continue;

                MethodInfo getSummary = pluginType.GetMethod("GetHotkeySummary", BindingFlags.Public | BindingFlags.Static);
                MethodInfo setHotkey = pluginType.GetMethod("SetHotkey", BindingFlags.Public | BindingFlags.Static);

                if (getSummary != null && setHotkey != null)
                {
                    hotkeyTargets.Add(new HotkeyTarget(pluginInfo.Metadata.Name, pluginType));
                }
            }
        }

        private HotkeyTarget FindTarget(Type pluginType)
        {
            if (pluginType == null) return null;
            foreach (var target in hotkeyTargets)
                if (target.PluginType == pluginType) return target;
            return null;
        }

        #endregion

        #region Minimize / Resize

        private void ToggleMinimize()
        {
            if (isMinimized) { windowRect.height = normalHeight; isMinimized = false; }
            else { normalHeight = windowRect.height; windowRect.height = 36f; isMinimized = true; }
        }

        private void DrawResizeHandle()
        {
            Rect handle = new Rect(windowRect.width - 18f, windowRect.height - 18f, 18f, 18f);
            GUI.Label(handle, "<color=#335533>\u25e2</color>", skinRichLabel);
            if (Event.current.type == EventType.MouseDown && handle.Contains(Event.current.mousePosition))
            {
                isResizing = true;
                resizeOffset = new Vector2(windowRect.width, windowRect.height) - Event.current.mousePosition;
                Event.current.Use();
                return;
            }
            GUI.DragWindow();
        }

        #endregion

        #region Utility

        private static string SafeFileName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "(unknown)";
            try { return Path.GetFileName(path); } catch { return path; }
        }

        private bool IsMainMenuVisible()
        {
            if (Time.unscaledTime < nextMainMenuCheckTime) return isMainMenuVisible;
            nextMainMenuCheckTime = Time.unscaledTime + 0.5f;
            if (mainMenuType == null) mainMenuType = FindType("MainMenu");
            isMainMenuVisible = mainMenuType != null && UnityEngine.Object.FindObjectOfType(mainMenuType) != null;
            return isMainMenuVisible;
        }

        private static Rect GetMainMenuButtonRect()
        {
            return new Rect(Mathf.Max(18f, Screen.width - 116f), 18f, 96f, 34f);
        }

        private static Type FindType(string typeName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        public static string GetHotkeySummary()
        {
            string ch = toggleCharacter != null ? toggleCharacter.Value : "";
            string k = toggleKey != null ? toggleKey.Value.ToString() : KeyCode.Quote.ToString();
            return string.IsNullOrEmpty(ch) ? k : k + " / " + ch;
        }

        public static void SetHotkey(string keyCodeName, string character)
        {
            if (toggleKey == null || toggleCharacter == null) return;
            if (Enum.TryParse(keyCodeName, out KeyCode pk)) toggleKey.Value = pk;
            toggleCharacter.Value = character ?? "";
            instance?.Config.Save();
        }

        private static bool MatchesCharacter(Event e)
        {
            string c = toggleCharacter != null ? toggleCharacter.Value : "";
            return !string.IsNullOrEmpty(c) && c.Length == 1 && e.character == c[0]
                && (toggleKey == null || toggleKey.Value == KeyCode.None || e.keyCode != toggleKey.Value);
        }

        private static string GetConfigPath(string guid)
        {
            string cfgFile = Path.Combine(Paths.ConfigPath, guid + ".cfg");
            return File.Exists(cfgFile) ? cfgFile : null;
        }

        #endregion

        #region Config Editor

        private void DrawConfigEntries(ConfigState cs, string cfgPath)
        {
            for (int i = 0; i < cs.Keys.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(cs.Keys[i], GUILayout.Width(280f));
                cs.Values[i] = GUILayout.TextField(cs.Values[i], GUILayout.Width(200f));
                GUILayout.Label("<color=#667766>" + cs.Defaults[i] + "</color>", skinDimLabel, GUILayout.Width(140f));
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Config", GUILayout.Width(110f)))
            {
                cs.Save(cfgPath);
            }
            if (cs.StatusMessage != null)
            {
                GUILayout.Label("<color=#FFCC44>" + cs.StatusMessage + "</color>", skinRichLabel);
            }
            GUILayout.EndHorizontal();
        }

        #endregion

        #region Inner Classes

        private sealed class ConfigState
        {
            public bool IsExpanded;
            public readonly List<string> Keys = new List<string>();
            public readonly List<string> Values = new List<string>();
            public readonly List<string> Defaults = new List<string>();
            public string StatusMessage;
            private readonly List<string> rawLines = new List<string>();
            private readonly List<int> valueLineIndices = new List<int>();

            public void Load(string path)
            {
                Keys.Clear(); Values.Clear(); Defaults.Clear();
                rawLines.Clear(); valueLineIndices.Clear();
                StatusMessage = null;

                try
                {
                    string[] lines = File.ReadAllLines(path);
                    rawLines.AddRange(lines);
                    string section = "";
                    string lastDef = "";

                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            section = line.Substring(1, line.Length - 2);
                            lastDef = "";
                            continue;
                        }
                        if (line.StartsWith("# Default value:"))
                        {
                            lastDef = "default: " + line.Substring(16).Trim();
                            continue;
                        }
                        int eq = line.IndexOf('=');
                        if (eq > 0 && !line.StartsWith("#") && !line.StartsWith(";"))
                        {
                            Keys.Add(section + "." + line.Substring(0, eq).Trim());
                            Values.Add(line.Substring(eq + 1).Trim());
                            Defaults.Add(lastDef);
                            valueLineIndices.Add(i);
                            lastDef = "";
                        }
                    }
                }
                catch (Exception ex) { StatusMessage = "Load error: " + ex.Message; }
            }

            public void Save(string path)
            {
                try
                {
                    for (int i = 0; i < valueLineIndices.Count && i < Values.Count; i++)
                    {
                        int idx = valueLineIndices[i];
                        string orig = rawLines[idx];
                        int eq = orig.IndexOf('=');
                        if (eq > 0) rawLines[idx] = orig.Substring(0, eq + 1) + " " + Values[i];
                    }
                    File.WriteAllLines(path, rawLines.ToArray());
                    StatusMessage = "Saved. Restart game to apply.";
                }
                catch (Exception ex) { StatusMessage = "Save error: " + ex.Message; }
            }
        }

        private sealed class HotkeyTarget
        {
            public HotkeyTarget(string displayName, Type pluginType)
            {
                DisplayName = displayName;
                PluginType = pluginType;
            }

            public string DisplayName { get; }
            public Type PluginType { get; }

            public string GetSummary()
            {
                MethodInfo m = PluginType.GetMethod("GetHotkeySummary", BindingFlags.Public | BindingFlags.Static);
                return m?.Invoke(null, null) as string ?? "(unavailable)";
            }

            public void Set(string keyCodeName, string character)
            {
                MethodInfo m = PluginType.GetMethod("SetHotkey", BindingFlags.Public | BindingFlags.Static);
                m?.Invoke(null, new object[] { keyCodeName, character });
            }
        }

        #endregion
    }
}
