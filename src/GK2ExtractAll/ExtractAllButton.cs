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
            try { var data = DataOf(window); return data == null || data.IsEmpty; }
            catch { return false; }
        }
    }
}
