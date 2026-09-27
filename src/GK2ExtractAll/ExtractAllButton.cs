using System;
using System.Collections.Generic;
using System.Reflection;
using GK2ExtractAll.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2ExtractAll
{
    internal static class ExtractAllButton
    {
        private static readonly Dictionary<int, Button> _buttons = new Dictionary<int, Button>();
        private static readonly Dictionary<int, TextMeshProUGUI> _results = new Dictionary<int, TextMeshProUGUI>();
        // Окна, для которых диагностику подписи уже выводили (один раз на создание).
        private static readonly HashSet<int> _diagnosed = new HashSet<int>();

        // Данные окна лежат в защищённом поле "data" базового LazyWidget<UIAutopsyWindowData>.
        private static readonly FieldInfo _dataField = AccessTools.Field(typeof(UIAutopsyWindow), "data");

        internal static UIAutopsyWindowData DataOf(UIAutopsyWindow window)
        {
            if (window == null || _dataField == null) return null;
            try { return _dataField.GetValue(window) as UIAutopsyWindowData; }
            catch { return null; }
        }

        internal static void Ensure(UIAutopsyWindow window)
        {
            if (window == null) return;

            int key = window.GetInstanceID();
            if (_buttons.TryGetValue(key, out var existing) && existing != null)
            {
                bool on = Plugin.Mod.ButtonEnabled.Value;
                existing.gameObject.SetActive(on);
                existing.interactable = on && !IsEmpty(window);
                return;
            }

            if (!Plugin.Mod.ButtonEnabled.Value) return;

            var parent = window.transform as RectTransform;
            if (parent == null) return;

            // Источник согласованной пары font+material — живая игровая подпись
            // в этом же окне. Глобальный GameStyle.FontMaterial мог указывать на
            // материал чужого шрифта (после установки других модов) → текст кнопки
            // не рисовался. Берём шрифт у эталона и не копируем случайный материал.
            var reference = UiFactory.FindReferenceLabel(parent);

            // UIAutopsyWindow — кэшируемый синглтон: при повторном открытии могли
            // остаться созданные ранее объекты. Переиспользуем их по имени.
            var btn = FindChild<Button>(parent, "ExtractAllBtn");
            if (btn == null)
            {
                btn = UiFactory.TextButton("ExtractAllBtn", parent, ExtractText.Button(Plugin.Lang), 26);
                var rt = (RectTransform)btn.transform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 40f);
                rt.sizeDelta = new Vector2(230f, 54f);
                btn.onClick.AddListener(() => ExtractRunner.Start(window));
            }
            else
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => ExtractRunner.Start(window));
            }

            var label = FindChild<TextMeshProUGUI>(parent, "ResultLabel");
            if (label == null)
            {
                label = UiFactory.Label("ResultLabel", parent, string.Empty, 24,
                    TextAlignmentOptions.Center, Color.white);
                var lrt = label.rectTransform;
                lrt.anchorMin = new Vector2(0.5f, 0f);
                lrt.anchorMax = new Vector2(0.5f, 0f);
                lrt.pivot = new Vector2(0.5f, 0f);
                lrt.anchoredPosition = new Vector2(0f, 100f);
                lrt.sizeDelta = new Vector2(500f, 34f);
                label.gameObject.SetActive(false);
            }

            // Перекрываем шрифт/материал наших подписей эталоном окна (или
            // собственным материалом шрифта), не полагаясь на глобальный guess.
            var buttonLabel = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            UiFactory.ApplyReferenceFont(buttonLabel, reference);
            UiFactory.ApplyReferenceFont(label, reference);

            _buttons[key] = btn;
            _results[key] = label;
            Plugin.Log.LogInfo("autopsy: extract-all button added");

            if (_diagnosed.Add(key))
            {
                LogDiagnostic(buttonLabel, "extract-all-label");
                LogDiagnostic(reference, "reference");
            }
        }

        // Одноразовая диагностика: показывает реальное состояние подписи
        // (главная зацепка при «кнопка без текста»).
        private static void LogDiagnostic(TMP_Text t, string tag)
        {
            try
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                if (t == null)
                {
                    Plugin.Log.LogInfo("diag[" + tag + "]: <null>");
                    return;
                }
                var c = t.color;
                var rt = t.rectTransform;
                var size = rt != null ? rt.rect.size : Vector2.zero;
                var pos = rt != null ? rt.position : Vector3.zero;
                Plugin.Log.LogInfo("diag[" + tag + "]: text=\"" + (t.text ?? "<null>") + "\""
                    + " empty=" + string.IsNullOrEmpty(t.text)
                    + " font=" + (t.font != null ? t.font.name : "<null>")
                    + " mat=" + (t.fontSharedMaterial != null ? t.fontSharedMaterial.name : "<null>")
                    + " color=(" + c.r.ToString("0.###", ci) + "," + c.g.ToString("0.###", ci)
                    + "," + c.b.ToString("0.###", ci) + "," + c.a.ToString("0.###", ci) + ")"
                    + " size=" + t.fontSize
                    + " align=" + t.alignment
                    + " rect=(" + size.x.ToString("0.#", ci) + "x" + size.y.ToString("0.#", ci) + ")"
                    + " pos=(" + pos.x.ToString("0.#", ci) + "," + pos.y.ToString("0.#", ci)
                    + "," + pos.z.ToString("0.#", ci) + ")");
            }
            catch (Exception ex) { Plugin.Log.LogWarning("diag[" + tag + "] failed: " + ex.Message); }
        }

        internal static void Clear()
        {
            // Hide() лишь деактивирует окно, поэтому созданные дочерние объекты
            // надо реально уничтожить — иначе повторное открытие их накопит.
            foreach (var kv in _buttons)
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value.gameObject);
            foreach (var kv in _results)
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value.gameObject);
            _buttons.Clear();
            _results.Clear();
            _diagnosed.Clear();
            ExtractRunner.OnWindowHidden();
        }

        internal static void SetResult(UIAutopsyWindow window, string text)
        {
            if (window == null) return;
            if (!_results.TryGetValue(window.GetInstanceID(), out var label) || label == null) return;
            label.text = text;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private static T FindChild<T>(Transform parent, string name) where T : Component
        {
            if (parent == null) return null;
            var t = parent.Find(name);
            return t != null ? t.GetComponent<T>() : null;
        }

        private static bool IsEmpty(UIAutopsyWindow window)
        {
            try { var data = DataOf(window); return data == null || data.IsEmpty; }
            catch { return false; }
        }
    }
}
