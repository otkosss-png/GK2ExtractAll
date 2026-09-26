# GK2 Extract All — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Кнопка «Извлечь всё» в окне вскрытия Graveyard Keeper 2 — последовательно извлекает все органы и предметы из карманов трупа.

**Architecture:** BepInEx-плагин + Harmony-патч `UIAutopsyWindow.Redraw()`, который добавляет кнопку; клик запускает корутину, которая по одному шагу вызывает штатные `UIAutopsyWindowData.TryExtractMainOrgan` / `TryExtractItemFromPocket`. Чистая логика очереди вынесена в `GK2ExtractAll.Core` и покрыта тестами.

**Tech Stack:** C# / .NET (netstandard2.0 ядро, netstandard2.1 плагин, net8.0 тесты xUnit), BepInEx 5.4.23.5, 0Harmony, GK2 Mod Framework, Unity 6 (`Assembly-CSharp.dll` игры), TMP.

**Spec:** `docs/superpowers/specs/2026-09-26-gk2-extract-all-design.md`

## Global Constraints

- SDK: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe"`.
- Игра: `E:\SteamLibrary\steamapps\common\Graveyard Keeper 2` (далее `<GAME>`).
- Сборка: из корня репо `& "<dotnet>" build -c Release`. Тесты: `& "<dotnet>" test -c Release`.
- Деплой для теста: закрыть `GraveyardKeeper2` → скопировать DLL в `<GAME>\BepInEx\plugins\GK2ExtractAll\GK2ExtractAll.dll` → удалить `<GAME>\BepInEx\cache` → `Start-Process "steam://rungameid/4358690"`.
- НИКОГДА не писать кириллицу через PowerShell (портится в ANSI) — только инструменты write/edit.
- Коммиты: `git -c user.name="otkosss-png" -c user.email="otkosss-png@users.noreply.github.com"`.
- Core — чистый netstandard2.0 без ссылок на Unity/BepInEx.
- Публичный API Core неизменен между задачами (имена ниже — контракт).
- Описание Workshop < 8000 байт; Steam допускает 2 изображения на айтем (превью + 1 скриншот).

---

### Task 1: Ядро очереди + тексты (TDD)

**Files:**
- Create: `src/GK2ExtractAll.Core/GK2ExtractAll.Core.csproj`
- Create: `src/GK2ExtractAll.Core/CellRef.cs`
- Create: `src/GK2ExtractAll.Core/ExtractQueue.cs`
- Create: `src/GK2ExtractAll.Core/ExtractText.cs`
- Create: `tests/GK2ExtractAll.Core.Tests/GK2ExtractAll.Core.Tests.csproj`
- Create: `tests/GK2ExtractAll.Core.Tests/QueueTests.cs`
- Create: `tests/GK2ExtractAll.Core.Tests/TextTests.cs`
- Create: `GK2ExtractAll.sln`

**Interfaces:**
- Produces: `GK2ExtractAll.Core.CellKind { Organ, Pocket }`, `CellRef(string Id, CellKind Kind)`, `ExtractQueue` (Begin/TakeNext/MarkAttempted/RecordExtracted/RecordSkipped/ShouldStop, свойства Planned/Extracted/Skipped/Steps/MaxSteps), `Lang { En, Ru }`, `ExtractText.Button(lang)`, `ExtractText.Summary(lang, extracted, total)`, `ExtractText.Nothing(lang)`.

- [ ] **Step 1: Create the Core project**

`src/GK2ExtractAll.Core/GK2ExtractAll.Core.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <AssemblyName>GK2ExtractAll.Core</AssemblyName>
    <RootNamespace>GK2ExtractAll.Core</RootNamespace>
    <Version>1.0.0</Version>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create the test project**

`tests/GK2ExtractAll.Core.Tests/GK2ExtractAll.Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\GK2ExtractAll.Core\GK2ExtractAll.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Write the failing tests**

