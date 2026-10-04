using System.Collections.Generic;
using BepInEx.Configuration;
using GK2.Framework;

namespace GK2ExtractAll
{
    // Настройки появляются в игровом меню Mods (GK2 Mod Framework).
    internal sealed class Mod : Gk2ModBase
    {
        internal const string DefaultLanguage = "auto";
        internal const bool DefaultEnabled = true;
        internal const int DefaultDelayMs = 500;

        private readonly Gk2ModMetadata _metadata = new Gk2ModMetadata(
            "otkosss.gk2.extractall",
            "GK2 Extract All",
            "otkosss",
            "1.2.7",
            "Adds an 'Extract all' button to the autopsy window: pulls every organ and pocket item out of a corpse.",
            false,
            false);

        internal ConfigEntry<string> Language;
        internal ConfigEntry<bool> ButtonEnabled;
        internal ConfigEntry<int> DelayMs;
        internal ConfigEntry<string> SelectionPreset;
        internal ConfigEntry<bool> AllowZombies;

        public override Gk2ModMetadata Metadata => _metadata;

        public override void OnRegister(Gk2ModContext context)
        {
            var s = context.Settings;
            // Языки: auto + встроенные en/ru + все Localization\<код>.json рядом с модом.
            var languages = new List<string> { "auto" };
            languages.AddRange(ModLocalization.AvailableCodes());
            Language = s.AddDropdown("General", "Language", DefaultLanguage, languages.ToArray(),
                "Language", "auto = game language; or a code from the Localization folder (en, ru, de...)", 10);
            ButtonEnabled = s.AddToggle("General", "Enabled", DefaultEnabled,
                "'Extract all' button", "Show the buttons in the autopsy window", 20);
            DelayMs = s.AddIntSlider("General", "DelayMs", DefaultDelayMs, 100, 2000,
                "Delay between extractions (ms)", "How fast the steps go", 5, 30);
            SelectionPreset = s.AddDropdown("General", "SelectionPreset", "all",
                new[] { "all", "organs", "pockets", "none" },
                "Selection preset", "What is marked by default in selection mode", 6);
            // Игра у зомби органы только меняет (вырезать нельзя) — мод по умолчанию тоже.
            AllowZombies = s.AddToggle("General", "AllowZombies", false,
                "Allow extracting from zombies", "The game only lets you swap a zombie's organs. On: the mod's buttons also extract them (you keep the organs)", 40);
        }
    }
}
