using BepInEx.Configuration;
using GK2.Framework;

namespace GK2ExtractAll
{
    // Настройки появляются в игровом меню Mods (GK2 Mod Framework).
    internal sealed class Mod : Gk2ModBase
    {
        internal const string DefaultLanguage = "en";
        internal const bool DefaultEnabled = true;
        internal const int DefaultDelayMs = 500;

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
            Language = s.AddDropdown("General", "Language", DefaultLanguage, new[] { "auto", "en", "ru" },
                "Language / Язык", "en, ru, auto (system)", 10);
            ButtonEnabled = s.AddToggle("General", "Enabled", DefaultEnabled,
                "Кнопка «Извлечь всё»", "Показывать кнопку в окне вскрытия", 20);
            DelayMs = s.AddIntSlider("General", "DelayMs", DefaultDelayMs, 100, 2000,
                "Задержка между извлечениями (мс)", "Как быстро идут шаги", 5, 30);
        }
    }
}
