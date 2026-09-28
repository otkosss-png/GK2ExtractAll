using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using GK2ExtractAll.Core;

namespace GK2ExtractAll
{
    [BepInDependency("ru.superman4eg.gk2.framework")]
    [BepInPlugin(Guid, "GK2 Extract All", "1.2.2")]
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

            // Патчи применяем ПО КЛАССАМ и с try/catch: если один патч не сошёлся
            // (Harmony биндит параметры по имени), остальные всё равно применятся.
            try
            {
                var harmony = new HarmonyLib.Harmony(Guid);
                foreach (var type in typeof(Plugin).Assembly.GetTypes())
                {
                    try { harmony.CreateClassProcessor(type).Patch(); }
                    catch (Exception ex) { Logger.LogWarning("patch " + type.Name + " failed: " + ex.Message); }
                }
            }
            catch (Exception ex) { Logger.LogWarning("harmony init failed: " + ex.Message); }

            Logger.LogInfo("GK2 Extract All " + Version + " loaded.");

            // Значки выбора живут на своём оверлей-канвасе (как «пин» в Recipe Pin).
            try { gameObject.AddComponent<ExtractMarks>(); }
            catch (Exception ex) { Logger.LogWarning("marks component failed: " + ex.Message); }
        }

        // Пресет выбора помним между открытиями (all / organs / pockets / none).
        internal static ExtractPreset Preset()
        {
            try { return ParsePreset(Mod != null && Mod.SelectionPreset != null ? Mod.SelectionPreset.Value : null); }
            catch { return ExtractPreset.All; }
        }

        internal static void SetPreset(ExtractPreset preset)
        {
            try
            {
                if (Mod == null || Mod.SelectionPreset == null) return;
                Mod.SelectionPreset.Value = preset.ToString().ToLowerInvariant();
            }
            catch { }
        }

        private static ExtractPreset ParsePreset(string value)
        {
            if (string.Equals(value, "organs", StringComparison.OrdinalIgnoreCase)) return ExtractPreset.Organs;
            if (string.Equals(value, "pockets", StringComparison.OrdinalIgnoreCase)) return ExtractPreset.Pockets;
            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) return ExtractPreset.None;
            return ExtractPreset.All;
        }

        private void EnsureSettings()
        {
            if (Mod.DelayMs != null) return;
            Mod.Language = Config.Bind("General", "Language", Mod.DefaultLanguage, "auto | en | ru");
            Mod.ButtonEnabled = Config.Bind("General", "Enabled", Mod.DefaultEnabled, "Show the 'Extract all' button");
            Mod.DelayMs = Config.Bind("General", "DelayMs", Mod.DefaultDelayMs, "Delay between extractions (ms)");
            Mod.SelectionPreset = Config.Bind("General", "SelectionPreset", "all", "Remembered selection preset: all | organs | pockets | none");
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