`tests/GK2ExtractAll.Core.Tests/QueueTests.cs`:
```csharp
using System.Collections.Generic;
using GK2ExtractAll.Core;
using Xunit;

public class QueueTests
{
    private static List<CellRef> Cells(params string[] ids)
    {
        var list = new List<CellRef>();
        foreach (var id in ids) list.Add(new CellRef(id, CellKind.Organ));
        return list;
    }

    [Fact]
    public void Takes_present_cells_in_order()
    {
        var q = new ExtractQueue();
        q.Begin(3);
        var cells = Cells("organ:0", "organ:1", "organ:2");
        Assert.Equal("organ:0", q.TakeNext(cells).Id);
        Assert.Equal("organ:1", q.TakeNext(cells).Id);
    }

    [Fact]
    public void Skipped_cell_is_never_taken_again()
    {
        var q = new ExtractQueue();
        q.Begin(2);
        var cells = Cells("organ:0", "organ:1");
        var first = q.TakeNext(cells);
        q.RecordSkipped(first.Id);
        Assert.Equal("organ:1", q.TakeNext(cells).Id);
        Assert.Equal(1, q.Skipped);
    }

    [Fact]
    public void Extracted_cell_is_not_retaken_when_still_present()
    {
        var q = new ExtractQueue();
        q.Begin(2);
        var cells = Cells("organ:0", "organ:1");
        var first = q.TakeNext(cells);
        q.RecordExtracted();
        q.MarkAttempted(first.Id);
        Assert.Equal("organ:1", q.TakeNext(cells).Id);
        Assert.Equal(1, q.Extracted);
    }

    [Fact]
    public void Returns_null_when_nothing_left()
    {
        var q = new ExtractQueue();
        q.Begin(1);
        Assert.Null(q.TakeNext(new List<CellRef>()));
    }

    [Fact]
    public void Stops_when_all_planned_are_done()
    {
        var q = new ExtractQueue();
        q.Begin(1);
        q.RecordExtracted();
        Assert.True(q.ShouldStop(1));
    }

    [Fact]
    public void Stops_on_empty_corpse()
    {
        var q = new ExtractQueue();
        q.Begin(3);
        Assert.True(q.ShouldStop(0));
    }

    [Fact]
    public void Stops_at_max_steps()
    {
        var q = new ExtractQueue();
        q.Begin(1000);
        q.MaxSteps = 2;
        q.RecordSkipped("a");
        q.RecordSkipped("b");
        Assert.True(q.ShouldStop(999));
    }
}
```

`tests/GK2ExtractAll.Core.Tests/TextTests.cs`:
```csharp
using GK2ExtractAll.Core;
using Xunit;

public class TextTests
{
    [Fact] public void Button_en() => Assert.Equal("Extract all", ExtractText.Button(Lang.En));
    [Fact] public void Summary_en() => Assert.Equal("Extracted 3 of 8", ExtractText.Summary(Lang.En, 3, 8));
    [Fact] public void Summary_ru() => Assert.Equal("Извлечено 3 из 8", ExtractText.Summary(Lang.Ru, 3, 8));
    [Fact] public void Nothing_ru() => Assert.Equal("Нечего извлекать", ExtractText.Nothing(Lang.Ru));
}
```

- [ ] **Step 4: Create the solution and add both projects**

Run:
```powershell
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" new sln -n GK2ExtractAll
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" sln GK2ExtractAll.sln add src\GK2ExtractAll.Core\GK2ExtractAll.Core.csproj
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" sln GK2ExtractAll.sln add tests\GK2ExtractAll.Core.Tests\GK2ExtractAll.Core.Tests.csproj
```
Expected: «Проект добавлен в решение».

- [ ] **Step 5: Run tests to verify they fail**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: FAIL — ошибки компиляции `CS0246` (CellRef/ExtractQueue/ExtractText не найдены).

- [ ] **Step 6: Implement the Core types**

`src/GK2ExtractAll.Core/CellRef.cs`:
```csharp
namespace GK2ExtractAll.Core
{
    public enum CellKind { Organ, Pocket }

    public sealed class CellRef
    {
        public readonly string Id;
        public readonly CellKind Kind;

        public CellRef(string id, CellKind kind)
        {
            Id = id;
            Kind = kind;
        }
    }
}
```

`src/GK2ExtractAll.Core/ExtractQueue.cs`:
```csharp
using System.Collections.Generic;

namespace GK2ExtractAll.Core
{
    // Чистая логика прохода по ячейкам: решает, что извлекать следующим,
    // и считает итог. Не знает про Unity/игру.
    public sealed class ExtractQueue
    {
        private readonly HashSet<string> _attempted = new HashSet<string>();

        public int Planned { get; private set; }
        public int Extracted { get; private set; }
        public int Skipped { get; private set; }
        public int Steps { get; private set; }
        public int MaxSteps { get; set; } = 200;

        public void Begin(int itemCount)
        {
            _attempted.Clear();
            Planned = itemCount;
            Extracted = 0;
            Skipped = 0;
            Steps = 0;
        }

        // Возвращает первую ещё не обработанную ячейку и сразу помечает её
        // обработанной (за один запуск каждая ячейка берётся не более одного раза).
        public CellRef TakeNext(IEnumerable<CellRef> present)
        {
            if (present == null) return null;
            foreach (var c in present)
                if (c != null && !string.IsNullOrEmpty(c.Id) && !_attempted.Contains(c.Id))
                {
                    _attempted.Add(c.Id);
                    return c;
                }
            return null;
        }

        public void MarkAttempted(string id)
        {
            if (!string.IsNullOrEmpty(id)) _attempted.Add(id);
        }

        public void RecordExtracted()
        {
            Steps++;
            Extracted++;
        }

        public void RecordSkipped(string id)
        {
            Steps++;
            Skipped++;
            MarkAttempted(id);
        }

        public bool ShouldStop(int currentItemCount)
            => Steps >= MaxSteps
               || currentItemCount <= 0
               || Extracted + Skipped >= Planned;
    }
}
```

