using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using GK2ExtractAll.Core;

namespace GK2ExtractAll
{
    // Framework — мягкая зависимость: есть — настройки в меню Mods; нет — мод работает, настройки в cfg.
    [BepInDependency(FrameworkGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, "GK2 Extract All", "1.2.9")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "otkosss.gk2.extractall";
        internal const string FrameworkGuid = "ru.superman4eg.gk2.framework";
        public static ManualLogSource Log;
        internal static ModSettings Mod;
        private static TextPack _texts = TextPack.Builtin("en");
        private static string _textsKey;
        private static float _textsCheckedAt = -100f;

        // Строки на текущем языке. «auto» следует за языком игры, поэтому раз в пару секунд
        // перепроверяем (язык игры может загрузиться позже нас или смениться в настройках).
        internal static TextPack Lang
        {
            get
            {
                try
                {
                    float now = Time.unscaledTime;
                    if (now - _textsCheckedAt < 2f) return _texts;
                    _textsCheckedAt = now;
                    string setting = Mod != null && Mod.Language != null ? Mod.Language.Value : "auto";
                    bool auto = string.IsNullOrWhiteSpace(setting) || string.Equals(setting, "auto", StringComparison.OrdinalIgnoreCase);
                    string key = setting + "|" + (auto ? ModLocalization.GameLanguage() : "");
                    if (key != _textsKey)
                    {
                        _textsKey = key;
                        _texts = ModLocalization.Load(setting);
                        Log?.LogInfo("language: setting=" + setting + " -> " + _texts.Code);
                    }
                }
                catch (Exception ex) { Log?.LogWarning("language: " + ex.Message); }
                return _texts;
            }
        }

        private void Awake()
        {
            Log = Logger;
            HasFramework = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(FrameworkGuid);
            ModLocalization.EnsureFiles();

            Mod = new ModSettings();
            if (HasFramework)
            {
                try { RegisterWithFramework(); }
                catch (Exception ex)
                {
                    Logger.LogWarning("GK2 Framework register failed, using local config: " + ex.Message);
                }
            }
            else
            {
                Logger.LogInfo("GK2 Mod Framework not installed: using BepInEx\\config settings (no in-game Mods menu).");
            }
            EnsureSettings();

            // Патчи применяем ПО КЛАССАМ и с try/catch: если один патч не сошёлся
            // (Harmony биндит параметры по имени), остальные всё равно применятся.
            try
            {
                var harmony = new HarmonyLib.Harmony(Guid);
                foreach (var type in LoadableTypes())
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

        internal static bool HasFramework;

        // Без Framework тип FrameworkMod не загрузится и GetTypes() бросит
        // ReflectionTypeLoadException — берём только успешно загруженные типы.
        private static System.Collections.Generic.IEnumerable<Type> LoadableTypes()
        {
            Type[] types;
            try { types = typeof(Plugin).Assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
            foreach (var t in types)
                if (t != null && t.Name != "FrameworkMod") yield return t; // не typeof: тот грузит тип
        }

        // Отдельный метод: типы Framework подгружаются только при его JIT,
        // а он вызывается лишь когда Framework найден.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void RegisterWithFramework()
        {
            GK2.Framework.FrameworkApi.RegisterMod(new FrameworkMod(), Config);
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
            if (Mod.Language == null) Mod.Language = Config.Bind("General", "Language", ModSettings.DefaultLanguage, "auto (game language) or a code from the Localization folder: en, ru, de...");
            if (Mod.ButtonEnabled == null) Mod.ButtonEnabled = Config.Bind("General", "Enabled", ModSettings.DefaultEnabled, "Show the 'Extract all' button");
            if (Mod.DelayMs == null) Mod.DelayMs = Config.Bind("General", "DelayMs", ModSettings.DefaultDelayMs, "Delay between extractions (ms)");
            if (Mod.SelectionPreset == null) Mod.SelectionPreset = Config.Bind("General", "SelectionPreset", "all", "Remembered selection preset: all | organs | pockets | none");
            if (Mod.AllowZombies == null) Mod.AllowZombies = Config.Bind("General", "AllowZombies", false, "Allow the mod to extract organs from zombies (the game only lets you swap them)");
        }

        internal static string Version => typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }
}
