using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GK2ExtractAll
{
    // Подбор шрифта под текст надписи. Наши надписи берут шрифт у игровой подписи окна, а в нём
    // может не быть символов перевода (корейский, японский, китайский — Нексус: вместо букв
    // пустые места). Если в текущем шрифте (с его запасными) символов нет — ищем среди
    // загруженных игрой шрифтов тот, где они есть, и ставим его с его же материалом.
    internal sealed class FontFitter : MonoBehaviour
    {
        private TMP_Text _label;
        private string _checkedText;

        // Текст → подходящий шрифт (null = текущий годится). Кэш на всю сессию.
        private static readonly Dictionary<string, TMP_FontAsset> Cache = new Dictionary<string, TMP_FontAsset>(StringComparer.Ordinal);
        private static readonly HashSet<string> Logged = new HashSet<string>(StringComparer.Ordinal);

        internal static void Attach(TMP_Text label)
        {
            if (label != null && label.GetComponent<FontFitter>() == null) label.gameObject.AddComponent<FontFitter>();
        }

        private void LateUpdate()
        {
            if (_label == null) _label = GetComponent<TMP_Text>();
            if (_label == null) return;
            var text = _label.text;
            if (text == _checkedText) return;
            _checkedText = text;
            try { Fit(_label); }
            catch (Exception ex) { Plugin.Log.LogWarning("font fit: " + ex.Message); }
        }

        internal static void Fit(TMP_Text label)
        {
            var text = Visible(label.text);
            if (string.IsNullOrEmpty(text) || Covers(label.font, text)) return;

            if (!Cache.TryGetValue(text, out var font))
            {
                font = Find(text);
                Cache[text] = font;
                if (Logged.Add(text))
                    Plugin.Log.LogInfo("font fit: \"" + text + "\" -> " + (font != null ? font.name : "<no font has these characters>"));
            }
            if (font == null || font == label.font) return;
            label.font = font;
            label.fontSharedMaterial = font.material;
        }

        // Только печатаемые символы, без тегов разметки TMP (<sprite …>, <color …>).
        private static string Visible(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new System.Text.StringBuilder(text.Length);
            bool inTag = false;
            foreach (var c in text)
            {
                if (c == '<') { inTag = true; continue; }
                if (c == '>' && inTag) { inTag = false; continue; }
                if (inTag || char.IsWhiteSpace(c) || char.IsControl(c)) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool Covers(TMP_FontAsset font, string text)
        {
            if (font == null) return false;
            try { return font.HasCharacters(text, out uint[] _, true, true); }
            catch { return false; }
        }

        // Сначала шрифты видимых игровых надписей (их игра уже подобрала под язык), потом все
        // загруженные шрифты.
        private static TMP_FontAsset Find(string text)
        {
            var tried = new HashSet<TMP_FontAsset>();
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t == null || t.font == null || !t.isActiveAndEnabled || !tried.Add(t.font)) continue;
                if (Covers(t.font, text)) return t.font;
            }
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (f == null || !tried.Add(f)) continue;
                if (Covers(f, text)) return f;
            }
            return null;
        }
    }
}