`src/GK2ExtractAll.Core/ExtractText.cs`:
```csharp
namespace GK2ExtractAll.Core
{
    public enum Lang { En, Ru }

    public static class ExtractText
    {
        public static string Button(Lang lang)
            => lang == Lang.Ru ? "Извлечь всё" : "Extract all";

        public static string Summary(Lang lang, int extracted, int total)
            => lang == Lang.Ru
                ? "Извлечено " + extracted + " из " + total
                : "Extracted " + extracted + " of " + total;

        public static string Nothing(Lang lang)
            => lang == Lang.Ru ? "Нечего извлекать" : "Nothing to extract";
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" test -c Release`
Expected: PASS — пройдено 11, не пройдено 0.

- [ ] **Step 8: Commit**

```bash
git add GK2ExtractAll.sln src tests
git -c user.name="otkosss-png" -c user.email="otkosss-png@users.noreply.github.com" commit -m "feat(core): extract queue + texts with tests"
```

---

### Task 2: Плагин-каркас: загружается в игре

**Files:**
- Create: `src/GK2ExtractAll/GK2ExtractAll.csproj`
- Create: `src/GK2ExtractAll/Plugin.cs`
- Create: `src/GK2ExtractAll/Mod.cs`
- Create: `.gitignore`
- Create: `tools/deploy.ps1`

**Interfaces:**
- Consumes: `GK2ExtractAll.Core` (линкуется исходниками).
- Produces: `GK2ExtractAll.Plugin` (Guid `otkosss.gk2.extractall`, `Plugin.Log`, `Plugin.Mod`), `GK2ExtractAll.Mod : Gk2ModBase` с `ConfigEntry<int> DelayMs`, `ConfigEntry<bool> ButtonEnabled`, `ConfigEntry<string> Language`; `ExtractText.Lang Plugin.Lang` (свойство).

- [ ] **Step 1: Create the plugin project**

`src/GK2ExtractAll/GK2ExtractAll.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <AssemblyName>GK2ExtractAll</AssemblyName>
    <RootNamespace>GK2ExtractAll</RootNamespace>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <Version>1.0.0</Version>
    <GameDir Condition="'$(GameDir)' == ''">E:\SteamLibrary\steamapps\common\Graveyard Keeper 2</GameDir>
    <ManagedDir>$(GameDir)\GraveyardKeeper2_Data\Managed</ManagedDir>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="..\GK2ExtractAll.Core\*.cs" LinkBase="Core" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="BepInEx"><HintPath>$(GameDir)\BepInEx\core\BepInEx.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="0Harmony"><HintPath>$(GameDir)\BepInEx\core\0Harmony.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="GK2.Framework"><HintPath>$(GameDir)\BepInEx\plugins\GK2.Framework.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Assembly-CSharp"><HintPath>$(ManagedDir)\Assembly-CSharp.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="LazyBearTechnology"><HintPath>$(ManagedDir)\LazyBearTechnology.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine"><HintPath>$(ManagedDir)\UnityEngine.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>$(ManagedDir)\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.UI"><HintPath>$(ManagedDir)\UnityEngine.UI.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.UIModule"><HintPath>$(ManagedDir)\UnityEngine.UIModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.InputLegacyModule"><HintPath>$(ManagedDir)\UnityEngine.InputLegacyModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.TextRenderingModule"><HintPath>$(ManagedDir)\UnityEngine.TextRenderingModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Unity.TextMeshPro"><HintPath>$(ManagedDir)\Unity.TextMeshPro.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add the plugin project to the solution**

Run:
```powershell
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" sln GK2ExtractAll.sln add src\GK2ExtractAll\GK2ExtractAll.csproj
```
Expected: «Проект добавлен в решение».

- [ ] **Step 3: Implement Mod (settings registration)**

`src/GK2ExtractAll/Mod.cs`:
```csharp
using BepInEx.Configuration;
using GK2.Framework;

namespace GK2ExtractAll
{
    // Настройки появляются в игровом меню Mods (GK2 Mod Framework).
    internal sealed class Mod : Gk2ModBase
    {
        private readonly Gk2ModMetadata _metadata = new Gk2ModMetadata(
            "otkosss.gk2.extractall",
            "GK2 Extract All",
            "otkosss",
            "1.0.0",
            "Adds an 'Extract all' button to the autopsy window: pulls every organ and pocket item out of a corpse.",
            false,
            false);

        internal ConfigEntry<string> Language;
        internal ConfigEntry<bool> ButtonEnabled;
        internal ConfigEntry<int> DelayMs;

        public override Gk2ModMetadata Metadata => _metadata;

        public override void OnRegister(Gk2ModContext context)
        {
            var s = context.Settings;
            Language = s.AddDropdown("General", "Language", "en", new[] { "auto", "en", "ru" },
                "Language / Язык", "en, ru, auto (system)", 10);
            ButtonEnabled = s.AddToggle("General", "Enabled", true,
                "Кнопка «Извлечь всё»", "Показывать кнопку в окне вскрытия", 20);
            DelayMs = s.AddIntSlider("General", "DelayMs", 500, 100, 2000,
                "Задержка между извлечениями (мс)", "Как быстро идут шаги", 5, 30);
        }
    }
}
```

- [ ] **Step 4: Implement Plugin**

`src/GK2ExtractAll/Plugin.cs`:
```csharp
using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GK2ExtractAll.Core;

