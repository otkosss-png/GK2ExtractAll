using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GK2ExtractAll.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace GK2ExtractAll
{
    // Переводы мода: BepInEx\plugins\<папка мода>\Localization\<код>.json (en.json, ru.json, de.json …).
    // В одном файле и надписи окна вскрытия (button, select, …), и строки экрана настроек
    // (mod.*, settings.*). Строки настроек копируются туда, где их ищет GK2 Mod Framework:
    // BepInEx\plugins\GK2.Framework\Localization\<modId>\<код>.json.
    // en.json/ru.json создаются, если их нет, — это шаблон для переводчиков; правки игрока не
    // перезаписываются. Язык «auto» = язык игры.
    internal static class ModLocalization
    {
        internal const string ModId = "otkosss.gk2.extractall";

        private static readonly Dictionary<string, string> SettingsEn = new Dictionary<string, string>
        {
            { "mod.name", "GK2 Extract All" },
            { "mod.description", "Adds 'Extract all' and 'Select...' buttons to the autopsy window." },
            { "settings.General.Language.name", "Language" },
            { "settings.General.Language.description", "auto = game language; or a code from the Localization folder (en, ru, de...)" },
            { "settings.General.Enabled.name", "'Extract all' button" },
            { "settings.General.Enabled.description", "Show the buttons in the autopsy window" },
            { "settings.General.DelayMs.name", "Delay between extractions (ms)" },
            { "settings.General.DelayMs.description", "How fast the steps go" },
            { "settings.General.SelectionPreset.name", "Selection preset" },
            { "settings.General.SelectionPreset.description", "What is marked by default in selection mode" },
        };

        private static readonly Dictionary<string, string> SettingsRu = new Dictionary<string, string>
        {
            { "mod.name", "GK2 Извлечь всё" },
            { "mod.description", "Кнопки «Извлечь всё» и «Выбрать…» в окне вскрытия." },
            { "settings.General.Language.name", "Язык" },
            { "settings.General.Language.description", "auto = язык игры; или код файла из папки Localization (en, ru, de…)" },
            { "settings.General.Enabled.name", "Кнопка «Извлечь всё»" },
            { "settings.General.Enabled.description", "Показывать кнопки в окне вскрытия" },
            { "settings.General.DelayMs.name", "Задержка между извлечениями (мс)" },
            { "settings.General.DelayMs.description", "Как быстро идут шаги" },
            { "settings.General.SelectionPreset.name", "Пресет выбора" },
            { "settings.General.SelectionPreset.description", "Что отмечено по умолчанию в режиме выбора" },
        };

        internal static string Dir
        {
            get
            {
                var self = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(string.IsNullOrEmpty(self) ? BepInEx.Paths.PluginPath : self, "Localization");
            }
        }

        // Создать шаблоны en/ru (если их нет) и разложить строки настроек для Framework.
        internal static void EnsureFiles()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                WriteTemplateIfMissing("en", SettingsEn);
                WriteTemplateIfMissing("ru", SettingsRu);
                SyncFrameworkSettings();
            }
            catch (Exception ex) { Plugin.Log?.LogWarning("localization: " + ex.Message); }
        }

        // Коды языков, для которых есть файл (для списка в настройках).
        internal static List<string> AvailableCodes()
        {
            var codes = new List<string>();
            try
            {
                if (Directory.Exists(Dir))
                    foreach (var f in Directory.GetFiles(Dir, "*.json"))
                    {
                        var code = Path.GetFileNameWithoutExtension(f).Trim().ToLowerInvariant();
                        if (IsValidCode(code) && !codes.Contains(code)) codes.Add(code);
                    }
            }
            catch { }
            foreach (var builtin in new[] { "ru", "en" })
                if (!codes.Contains(builtin)) codes.Insert(0, builtin);
            codes.Sort(StringComparer.Ordinal);
            return codes;
        }

        // setting: auto | код. Для auto — язык игры; zh_cn → zh_cn, затем zh; нет файла → встроенный.
        internal static TextPack Load(string setting)
        {
            string wanted = string.Equals(setting, "auto", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(setting)
                ? GameLanguage()
                : setting.Trim().ToLowerInvariant().Replace('-', '_');

            foreach (var code in Candidates(wanted))
            {
                var file = ReadFile(code);
                if (file != null) return TextPack.Create(code, file);
                if (code == "en" || code == "ru") return TextPack.Builtin(code);
            }
            return TextPack.Builtin("en");
        }

        internal static string GameLanguage()
        {
            try { return GK2.Framework.FrameworkLocalization.CurrentLanguage ?? "en"; }
            catch { return "en"; }
        }

        private static IEnumerable<string> Candidates(string code)
        {
            if (!IsValidCode(code)) { yield return "en"; yield break; }
            yield return code;
            int sep = code.IndexOf('_');
            if (sep > 0) yield return code.Substring(0, sep);
            yield return "en";
        }

        private static bool IsValidCode(string code)
            => !string.IsNullOrEmpty(code) && code.Length <= 16
               && code.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_');

        private static Dictionary<string, string> ReadFile(string code)
        {
            var path = Path.Combine(Dir, code + ".json");
            if (!File.Exists(path)) return null;
            try
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path, Encoding.UTF8));
                return parsed ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("localization: cannot read " + code + ".json: " + ex.Message);
                return null;
            }
        }

        private static void WriteTemplateIfMissing(string code, Dictionary<string, string> settings)
        {
            var path = Path.Combine(Dir, code + ".json");
            if (File.Exists(path)) return;
            var all = new Dictionary<string, string>();
            foreach (var pair in TextPack.Template(code)) all[pair.Key] = pair.Value;
            foreach (var pair in settings) all[pair.Key] = pair.Value;
            File.WriteAllText(path, JsonConvert.SerializeObject(all, Formatting.Indented), new UTF8Encoding(false));
        }

        // Строки mod.*/settings.* из каждого нашего файла — в папку переводов GK2 Mod Framework.
        private static void SyncFrameworkSettings()
        {
            var target = Path.Combine(BepInEx.Paths.PluginPath, "GK2.Framework", "Localization", ModId);
            Directory.CreateDirectory(target);
            foreach (var code in AvailableCodes())
            {
                var file = ReadFile(code) ?? new Dictionary<string, string>();
                var fallback = code == "ru" ? SettingsRu : code == "en" ? SettingsEn : null;
                var settings = new Dictionary<string, string>();
                if (fallback != null) foreach (var pair in fallback) settings[pair.Key] = pair.Value;
                foreach (var pair in file)
                    if (!string.IsNullOrEmpty(pair.Value) && (pair.Key.StartsWith("settings.", StringComparison.Ordinal)
                        || pair.Key.StartsWith("mod.", StringComparison.Ordinal)))
                        settings[pair.Key] = pair.Value;
                if (settings.Count == 0) continue;
                WriteIfChanged(Path.Combine(target, code + ".json"), JsonConvert.SerializeObject(settings, Formatting.Indented));
            }
            try { GK2.Framework.FrameworkLocalization.Reload(ModId); } catch { }
        }

        private static void WriteIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == content) return;
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
    }
}
