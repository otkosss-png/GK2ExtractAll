using System;
using System.Collections.Generic;

namespace GK2ExtractAll.Core
{
    // Набор строк мода на одном языке. Встроены en и ru; любой другой язык (и правки en/ru)
    // приходят из Localization\<код>.json рядом с DLL. Порядок поиска строки: файл перевода →
    // встроенный язык → английский → сам ключ. Пустая строка в файле считается «не переведено».
    public sealed class TextPack
    {
        public static readonly string[] Keys =
        {
            "button", "select", "run_selected", "counter", "summary", "nothing",
        };

        private static readonly Dictionary<string, string> BuiltinEn = new Dictionary<string, string>
        {
            { "button", "Extract all" },
            { "select", "Select..." },
            { "run_selected", "Extract" },
            { "counter", "Selected: {0} of {1}" },
            { "summary", "Extracted {0} of {1}" },
            { "nothing", "Nothing to extract" },
        };

        private static readonly Dictionary<string, string> BuiltinRu = new Dictionary<string, string>
        {
            { "button", "Извлечь всё" },
            { "select", "Выбрать…" },
            { "run_selected", "Вырезать" },
            { "counter", "Выбрано: {0} из {1}" },
            { "summary", "Извлечено {0} из {1}" },
            { "nothing", "Нечего извлекать" },
        };

        private readonly Dictionary<string, string> _map;

        public string Code { get; }

        private TextPack(string code, Dictionary<string, string> map)
        {
            Code = code;
            _map = map;
        }

        // Встроенные строки языка: ru — русские, всё остальное — английские.
        public static TextPack Builtin(string code) => Create(code, null);

        public static TextPack Create(string code, IDictionary<string, string> overrides)
        {
            code = string.IsNullOrWhiteSpace(code) ? "en" : code.Trim().ToLowerInvariant();
            var map = new Dictionary<string, string>(BuiltinFor(code), StringComparer.Ordinal);
            if (overrides != null)
                foreach (var pair in overrides)
                    if (!string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value))
                        map[pair.Key.Trim()] = pair.Value;
            return new TextPack(code, map);
        }

        // Шаблон перевода для файла: ключи мода с английскими строками.
        public static IReadOnlyDictionary<string, string> Template(string code) => BuiltinFor(code);

        public string Get(string key)
        {
            if (_map.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
            return BuiltinEn.TryGetValue(key, out var en) ? en : key;
        }

        // Подстановка {0}/{1}. Если переводчик сломал шаблон — английский вариант, а не исключение.
        public string Format(string key, params object[] args)
        {
            try { return string.Format(Get(key), args); }
            catch (FormatException)
            {
                return BuiltinEn.TryGetValue(key, out var en) ? string.Format(en, args) : key;
            }
        }

        private static Dictionary<string, string> BuiltinFor(string code)
            => string.Equals(code, "ru", StringComparison.OrdinalIgnoreCase) ? BuiltinRu : BuiltinEn;
    }
}