namespace GK2ExtractAll
{
    [BepInDependency("ru.superman4eg.gk2.framework")]
    [BepInPlugin(Guid, "GK2 Extract All", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "otkosss.gk2.extractall";
        public static ManualLogSource Log;
        internal static Mod Mod;
        internal static Lang Lang = Lang.En;

        private void Awake()
        {
            Log = Logger;

            Mod = new Mod();
            try
            {
                GK2.Framework.FrameworkApi.RegisterMod(Mod, Config);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("GK2 Framework register failed, using local config: " + ex.Message);
            }
            EnsureSettings();

            Lang = ResolveLanguage(Mod.Language.Value);

            try { new HarmonyLib.Harmony(Guid).PatchAll(typeof(Plugin).Assembly); }
            catch (Exception ex) { Logger.LogWarning("harmony patch failed: " + ex.Message); }

            Logger.LogInfo("GK2 Extract All " + Version + " loaded.");
        }

        private void EnsureSettings()
        {
            if (Mod.DelayMs != null) return;
            Mod.Language = Config.Bind("General", "Language", "en", "auto | en | ru");
            Mod.ButtonEnabled = Config.Bind("General", "Enabled", true, "Show the 'Extract all' button");
            Mod.DelayMs = Config.Bind("General", "DelayMs", 500, "Delay between extractions (ms)");
        }

        internal static Lang ResolveLanguage(string value)
        {
            if (string.Equals(value, "en", StringComparison.OrdinalIgnoreCase)) return Lang.En;
            if (string.Equals(value, "ru", StringComparison.OrdinalIgnoreCase)) return Lang.Ru;
            var two = System.Globalization.CultureInfo.CurrentUICulture?.TwoLetterISOLanguageName;
            return string.Equals(two, "ru", StringComparison.OrdinalIgnoreCase) ? Lang.Ru : Lang.En;
        }

        internal static string Version => typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }
}
```

- [ ] **Step 5: Add .gitignore**

`.gitignore`:
```
bin/
obj/
dist/
*.user
```

- [ ] **Step 6: Add the deploy script**

`tools/deploy.ps1`:
```powershell
param([string]$GameDir = "E:\SteamLibrary\steamapps\common\Graveyard Keeper 2")
$dll = Join-Path $PSScriptRoot "..\src\GK2ExtractAll\bin\Release\GK2ExtractAll.dll"
$dst = Join-Path $GameDir "BepInEx\plugins\GK2ExtractAll"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Get-Process GraveyardKeeper2 -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3
Copy-Item $dll (Join-Path $dst "GK2ExtractAll.dll") -Force
Remove-Item (Join-Path $GameDir "BepInEx\cache") -Recurse -Force -ErrorAction SilentlyContinue
Start-Process "steam://rungameid/4358690"
Write-Output "deployed"
```

- [ ] **Step 7: Build**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release`
Expected: «Сборка успешно завершена», 0 ошибок; `src\GK2ExtractAll\bin\Release\GK2ExtractAll.dll` создан.

- [ ] **Step 8: Deploy and verify it loads**

Run:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\deploy.ps1
```
Expected (через ~40 c): в `<GAME>\BepInEx\LogOutput.log` есть строки `Loading [GK2 Extract All 1.0.0]` и `GK2 Extract All 1.0.0.0 loaded.`

- [ ] **Step 9: Commit**

```bash
git add .gitignore src tools GK2ExtractAll.sln
git -c user.name="otkosss-png" -c user.email="otkosss-png@users.noreply.github.com" commit -m "feat(plugin): bootstrap BepInEx plugin with GK2 Framework settings"
```

---

### Task 3: Кнопка в окне вскрытия

**Files:**
- Create: `src/GK2ExtractAll/UiFactory.cs`
- Create: `src/GK2ExtractAll/GameStyle.cs`
- Create: `src/GK2ExtractAll/AutopsyPatch.cs`
- Create: `src/GK2ExtractAll/ExtractAllButton.cs`

**Interfaces:**
- Consumes: `Plugin.Log`, `Plugin.Mod.ButtonEnabled`, `Plugin.Lang`, `ExtractText.Button(lang)`.
- Produces: `ExtractAllButton.Ensure(UIAutopsyWindow window)`, `ExtractAllButton.Clear()`, `ExtractAllButton.SetResult(string text)`.

- [ ] **Step 1: Copy the UI helpers from GK2ZombieHQ**

Скопировать `C:\Users\Проньки\Documents\OpenCode\GK2ZombieHQ\src\GK2ZombieHQ\UiFactory.cs` и
`...\GameStyle.cs` в `src/GK2ExtractAll/`, затем в обоих файлах заменить `namespace GK2ZombieHQ`
на `namespace GK2ExtractAll`, а в `GameStyle.cs` удалить всё, что относится к зомби/черепам
(поля `_zombieIcon`, `_whiteSkull`, `_redSkull`, `_spriteAsset`, свойства `ZombieIcon`,
`WhiteSkull`, `RedSkull`, `SpriteAsset`, методы `RefreshButtonSprite`, `IsBadButtonSprite`) —
оставить `Text/Accent/Danger/PanelBg/ButtonBg/Dim`, `Font`, `FontMaterial`, `ButtonSprite`,
`PanelSprite`, `Ensure`, `TryGraphic`. В `RefreshButtonSprite` заменить `Plugin.Log.LogInfo` на
`Plugin.Log.LogInfo` (без изменений), вызовы `GameStyle.RefreshButtonSprite()` — из
`ExtractAllButton.Ensure`.

- [ ] **Step 2: Write the Harmony patch**

`src/GK2ExtractAll/AutopsyPatch.cs`:
```csharp
using HarmonyLib;

namespace GK2ExtractAll
{
    [HarmonyPatch(typeof(UIAutopsyWindow), "Redraw")]
    internal static class AutopsyWindowRedrawPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIAutopsyWindow __instance)
        {
            ExtractAllButton.Ensure(__instance);
        }
    }

    [HarmonyPatch(typeof(UIAutopsyWindow), "Hide")]
    internal static class AutopsyWindowHidePatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIAutopsyWindow __instance)
        {
            ExtractAllButton.Clear();
        }
    }
}
```

- [ ] **Step 3: Implement the button**

`src/GK2ExtractAll/ExtractAllButton.cs`:
```csharp
using System.Collections.Generic;
using GK2ExtractAll.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2ExtractAll
{
    internal static class ExtractAllButton
    {
        private static readonly Dictionary<int, Button> _buttons = new Dictionary<int, Button>();
        private static readonly Dictionary<int, TextMeshProUGUI> _results = new Dictionary<int, TextMeshProUGUI>();

        internal static void Ensure(UIAutopsyWindow window)
        {
            if (window == null) return;
            if (!Plugin.Mod.ButtonEnabled.Value) return;

            int key = window.GetInstanceID();
            if (_buttons.TryGetValue(key, out var existing) && existing != null)
            {
                existing.interactable = !IsEmpty(window);
                existing.gameObject.SetActive(true);
                return;
            }

            var parent = window.transform as RectTransform;
            if (parent == null) return;

            var btn = UiFactory.TextButton("ExtractAllBtn", parent, ExtractText.Button(Plugin.Lang), 26);
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 40f);
            rt.sizeDelta = new Vector2(230f, 54f);
            btn.onClick.AddListener(() => ExtractRunner.Start(window));

            var label = UiFactory.Label("ResultLabel", parent, string.Empty, 24,
                TextAlignmentOptions.Center, Color.white);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0.5f, 0f);
            lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot = new Vector2(0.5f, 0f);
            lrt.anchoredPosition = new Vector2(0f, 100f);
            lrt.sizeDelta = new Vector2(500f, 34f);
            label.gameObject.SetActive(false);

            _buttons[key] = btn;
            _results[key] = label;
            Plugin.Log.LogInfo("autopsy: extract-all button added");
        }

        internal static void Clear()
        {
            // окно закрыто: сбрасываем кэш, чтобы при следующем открытии кнопка создалась заново
            _buttons.Clear();
            _results.Clear();
            ExtractRunner.StopIfRunning();
        }

        internal static void SetResult(UIAutopsyWindow window, string text)
        {
            if (window == null) return;
            if (!_results.TryGetValue(window.GetInstanceID(), out var label) || label == null) return;
            label.text = text;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private static bool IsEmpty(UIAutopsyWindow window)
        {
            try { return window.Data == null || window.Data.IsEmpty; }
            catch { return false; }
        }
    }
}
```

- [ ] **Step 4: Build and deploy**

Run (в корне репо):
```powershell
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools\deploy.ps1
```
Expected: сборка 0 ошибок; в логе `autopsy: extract-all button added` при открытии окна вскрытия (проверяется в игре).

- [ ] **Step 5: Verify in game (manual)**

Открыть труп на столе вскрытия. Expected: внизу окна появилась кнопка «Извлечь всё» (или «Extract all»). На пустом трупе кнопка неактивна.

- [ ] **Step 6: Commit**

```bash
git add src
git -c user.name="otkosss-png" -c user.email="otkosss-png@users.noreply.github.com" commit -m "feat(ui): add Extract all button to the autopsy window"
```

---

### Task 4: Раннер — последовательное извлечение

**Files:**
- Create: `src/GK2ExtractAll/ExtractRunner.cs`
- Modify: `src/GK2ExtractAll/ExtractAllButton.cs` (уже вызывает `ExtractRunner.Start(window)`)

**Interfaces:**
- Consumes: `ExtractQueue`, `CellRef`, `CellKind`, `ExtractText.Summary`, `ExtractAllButton.SetResult`.
- Produces: `ExtractRunner.Start(UIAutopsyWindow window)`, `ExtractRunner.StopIfRunning()`.

- [ ] **Step 1: Implement the runner**

`src/GK2ExtractAll/ExtractRunner.cs`:
```csharp
using System.Collections;
using System.Collections.Generic;
using GK2ExtractAll.Core;
using UnityEngine;

