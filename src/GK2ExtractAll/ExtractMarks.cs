using System;
using System.Collections.Generic;
using GK2ExtractAll.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GK2ExtractAll
{
    // Значки «вырезать / оставить» на ячейках окна вскрытия — сделано как в Recipe Pin:
    // значки живут на СВОЁМ оверлей-канвасе (сверху всего) и позиционируются над ячейками,
    // поэтому клик по значку ловится всегда и не конфликтует с игровой ячейкой.
    // Показываются только в режиме выбора (после кнопки «Выбрать для вырезки»).
    internal sealed class ExtractMarks : MonoBehaviour
    {
        internal static ExtractMarks Instance;

        private UIAutopsyWindow _window;
        private ExtractSelection _selection;
        private ExtractRunner.CellHandles _handles;
        private readonly Dictionary<string, RectTransform> _badges = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, Image> _badgeImages = new Dictionary<string, Image>();
        private GameObject _canvasGo;

        internal static bool ModeOn => Instance != null && Instance._active;
        private bool _active;

        private void Awake()
        {
            Instance = this;
        }

        internal static bool IsModeFor(UIAutopsyWindow window)
            => Instance != null && Instance._active && Instance._window == window;

        internal static int SelectedCount => Instance != null && Instance._selection != null
            ? Instance._selection.SelectedCount : 0;
        internal static int TotalCount => Instance != null && Instance._selection != null
            ? Instance._selection.Count : 0;

        // Ячейка -> id выбора: для геймпада (A на ячейке в режиме выбора ставит/снимает отметку).
        internal static bool ToggleByCell(UIItemCell cell)
        {
            if (Instance == null || !Instance._active || cell == null || Instance._handles == null) return false;
            var id = ExtractRunner.IdOf(Instance._handles, cell);
            if (id == null) return false;
            Instance.Toggle(id);
            return true;
        }

        // Включить режим: построить выбор (по запомненному пресету) и показать значки.
        internal static void EnterMode(UIAutopsyWindow window)
        {
            try
            {
                if (Instance == null || window == null) return;
                var entries = ExtractRunner.Entries(window);
                if (entries.Count == 0) return;

                Instance.ClearBadges();
                Instance._window = window;
                Instance._selection = new ExtractSelection();
                Instance._selection.Begin(entries, ExtractPreset.None);
                Instance._handles = ExtractRunner.Handles(window);
                Instance._active = true;
                Instance.EnsureCanvas();
                Instance.BuildBadges();
                Instance.Refresh();
                ExtractAllButton.UpdateModeLabel(window);
                Plugin.Log.LogInfo("extract-all: selection mode on (" + SelectedCount + "/" + TotalCount + ")");
            }
            catch (Exception ex) { Plugin.Log.LogWarning("marks enter: " + ex.Message); }
        }

        internal static void ExitMode()
        {
            try
            {
                if (Instance == null) return;
                var window = Instance._window;
                Instance._active = false;
                Instance.ClearBadges();
                Instance._selection = null;
                Instance._handles = null;
                Instance._window = null;
                ExtractAllButton.SetResult(window, null);
                ExtractAllButton.UpdateModeLabel(window);
            }
            catch { }
        }

        // Вырезать отмеченное и выйти из режима.
        internal static void RunSelected(UIAutopsyWindow window)
        {
            try
            {
                if (Instance == null || window == null || !Instance._active) return;
                if (Instance._window != window) return;
                if (Instance._selection == null || Instance._selection.SelectedCount == 0) { ExitMode(); return; }
                var sel = Instance._selection;
                ExitMode();
                ExtractRunner.Start(window, sel);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("marks run: " + ex.Message); }
        }

        // Значок ячейки, на которой стоит фокус геймпада (как «пин» в Recipe Pin).
        internal static bool ToggleFocused()
        {
            try
            {
                if (Instance == null || !Instance._active) return false;
                var item = FocusedItem(Instance._window);
                if (item == null) return false;
                var cell = item.GetComponentInParent<UIItemCell>();
                var id = cell != null ? ExtractRunner.IdOf(Instance._handles, cell) : null;
                if (id == null) return false;
                Instance.Toggle(id);
                return true;
            }
            catch (Exception ex) { Plugin.Log.LogWarning("marks focus toggle: " + ex.Message); return false; }
        }

        private static LazyBearTechnology.GamepadNavigationItem FocusedItem(UIAutopsyWindow window)
        {
            try
            {
                var mi = HarmonyLib.AccessTools.Method(
                    typeof(LazyBearTechnology.LazyWindow<UIAutopsyWindowData>), "GetFocusedNavigationItem");
                return mi != null ? mi.Invoke(window, null) as LazyBearTechnology.GamepadNavigationItem : null;
            }
            catch { return null; }
        }

        internal void Toggle(string id)
        {
            if (_selection == null || string.IsNullOrEmpty(id)) return;
            _selection.Toggle(id);
            Refresh();
        }

        private void EnsureCanvas()
        {
            if (_canvasGo != null) return;
            _canvasGo = new GameObject("GK2ExtractAll_Marks");
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;   // выше окон игры — значок всегда кликается
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGo.AddComponent<GraphicRaycaster>();
        }

        private void BuildBadges()
        {
            if (_selection == null) return;
            foreach (var id in _selection.Ids)
            {
                var cell = ExtractRunner.CellOf(_handles, id);
                if (cell == null) continue;

                // Вид как в Recipe Pin: маленький квадратик-чекбокс (рамка + «заливка» внутри).
                var go = new GameObject("ExtractMark", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_canvasGo.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(16, 16);

                var frame = go.GetComponent<Image>();
                frame.raycastTarget = true;
                frame.color = new Color(0.93f, 0.90f, 0.82f, 0.95f);

                var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
                innerGo.transform.SetParent(rt, false);
                var innerRt = (RectTransform)innerGo.transform;
                innerRt.anchorMin = Vector2.zero;
                innerRt.anchorMax = Vector2.one;
                innerRt.offsetMin = new Vector2(2, 2);
                innerRt.offsetMax = new Vector2(-2, -2);
                var inner = innerGo.GetComponent<Image>();
                inner.raycastTarget = false;

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = frame;
                var capturedId = id;
                btn.onClick.AddListener(() => Toggle(capturedId));

                _badges[id] = rt;
                _badgeImages[id] = inner;
            }
        }

        private void ClearBadges()
        {
            foreach (var kv in _badges)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _badges.Clear();
            _badgeImages.Clear();
        }

        // Значки висят над ячейками: позицию пересчитываем каждый кадр (окно может двигаться).
        private void LateUpdate()
        {
            if (!_active || _canvasGo == null || _handles == null) return;
            try
            {
                var canvasRt = (RectTransform)_canvasGo.transform;
                foreach (var kv in _badges)
                {
                    var rt = kv.Value;
                    if (rt == null) continue;
                    var cell = ExtractRunner.CellOf(_handles, kv.Key);
                    var cellRt = cell != null ? cell.transform as RectTransform : null;
                    if (cellRt == null) { rt.gameObject.SetActive(false); continue; }
                    if (!rt.gameObject.activeSelf) rt.gameObject.SetActive(true);

                    // Левый верхний угол ячейки -> экран -> локальные координаты нашего канваса.
                    Vector3 world = cellRt.TransformPoint(new Vector3(cellRt.rect.xMin, cellRt.rect.yMax, 0f));
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
                    Vector2 local;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out local))
                        rt.anchoredPosition = local + new Vector2(14f, -14f);                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("marks follow: " + ex.Message); }
        }

        private void Refresh()
        {
            try
            {
                foreach (var kv in _badgeImages)
                {
                    var img = kv.Value;
                    if (img == null) continue;
                    bool on = _selection != null && _selection.IsSelected(kv.Key);
                    // Как в Recipe Pin: не выбрано — тёмный квадратик, выбрано — жёлтая заливка.
                    img.color = on
                        ? new Color(0.98f, 0.82f, 0.28f, 1f)
                        : new Color(0.07f, 0.07f, 0.07f, 0.72f);
                }
                ExtractAllButton.SetResult(_window,
                    ExtractText.Counter(Plugin.Lang, SelectedCount, TotalCount));
            }
            catch (Exception ex) { Plugin.Log.LogWarning("marks refresh: " + ex.Message); }
        }
    }
}
