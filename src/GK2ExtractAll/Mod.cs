using System.Collections.Generic;
using BepInEx.Configuration;
using GK2.Framework;

namespace GK2ExtractAll
{
    // Настройки мода БЕЗ типов GK2 Framework: остальной код читает их всегда, даже когда
    // Framework не установлен.
    internal sealed class ModSettings
    {
        internal const string DefaultLanguage = "auto";
        internal const bool DefaultEnabled = true;
        internal const int DefaultDelayMs = 500;

        internal ConfigEntry<string> Language;
        internal ConfigEntry<bool> ButtonEnabled;
        internal ConfigEntry<int> DelayMs;
        internal ConfigEntry<string> SelectionPreset;
        internal ConfigEntry<bool> AllowZombies;
    }

    // Мост к игровому меню Mods (GK2 Mod Framework). Класс трогается ТОЛЬКО из
    // Plugin.RegisterWithFramework, когда Framework найден: иначе загрузка типа упала бы.
    internal sealed class FrameworkMod : Gk2ModBase
    {
        private readonly Gk2ModMetadata _metadata = new Gk2ModMetadata(
            "otkosss.gk2.extractall",
            "GK2 Extract All",
            "otkosss",
            "1.2.9",
            "Adds an 'Extract all' button to the autopsy window: pulls every organ and pocket item out of a corpse.",
            false,
            false);

        public override Gk2ModMetadata Metadata => _metadata;

        public override void OnRegister(Gk2ModContext context)
        {
            var s = context.Settings;
            var m = Plugin.Mod;
            // Языки: auto + встроенные en/ru + все Localization\<код>.json рядом с модом.
            var languages = new List<string> { "auto" };
            languages.AddRange(ModLocalization.AvailableCodes());
            m.Language = s.AddDropdown("General", "Language", ModSettings.DefaultLanguage, languages.ToArray(),
                "Language", "auto = game language; or a code from the Localization folder (en, ru, de...)", 10);
            m.ButtonEnabled = s.AddToggle("General", "Enabled", ModSettings.DefaultEnabled,
                "'Extract all' button", "Show the buttons in the autopsy window", 20);
            m.DelayMs = s.AddIntSlider("General", "DelayMs", ModSettings.DefaultDelayMs, 100, 2000,
                "Delay between extractions (ms)", "How fast the steps go", 5, 30);
            m.SelectionPreset = s.AddDropdown("General", "SelectionPreset", "all",
                new[] { "all", "organs", "pockets", "none" },
                "Selection preset", "What is marked by default in selection mode", 6);
            // Игра у зомби органы только меняет (вырезать нельзя) — мод по умолчанию тоже.
            m.AllowZombies = s.AddToggle("General", "AllowZombies", false,
                "Allow extracting from zombies", "The game only lets you swap a zombie's organs. On: the mod's buttons also extract them (you keep the organs)", 40);
        }
    }
}