namespace GK2ExtractAll
{
    internal sealed class ExtractRunner : MonoBehaviour
    {
        private static ExtractRunner _instance;
        private UIAutopsyWindow _window;
        private Coroutine _co;

        internal static void Start(UIAutopsyWindow window)
        {
            if (window == null) return;
            if (_instance == null)
            {
                var go = new GameObject("GK2ExtractAllRunner");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ExtractRunner>();
            }
            _instance.Run(window);
        }

        internal static void StopIfRunning()
        {
            if (_instance != null && _instance._co != null) _instance.StopCoroutine(_instance._co);
            if (_instance != null) _instance._co = null;
        }

        private void Run(UIAutopsyWindow window)
        {
            StopIfRunning();
            _window = window;
            _co = StartCoroutine(Loop());
        }

        private IEnumerator Loop()
        {
            var queue = new ExtractQueue();
            queue.Begin(CountItems());
            float delay = Mathf.Clamp(Plugin.Mod.DelayMs.Value, 100, 2000) / 1000f;

            while (true)
            {
                int items = CountItems();
                if (queue.ShouldStop(items)) break;

                var next = queue.TakeNext(Collect());
                if (next == null) break;

                int before = items;
                Trigger(next);
                yield return new WaitForSeconds(delay);

                bool ok = CountItems() < before;
                if (ok) queue.RecordExtracted(); // TakeNext уже пометил ячейку обработанной
                else queue.RecordSkipped(next.Id);
            }

            string text = queue.Planned == 0
                ? ExtractText.Nothing(Plugin.Lang)
                : ExtractText.Summary(Plugin.Lang, queue.Extracted, queue.Planned);
            ExtractAllButton.SetResult(_window, text);
            Plugin.Log.LogInfo("extract-all: " + queue.Extracted + "/" + queue.Planned
                + " (skipped " + queue.Skipped + ", steps " + queue.Steps + ")");
        }

