# Basic BepInEx Mod Template

Copy this folder to the game root under:

```text
ModSource\YourModName
```

Then rename:

```text
BasicMod.csproj
Plugin.cs
namespace BasicMod
local.spacefleet.basic-mod
Basic Spacefleet Mod
```

Build:

```powershell
dotnet build .\ModSource\YourModName\YourModName.csproj -c Release
```

Install:

```powershell
Copy-Item .\ModSource\YourModName\bin\Release\YourModName.dll .\BepInEx\plugins\ -Force
```
