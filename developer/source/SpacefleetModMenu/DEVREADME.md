# Developer Guide: Making Mods for Spacefleet Mod Menu

This guide explains how to create a BepInEx mod for Spacefleet - Heat Death
that fully integrates with the Spacefleet Mod Menu system — including the shared
cyberpunk UI skin, hotkey auto-detection, window management, and the
enable/disable system.

## Table of Contents

1. [Quick Start](#quick-start)
2. [Project Setup](#project-setup)
3. [Basic Plugin Structure](#basic-plugin-structure)
4. [Hotkey API (Auto-Detection)](#hotkey-api-auto-detection)
5. [Consuming the Shared Cyberpunk Skin](#consuming-the-shared-cyberpunk-skin)
6. [Drawing Opaque IMGUI Windows](#drawing-opaque-imgui-windows)
7. [Minimize and Resize](#minimize-and-resize)
8. [Complete Example: Full ModMenu-Compatible Mod](#complete-example)
9. [Enable/Disable Compatibility](#enabledisable-compatibility)
10. [Window IDs and Depth](#window-ids-and-depth)
11. [Config Files](#config-files)
12. [ModMenu Public API Reference](#modmenu-public-api-reference)
13. [Architecture Overview](#architecture-overview)
14. [Tips and Best Practices](#tips-and-best-practices)

---

## Quick Start

The fastest way to create a compatible mod:

1. Copy the template from `developer/templates/BasicBepInExMod/`
2. Rename the project and update the `BepInPlugin` attribute
3. Add the hotkey API methods (`GetHotkeySummary`, `SetHotkey`)
4. Add the skin consumption code (`TryGetSharedSkin`)
5. Add background drawing in your `DrawWindow` method
6. Build, deploy to `BepInEx/plugins/`, and test

---

## Project Setup

### .csproj File

Create a `netstandard2.0` project with references to the game assemblies:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <AssemblyName>MyMod</AssemblyName>
    <RootNamespace>MyMod</RootNamespace>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <!-- BepInEx core -->
    <Reference Include="BepInEx">
      <HintPath>..\..\BepInEx\core\BepInEx.dll</HintPath>
      <Private>false</Private>
    </Reference>

    <!-- Only if you use Harmony patches -->
    <Reference Include="0Harmony">
      <HintPath>..\..\BepInEx\core\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>

    <!-- Unity core -->
    <Reference Include="UnityEngine">
      <HintPath>..\..\Spacefleet - Heat Death_Data\Managed\UnityEngine.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>..\..\Spacefleet - Heat Death_Data\Managed\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>

    <!-- For IMGUI windows -->
    <Reference Include="UnityEngine.IMGUIModule">
      <HintPath>..\..\Spacefleet - Heat Death_Data\Managed\UnityEngine.IMGUIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>

    <!-- For keyboard input -->
    <Reference Include="UnityEngine.InputLegacyModule">
      <HintPath>..\..\Spacefleet - Heat Death_Data\Managed\UnityEngine.InputLegacyModule.dll</HintPath>
      <Private>false</Private>
    </Reference>

    <!-- For rich text labels -->
    <Reference Include="UnityEngine.TextRenderingModule">
      <HintPath>..\..\Spacefleet - Heat Death_Data\Managed\UnityEngine.TextRenderingModule.dll</HintPath>
      <Private>false</Private>
    </Reference>

    <!-- For game types (only if patching game code) -->
    <Reference Include="Assembly-CSharp">
      <HintPath>..\..\Spacefleet - Heat Death_Data\Managed\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

**Important**: All `HintPath` values are relative to `ModSource/YourMod/`.
Place your source folder under `ModSource/` in the game root.

### Build

```powershell
dotnet build ModSource\MyMod\MyMod.csproj -c Release
```

Output: `ModSource/MyMod/bin/Release/MyMod.dll`

### Deploy

```powershell
Copy-Item ModSource\MyMod\bin\Release\MyMod.dll BepInEx\plugins\ -Force
```

---

## Basic Plugin Structure

```csharp
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace MyMod
{
    [BepInPlugin("local.spacefleet.my-mod", "My Spacefleet Mod", "0.1.0")]
    public sealed class MyModPlugin : BaseUnityPlugin
    {
        private static MyModPlugin instance;
        private static ConfigEntry<KeyCode> toggleKey;
        private static ConfigEntry<string> toggleCharacter;

        private bool isOpen;

        private void Awake()
        {
            instance = this;
            toggleKey = Config.Bind("Hotkeys", "ToggleKey", KeyCode.F9,
                "Key to open/close the mod window.");
            toggleCharacter = Config.Bind("Hotkeys", "ToggleCharacter", "",
                "Optional typed character for layout-specific keys.");
            Logger.LogInfo("My Mod loaded. Hotkey: " + GetHotkeySummary());
        }

        private void Update()
        {
            if (toggleKey.Value != KeyCode.None && Input.GetKeyDown(toggleKey.Value))
                isOpen = !isOpen;
        }
    }
}
```

---

## Hotkey API (Auto-Detection)

For the Mod Menu to discover your mod and show its hotkey in the editor, your
plugin class must expose two **public static** methods:

```csharp
/// <summary>
/// Returns a human-readable description of the current hotkey.
/// Called by ModMenu via reflection.
/// </summary>
public static string GetHotkeySummary()
{
    string ch = toggleCharacter?.Value ?? "";
    string k = toggleKey?.Value.ToString() ?? "None";
    return string.IsNullOrEmpty(ch) ? k : k + " / " + ch;
}

/// <summary>
/// Changes the hotkey at runtime. Called by ModMenu via reflection.
/// </summary>
public static void SetHotkey(string keyCodeName, string character)
{
    if (toggleKey == null || toggleCharacter == null) return;
    if (System.Enum.TryParse(keyCodeName, out KeyCode pk))
        toggleKey.Value = pk;
    toggleCharacter.Value = character ?? "";
    instance?.Config.Save();
}
```

**Requirements:**
- Both methods must be `public static`
- Both must exist on the same class that has `[BepInPlugin]`
- Method signatures must match exactly
- `GetHotkeySummary()` returns `string`, takes no parameters
- `SetHotkey(string, string)` returns `void`, takes `(string keyCodeName, string character)`

**How it works:** The Mod Menu scans `Chainloader.PluginInfos` every 5 seconds.
For each plugin, it checks if the plugin type has both methods via reflection.
If found, the mod appears in the hotkey editor with "Change" and "Clear" buttons.

---

## Consuming the Shared Cyberpunk Skin

The Mod Menu provides a shared `GUISkin` that any mod can use. The skin is
accessed via reflection — **no compile-time dependency on SpacefleetModMenu**.

### Step 1: Add Fields

```csharp
private static bool skinChecked;
private static GUISkin sharedSkin;
private static Texture2D bgTexture;
```

### Step 2: Add TryGetSharedSkin Method

```csharp
using System.Reflection;

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
                // Get the shared skin
                MethodInfo m = t.GetMethod("GetSharedSkin",
                    BindingFlags.Public | BindingFlags.Static);
                sharedSkin = m?.Invoke(null, null) as GUISkin;

                // Get the background texture (respects opacity setting)
                MethodInfo bgm = t.GetMethod("GetBgTexture",
                    BindingFlags.Public | BindingFlags.Static);
                bgTexture = bgm?.Invoke(null, null) as Texture2D;
            }
        }
        catch { }

        // Fallback: create our own dark background if ModMenu is not loaded
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
```

### Step 3: Apply in OnGUI

```csharp
private void OnGUI()
{
    if (!isOpen) return;

    GUISkin skin = TryGetSharedSkin();
    GUISkin prev = GUI.skin;
    if (skin != null) GUI.skin = skin;

    GUI.depth = -1005;  // Pick a unique depth
    windowRect = GUI.Window(42020, windowRect, DrawWindow, "My Mod Window");

    GUI.skin = prev;  // Always restore!
}
```

**Important:** Always save and restore `GUI.skin`. If you don't restore it,
you'll corrupt the skin for all subsequent IMGUI calls in that frame.

---

## Drawing Opaque IMGUI Windows

The most critical step for proper window rendering. Without explicit background
drawing, windows will appear transparent.

```csharp
private void DrawWindow(int id)
{
    // FIRST: Draw opaque background fill
    if (bgTexture != null)
        GUI.DrawTexture(new Rect(0, 0, windowRect.width, windowRect.height), bgTexture);

    // Then draw your content
    GUILayout.Label("Hello from My Mod!");

    // LAST: Enable window dragging
    GUI.DragWindow();
}
```

**Why is this needed?** Unity's `GUI.Window` chrome sometimes doesn't render
backgrounds correctly with custom skins, especially on `ScriptableObject`-based
`GUISkin` instances. Drawing an explicit texture as the first operation
guarantees the window has a visible background.

---

## Minimize and Resize

### Minimize

```csharp
private bool isMinimized;
private float normalHeight;

// In DrawWindow:
GUILayout.BeginHorizontal();
GUILayout.Label("My Mod Title");
GUILayout.FlexibleSpace();
if (GUILayout.Button(isMinimized ? "\u25a1" : "\u2014", GUILayout.Width(28f)))
{
    if (isMinimized)
    {
        windowRect.height = normalHeight;
        isMinimized = false;
    }
    else
    {
        normalHeight = windowRect.height;
        windowRect.height = 36f;
        isMinimized = true;
    }
}
if (GUILayout.Button("\u2715", GUILayout.Width(28f)))
{
    isOpen = false;
}
GUILayout.EndHorizontal();

if (isMinimized)
{
    GUI.DragWindow();
    return;  // Skip all content when minimized
}
```

### Resize

```csharp
private bool isResizing;
private Vector2 resizeOffset;
private static readonly Vector2 MinSize = new Vector2(400f, 200f);

// In OnGUI (before GUI.Window call):
if (isResizing)
{
    Event e = Event.current;
    if (e.type == EventType.MouseDrag)
    {
        windowRect.width = Mathf.Max(MinSize.x,
            e.mousePosition.x - windowRect.x + resizeOffset.x);
        windowRect.height = Mathf.Max(MinSize.y,
            e.mousePosition.y - windowRect.y + resizeOffset.y);
    }
    if (e.rawType == EventType.MouseUp)
        isResizing = false;
}

// At the END of DrawWindow (before GUI.DragWindow):
Rect handle = new Rect(windowRect.width - 18f, windowRect.height - 18f, 18f, 18f);
GUI.Label(handle, "\u25e2");
if (Event.current.type == EventType.MouseDown && handle.Contains(Event.current.mousePosition))
{
    isResizing = true;
    resizeOffset = new Vector2(windowRect.width, windowRect.height)
                   - Event.current.mousePosition;
    Event.current.Use();
    return;
}
GUI.DragWindow();
```

---

## Complete Example

Here's a full, working mod that integrates with the Mod Menu:

```csharp
using BepInEx;
using BepInEx.Configuration;
using System;
using System.Reflection;
using UnityEngine;

namespace MySpacefleetMod
{
    [BepInPlugin("local.spacefleet.my-mod", "My Spacefleet Mod", "0.1.0")]
    public sealed class MyModPlugin : BaseUnityPlugin
    {
        private static MyModPlugin instance;
        private static ConfigEntry<KeyCode> toggleKey;
        private static ConfigEntry<string> toggleCharacter;

        private Rect windowRect = new Rect(100f, 100f, 500f, 400f);
        private Vector2 scroll;
        private bool isOpen;
        private bool isMinimized;
        private float normalHeight;
        private bool isResizing;
        private Vector2 resizeOffset;
        private static readonly Vector2 MinSize = new Vector2(400f, 200f);

        private static bool skinChecked;
        private static GUISkin sharedSkin;
        private static Texture2D bgTexture;

        // ==================== Lifecycle ====================

        private void Awake()
        {
            instance = this;
            toggleKey = Config.Bind("Hotkeys", "ToggleKey", KeyCode.F9,
                "Key to open/close the mod window.");
            toggleCharacter = Config.Bind("Hotkeys", "ToggleCharacter", "",
                "Optional typed character for layout-specific keys.");
            Logger.LogInfo("My Mod loaded. Hotkey: " + GetHotkeySummary());
        }

        private void Update()
        {
            if (toggleKey.Value != KeyCode.None && Input.GetKeyDown(toggleKey.Value))
                isOpen = !isOpen;
        }

        private void OnGUI()
        {
            Event e = Event.current;

            // Handle character-based hotkey (for layout-specific keys like ¡ or º)
            if (e.type == EventType.KeyDown && MatchesCharacter(e))
            {
                isOpen = !isOpen;
                e.Use();
            }

            if (!isOpen) return;

            // Handle resize dragging
            if (isResizing)
            {
                if (e.type == EventType.MouseDrag)
                {
                    windowRect.width = Mathf.Max(MinSize.x,
                        e.mousePosition.x - windowRect.x + resizeOffset.x);
                    windowRect.height = Mathf.Max(MinSize.y,
                        e.mousePosition.y - windowRect.y + resizeOffset.y);
                }
                if (e.rawType == EventType.MouseUp)
                    isResizing = false;
            }

            // Apply shared skin
            GUISkin skin = TryGetSharedSkin();
            GUISkin prev = GUI.skin;
            if (skin != null) GUI.skin = skin;

            GUI.depth = -1005;  // Pick a unique depth for your mod
            windowRect = GUI.Window(42020, windowRect, DrawWindow,
                "My Spacefleet Mod");

            GUI.skin = prev;
        }

        // ==================== Window Drawing ====================

        private void DrawWindow(int id)
        {
            // Draw opaque background
            if (bgTexture != null)
                GUI.DrawTexture(new Rect(0, 0, windowRect.width,
                    windowRect.height), bgTexture);

            // Title bar with minimize and close
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b><color=#00FF88>MY MOD</color></b>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(isMinimized ? "\u25a1" : "\u2014",
                GUILayout.Width(28f)))
            {
                if (isMinimized)
                {
                    windowRect.height = normalHeight;
                    isMinimized = false;
                }
                else
                {
                    normalHeight = windowRect.height;
                    windowRect.height = 36f;
                    isMinimized = true;
                }
            }
            if (GUILayout.Button("\u2715", GUILayout.Width(28f)))
                isOpen = false;
            GUILayout.EndHorizontal();

            if (isMinimized)
            {
                GUI.DragWindow();
                return;
            }

            // Your content here
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("This is my custom mod content.");
            GUILayout.Label("It fully integrates with the Mod Menu!");
            GUILayout.Space(8f);

            if (GUILayout.Button("Do Something", GUILayout.Width(150f)))
            {
                Logger.LogInfo("Button pressed!");
            }

            GUILayout.EndScrollView();

            // Resize handle
            Rect handle = new Rect(windowRect.width - 18f,
                windowRect.height - 18f, 18f, 18f);
            GUI.Label(handle, "\u25e2");
            if (Event.current.type == EventType.MouseDown
                && handle.Contains(Event.current.mousePosition))
            {
                isResizing = true;
                resizeOffset = new Vector2(windowRect.width, windowRect.height)
                               - Event.current.mousePosition;
                Event.current.Use();
                return;
            }
            GUI.DragWindow();
        }

        // ==================== ModMenu Hotkey API ====================

        public static string GetHotkeySummary()
        {
            string ch = toggleCharacter?.Value ?? "";
            string k = toggleKey?.Value.ToString() ?? "F9";
            return string.IsNullOrEmpty(ch) ? k : k + " / " + ch;
        }

        public static void SetHotkey(string keyCodeName, string character)
        {
            if (toggleKey == null || toggleCharacter == null) return;
            if (Enum.TryParse(keyCodeName, out KeyCode pk))
                toggleKey.Value = pk;
            toggleCharacter.Value = character ?? "";
            instance?.Config.Save();
        }

        // ==================== Shared Skin ====================

        private static GUISkin TryGetSharedSkin()
        {
            if (!skinChecked)
            {
                skinChecked = true;
                try
                {
                    Type t = null;
                    foreach (Assembly asm in AppDomain.CurrentDomain
                        .GetAssemblies())
                    {
                        t = asm.GetType("SpacefleetModMenu.ModMenuPlugin");
                        if (t != null) break;
                    }
                    if (t != null)
                    {
                        MethodInfo m = t.GetMethod("GetSharedSkin",
                            BindingFlags.Public | BindingFlags.Static);
                        sharedSkin = m?.Invoke(null, null) as GUISkin;

                        MethodInfo bgm = t.GetMethod("GetBgTexture",
                            BindingFlags.Public | BindingFlags.Static);
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

        // ==================== Utility ====================

        private static bool MatchesCharacter(Event e)
        {
            string c = toggleCharacter?.Value ?? "";
            return !string.IsNullOrEmpty(c) && c.Length == 1
                && e.character == c[0]
                && (toggleKey == null || toggleKey.Value == KeyCode.None
                    || e.keyCode != toggleKey.Value);
        }
    }
}
```

---

## Enable/Disable Compatibility

The Mod Menu can disable mods by moving their DLL to `BepInEx/plugins/disabled/`.
Your mod doesn't need to do anything special for this — it works at the file
system level.

However, keep in mind:

- Your mod will not be loaded if its DLL is in the `disabled/` folder
- The change takes effect after game restart
- Players can re-enable from the ModMenu's "DISABLED MODS" section
- The ModMenu protects itself from being disabled

---

## Window IDs and Depth

Each IMGUI window needs a unique integer ID to avoid conflicts. The current
allocation:

```text
42010   ModMenu
42011   Console
42012   EconomyDebug
42013   BattleDebug Info
42014   BattleDebug Feed
```

**For new mods, use IDs starting from 42015.**

`GUI.depth` controls the rendering order (lower = on top):

```text
-1000   ModMenu
-1001   Console
-1002   EconomyDebug
-1003   BattleDebug
```

**For new mods, use depths starting from -1004 or lower.**

---

## Config Files

BepInEx automatically creates config files in `BepInEx/config/` based on your
plugin's GUID:

```text
BepInEx/config/local.spacefleet.my-mod.cfg
```

Use `ConfigEntry<T>` for typed values:

```csharp
var myOption = Config.Bind("Section", "Key", defaultValue, "Description");
```

The ModMenu's inline config editor can read and edit these files. No additional
integration is needed.

---

## ModMenu Public API Reference

All methods are `public static` on `SpacefleetModMenu.ModMenuPlugin`. Access
via reflection only — never add a compile-time reference to the ModMenu DLL.

| Method | Returns | Description |
|--------|---------|-------------|
| `GetSharedSkin()` | `GUISkin` | The full cyberpunk skin |
| `GetBgTexture()` | `Texture2D` | Background fill texture (respects opacity slider) |
| `GetRichLabel()` | `GUIStyle` | Rich text label, no word wrap, 13pt |
| `GetRichLabelWrap()` | `GUIStyle` | Rich text label, word wrap, 13pt |
| `GetHeaderStyle()` | `GUIStyle` | Bold, bright green (#00FF88), 14pt |
| `GetDimLabel()` | `GUIStyle` | Muted grey (#667766), 13pt |
| `GetHotkeySummary()` | `string` | ModMenu's own hotkey description |
| `SetHotkey(string, string)` | `void` | Change ModMenu's own hotkey |

### Skin Colors

| Color | Hex | Usage |
|-------|-----|-------|
| Background | `#0D0F14` | Window fill |
| Panel | `#121C1C` | Box/section fill |
| Border | `#00AA44` | Window borders, button borders |
| Border Dim | `#264030` | Box borders |
| Button Normal | `#0F1A14` | Button background |
| Button Hover | `#142A1E` | Button hover |
| Button Press | `#003319` | Button active |
| Text | `#B0C4B0` | Body text |
| Text Bright | `#00FF88` | Headers, active elements |
| Text Dim | `#667766` | Secondary info, GUIDs |
| Field | `#0A0D0F` | Text field background |
| Scroll Bg | `#0A0D0F` | Scrollbar track |
| Scroll Thumb | `#1A4030` | Scrollbar thumb |

---

## Architecture Overview

```text
ModMenu (central pillar)
  ├── Creates shared GUISkin + bgTexture
  ├── Exposes public static API methods
  ├── Auto-detects all plugins via Chainloader.PluginInfos
  ├── Manages enable/disable via filesystem
  └── Provides inline config editor

Other Mods (consumers)
  ├── Discover ModMenu via reflection (no compile-time dep)
  ├── Call GetSharedSkin() + GetBgTexture() once (cached)
  ├── Apply skin in OnGUI, restore after
  ├── Draw bgTexture in DrawWindow for opaque background
  ├── Expose GetHotkeySummary() + SetHotkey() for detection
  └── Fall back gracefully if ModMenu is not present
```

**Key design principle:** All integration is via reflection. Mods work
independently. ModMenu enhances them but is never required.

---

## Tips and Best Practices

1. **Always restore `GUI.skin`** after your window draw. Failing to restore
   corrupts rendering for all subsequent IMGUI calls.

2. **Cache reflection lookups.** Use `skinChecked` bool so you only search
   assemblies once, not every frame.

3. **Use `DontDestroyOnLoad`** on any `Texture2D` you create, so Unity doesn't
   garbage-collect it during scene transitions.

4. **Draw `GUI.DrawTexture` first** in `DrawWindow`. This is the only reliable
   way to guarantee an opaque window background.

5. **Pick unique window IDs** (42015+) and `GUI.depth` values (-1004 or lower)
   to avoid conflicts with existing mods.

6. **Use BepInEx `ConfigEntry<T>`** for settings. The ModMenu's config editor
   automatically finds and displays your config file.

7. **Test without ModMenu.** Your mod should work correctly even if the Mod
   Menu is not installed. The fallback texture ensures a dark background, and
   the default Unity skin is functional if not pretty.

8. **Don't reference ModMenu at compile time.** Always use reflection. This
   keeps mods independent and prevents load-order crashes.

9. **Use `Time.unscaledTime`** for timing that should work during pause or
   with modified `timeScale`.

10. **Log sparingly.** Use `Logger.LogInfo` for startup confirmation, but avoid
    per-frame logging in production builds.