        private bool HasData()
        {
            try { return _window != null && _window.Data != null; }
            catch { return false; }
        }

        private int CountItems()
        {
            if (!HasData()) return 0;
            int n = 0;
            var organs = _window.bodyOrgansInventoryWidget;
            if (organs != null && organs.mainOrgansFixedTypeItemCells != null)
                foreach (var c in organs.mainOrgansFixedTypeItemCells)
                    if (IsFilled(c)) n++;
            var pockets = _window.bodyPocketInventoryWidget;
            if (pockets != null && pockets.cells != null)
                foreach (var c in pockets.cells)
                    if (IsFilled(c)) n++;
            return n;
        }

        private List<CellRef> Collect()
        {
            var list = new List<CellRef>();
            if (!HasData()) return list;
            var organs = _window.bodyOrgansInventoryWidget;
            if (organs != null && organs.mainOrgansFixedTypeItemCells != null)
                for (int i = 0; i < organs.mainOrgansFixedTypeItemCells.Count; i++)
                {
                    var c = organs.mainOrgansFixedTypeItemCells[i];
                    if (IsUsable(c)) list.Add(new CellRef("organ:" + i, CellKind.Organ));
                }
            var pockets = _window.bodyPocketInventoryWidget;
            if (pockets != null && pockets.cells != null)
                for (int i = 0; i < pockets.cells.Count; i++)
                {
                    var c = pockets.cells[i];
                    if (IsUsable(c)) list.Add(new CellRef("pocket:" + i, CellKind.Pocket));
                }
            return list;
        }

        private void Trigger(CellRef cell)
        {
            try
            {
                var data = _window.Data;
                if (data == null) return;
                var c = Resolve(cell.Id);
                if (c == null) return;
                if (cell.Kind == CellKind.Organ) data.TryExtractMainOrgan(c);
                else data.TryExtractItemFromPocket(c);
            }
            catch (System.Exception ex) { Plugin.Log.LogWarning("extract step: " + ex.Message); }
        }

        private UIItemCell Resolve(string id)
        {
            var parts = id.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int i)) return null;
            if (parts[0] == "organ")
            {
                var w = _window.bodyOrgansInventoryWidget;
                return w != null && w.mainOrgansFixedTypeItemCells != null
                       && i >= 0 && i < w.mainOrgansFixedTypeItemCells.Count
                    ? w.mainOrgansFixedTypeItemCells[i] : null;
            }
            var p = _window.bodyPocketInventoryWidget;
            return p != null && p.cells != null && i >= 0 && i < p.cells.Count ? p.cells[i] : null;
        }

        private static bool IsFilled(UIItemCell c) => c != null && c.DisplayingItem != null;

        private static bool IsUsable(UIItemCell c) => IsFilled(c) && c.IsInteractable;
    }
}
```

- [ ] **Step 2: Build**

Run: `& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release`
Expected: 0 ошибок. Если `mainOrgansFixedTypeItemCells`/`cells`/`Data`/`IsInteractable`/`DisplayingItem` не резолвятся — сверить имена по `Assembly-CSharp.dll` (рекон: `BodyOrgansInventoryWidget.mainOrgansFixedTypeItemCells`,
`BodyPocketInventoryWidget.cells`, `UIItemCell.DisplayingItem`, `UIItemCell.IsInteractable`,
`UIAutopsyWindowData.TryExtractMainOrgan/TryExtractItemFromPocket`).

- [ ] **Step 3: Deploy and verify in game (manual)**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\deploy.ps1`
Проверить на трупе с 2+ органами и предметом в кармане: все органы и предмет уходят в инвентарь по очереди; под кнопкой появляется «Извлечено N из M». Проверить труп без инструментов: шаги пропускаются, итог «Извлечено 0 из M».

- [ ] **Step 4: Commit**

```bash
git add src
git -c user.name="otkosss-png" -c user.email="otkosss-png@users.noreply.github.com" commit -m "feat: sequential extract-all runner (organs + pockets)"
```

---

### Task 5: Настройки в игре (задержка/вкл/язык) — проверка

**Files:**
- Modify: `src/GK2ExtractAll/Plugin.cs` (ничего нового, если Task 2 уже зарегистрировал опции)
- Modify: `src/GK2ExtractAll/ExtractAllButton.cs` (учитывать `ButtonEnabled` при открытом окне)

**Interfaces:**
- Consumes: `Mod.Language`, `Mod.ButtonEnabled`, `Mod.DelayMs`, `Plugin.ResolveLanguage`.
- Produces: ничего нового.

- [ ] **Step 1: Apply the toggle live**

В `ExtractAllButton.Ensure` перед ранним `return` для существующей кнопки добавить реакцию на выключение:
```csharp
            if (_buttons.TryGetValue(key, out var existing) && existing != null)
            {
                bool on = Plugin.Mod.ButtonEnabled.Value;
                existing.gameObject.SetActive(on);
                existing.interactable = on && !IsEmpty(window);
                if (!on) return;
                return;
            }
```
И в самом начале `Ensure` заменить блок
```csharp
            if (!Plugin.Mod.ButtonEnabled.Value) return;
```
оставить (кнопка не создаётся, если выключено).

- [ ] **Step 2: Build, deploy, verify settings in game**

Run:
```powershell
& "C:\Users\Проньки\dotnet-sdk\dotnet.exe" build -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools\deploy.ps1
```
Expected: в игровом меню **Mods → GK2 Extract All** есть Language / Enabled / DelayMs; смена DelayMs влияет на темп (проверить 1000 мс — заметно медленнее), выключение Enabled убирает кнопку; смена языка меняет надпись кнопки.

- [ ] **Step 3: Commit**

```bash
git add src
git -c user.name="otkosss-png" -c user.email="otkosss-png@users.noreply.github.com" commit -m "feat(settings): live toggle/language/delay for the extract-all button"
```

---

### Task 6: README, описание, публикация

**Files:**
- Create: `README.md`
- Create: `E:\GK2Upload\GK2ExtractAll_description.txt`
- Create: `E:\GK2Upload\GK2ExtractAll\BepInEx\plugins\GK2ExtractAll\GK2ExtractAll.dll` (копия сборки)
- Create: `E:\GK2Upload\GK2ExtractAll\BepInEx\plugins\GK2ExtractAll\README.txt`
- Create: `E:\GK2Upload\GK2ExtractAll_preview.png`, `E:\GK2Upload\GK2ExtractAll_screen.png`

- [ ] **Step 1: Write README.md**

```markdown
# GK2 Extract All

A BepInEx plugin for **Graveyard Keeper 2**: adds an **Extract all** button to the
autopsy window. One click pulls every organ and every item from the corpse's pockets,
one by one, using the game's own extraction flow.

## Features
- **Extract all** button in the autopsy window (organs + pockets).
- Sequential, like clicking each cell (no instant mass-calls).
- Failed extractions are skipped; a summary shows "Extracted N of M".
- Works with mouse and controller; settings in the in-game **Mods** menu.

## Requirements
- Graveyard Keeper 2 (Steam).
- BepInEx 5.4.23.5 x64 and **GK2 Mod Framework**
  (both from [GK2 Mod Installer](https://github.com/otkosss-png/GK2ModInstaller)).

## Install
Copy `BepInEx/plugins/GK2ExtractAll/GK2ExtractAll.dll` into
`<game>\BepInEx\plugins\GK2ExtractAll\`, or install the Workshop item via the
[GK2 Workshop Auto-Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3807406994).

## Build
```
& "<dotnet>" build -c Release
```

Not affiliated with the developers or publishers of Graveyard Keeper 2.
```

- [ ] **Step 2: Write the Workshop description (< 8000 bytes, RU+EN)**

`E:\GK2Upload\GK2ExtractAll_description.txt` — по образцу
`E:\GK2Upload\GK2ZombieHQ_description.txt`: секция EN («Extract all» — what it does, controls,
settings, links), затем разделитель и раздел РУССКИЙ. В LINKS указать:
`https://github.com/otkosss-png/GK2ExtractAll`,
`https://github.com/otkosss-png/GK2ModInstaller`,
`https://steamcommunity.com/sharedfiles/filedetails/?id=3807406994`.
Проверить размер: `(Get-Item 'E:\GK2Upload\GK2ExtractAll_description.txt').Length` < 8000.

- [ ] **Step 3: Stage the upload folder**

```powershell
$stage = "E:\GK2Upload\GK2ExtractAll\BepInEx\plugins\GK2ExtractAll"
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item "src\GK2ExtractAll\bin\Release\GK2ExtractAll.dll" "$stage\GK2ExtractAll.dll" -Force
Copy-Item "README.md" "$stage\README.txt" -Force
```

- [ ] **Step 4: Capture a real screenshot of the window**

Запустить игру, загрузить сейв, открыть труп на столе вскрытия (кнопка «Извлечь всё» видна),
затем:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Users\DA76~1\AppData\Local\Temp\opencode\capture.ps1 -Out "E:\GK2Upload\GK2ExtractAll_screen.png"
```
Expected: PNG с окном вскрытия и кнопкой.

- [ ] **Step 5: Create the Workshop item**

Run:
```powershell
& "C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller\tools\GK2Publisher\bin\Release\GK2Publisher.exe" `
  --create --folder "E:\GK2Upload\GK2ExtractAll" --title "GK2 Extract All - autopsy 'extract everything' button" `
  --desc-file "E:\GK2Upload\GK2ExtractAll_description.txt" --public
```
Expected: `created item <ID>` + `SubmitItemUpdate: k_EResultOK`. Записать `<ID>`.

- [ ] **Step 6: Add preview + screenshot to the item**

Превью (`GK2ExtractAll_preview.png`, сгенерировать в стиле остальных — рамка + название + иконка)
и один реальный скрин:
```powershell
& "C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller\tools\GK2Publisher\bin\Release\GK2Publisher.exe" `
  --update <ID> --preview "E:\GK2Upload\GK2ExtractAll_preview.png" --public
& "C:\Users\Проньки\Documents\OpenCode\GK2ModInstaller\tools\GK2Publisher\bin\Release\GK2Publisher.exe" `
  --update <ID> --screenshot "E:\GK2Upload\GK2ExtractAll_screen.png" --public
```
Expected: оба `k_EResultOK` (по одному изображению за апдейт — лимит 2).

- [ ] **Step 7: GitHub repo + release**

```bash
git remote add origin https://github.com/otkosss-png/GK2ExtractAll.git
git push -u origin main
```
Собрать zip (`E:\GK2Upload\GK2ExtractAll_v1.0.0.zip` = `BepInEx/plugins/GK2ExtractAll/{dll,README.txt}`),
затем:
```powershell
gh release create v1.0.0 -R otkosss-png/GK2ExtractAll --title "GK2 Extract All v1.0.0" --notes "First release: Extract all button for the autopsy window (organs + pockets)." "E:\GK2Upload\GK2ExtractAll_v1.0.0.zip"
```
Expected: ссылка на релиз.

- [ ] **Step 8: Commit**

```bash
git add README.md
git -c user.name="otkosss-png" -c user.email="otkosss-png@users.noreply.github.com" commit -m "docs: README + Workshop/GitHub release v1.0.0"
```

---

## Self-Review

- **Spec coverage:** Р1 (органы+карманы) → Task 4 `Collect`/`Trigger`; Р2 (кнопка) → Task 3;
  Р3 (по очереди, пауза) → Task 4 `Loop` + `DelayMs` (Task 2/5); Р4 (пропуск + итог) →
  Task 4 `RecordSkipped` + `SetResult`; Р5 (упаковка/публикация) → Task 6; Р6 (геймпад) →
  Task 3 (игровая `LazyButton`/TMP-кнопка на RectTransform окна попадает в навигацию) +
  ручная проверка в Task 4 Step 3 (`Existing UI`).
- **Placeholders:** нет «TBD»; все шаги с кодом или точной командой.
- **Type consistency:** `ExtractQueue` API (Begin/TakeNext/MarkAttempted/RecordExtracted/
  RecordSkipped/ShouldStop, Planned/Extracted/Skipped/Steps/MaxSteps) одинаков в Task 1, 4;
  `CellRef(Id, Kind)`, `CellKind`, `Lang`, `ExtractText.*` совпадают; `ExtractRunner.Start/
  StopIfRunning` совпадают в Task 3 и 4; `ExtractAllButton.Ensure/Clear/SetResult` — Task 3/4.
- **Открытый риск (вне задач):** имена полей `mainOrgansFixedTypeItemCells`/`cells` и
  `IsInteractable`/`DisplayingItem` сверяются на этапе сборки Task 4 Step 2 (есть fallback-рекон).
