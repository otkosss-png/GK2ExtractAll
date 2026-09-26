using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GK2ExtractAll.Core;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2ExtractAll
{
    internal sealed class ExtractRunner : MonoBehaviour
    {
        private const float PollStep = 0.05f;
        private const float PollTimeout = 1f;

        private static ExtractRunner _instance;
        private UIAutopsyWindow _window;
        private Coroutine _co;
        // Пока мы внутри шага подтверждения, игра сама закрывает окно вскрытия
        // (confirm-колбэки вызывают UIAutopsyWindow.Close()) — не считаем это
        // закрытием пользователем и не останавливаем проход.
        private bool _confirming;

        // Поля виджетов приватные; списки ячеек тоже приватные.
        private static readonly FieldInfo _organsField = AccessTools.Field(typeof(UIAutopsyWindow), "bodyOrgansInventoryWidget");
        private static readonly FieldInfo _pocketsField = AccessTools.Field(typeof(UIAutopsyWindow), "bodyPocketInventoryWidget");
        private static readonly FieldInfo _organCellsField = AccessTools.Field(typeof(BodyOrgansInventoryWidget), "mainOrgansFixedTypeItemCells");
        private static readonly FieldInfo _pocketCellsField = AccessTools.Field(typeof(BodyPocketInventoryWidget), "cells");
        // TryExtract* только ОТКРЫВАЮТ окно выбора; извлечение делают confirm-колбэки.
        private static readonly MethodInfo _extractOrgan = AccessTools.Method(typeof(UIAutopsyWindowData), "TryExtractMainOrgan", new[] { typeof(UIItemCell) });
        private static readonly MethodInfo _extractPocket = AccessTools.Method(typeof(UIAutopsyWindowData), "TryExtractItemFromPocket", new[] { typeof(UIItemCell) });
        // Кнопка "Начать крафт" в окне выбора: OnStartCraft() проверяет CanStartCraft
        // (startCraftButton.interactable) и вызывает OnStartCraftPressed() -> onStartCraftPressed -> Close().
        private static readonly MethodInfo _confirmCraft = AccessTools.Method(typeof(UIBaseCraftSelectionWindow), "OnStartCraft");
        // Кнопка "Да" в диалоге: UIDialogWindowData.ButtonsData[0].onPressed (yesAction).
        private static readonly FieldInfo _dialogDataField = AccessTools.Field(typeof(UIDialogWindow), "data");
        private static readonly PropertyInfo _buttonsDataProp = AccessTools.Property(typeof(UIDialogWindowData), "ButtonsData");
        private static readonly FieldInfo _onPressedField = FindNestedField(typeof(UIDialogWindowData), "ButtonData", "onPressed");

        private static FieldInfo FindNestedField(Type owner, string nestedName, string fieldName)
        {
            try
            {
                var nested = owner != null ? owner.GetNestedType(nestedName, BindingFlags.Public | BindingFlags.NonPublic) : null;
                return nested != null ? AccessTools.Field(nested, fieldName) : null;
            }
            catch { return null; }
        }

        internal static void Start(UIAutopsyWindow window)
        {
            if (window == null) return;
            if (_instance == null)
            {
                var go = new GameObject("GK2ExtractAllRunner");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ExtractRunner>();
            }
            _instance.Run(window);
        }

        internal static void StopIfRunning()
        {
            if (_instance != null && _instance._co != null) _instance.StopCoroutine(_instance._co);
            if (_instance != null) _instance._co = null;
        }

        // Вызывается из UI-патча Hide: если окно закрыла сама игра в рамках нашего
        // подтверждения — игнорируем, иначе останавливаем проход.
        internal static void OnWindowHidden()
        {
            if (_instance != null && _instance._confirming) return;
            StopIfRunning();
        }

        private void Run(UIAutopsyWindow window)
        {
            StopIfRunning();
            _window = window;
            _confirming = false;
            _co = StartCoroutine(Loop());
        }

        private IEnumerator Loop()
        {
            var queue = new ExtractQueue();
            // Planned считаем по тому же предикату, что и выбор (IsUsable), иначе
            // заполненная, но недоступная ячейка раздувает знаменатель итога.
            queue.Begin(Collect().Count);
            float delay = Mathf.Clamp(Plugin.Mod.DelayMs.Value, 100, 2000) / 1000f;

            while (true)
            {
                var present = Collect();
                if (queue.ShouldStop(present.Count)) break;

                var next = queue.TakeNext(present);
                if (next == null) break;

                int before = CountItems();
                Trigger(next);
                yield return ConfirmStep(next.Kind, delay);

                bool ok = CountItems() < before;
                if (ok) queue.RecordExtracted(); // TakeNext уже пометил ячейку обработанной
                else queue.RecordSkipped(next.Id);

                // Confirm-колбэк закрывает окно вскрытия; возвращаем его, чтобы
                // следующие шаги шли штатно и итоговая подпись была видна.
                ReopenIfClosed();
                yield return null;
            }

            string text = queue.Planned == 0
                ? ExtractText.Nothing(Plugin.Lang)
                : ExtractText.Summary(Plugin.Lang, queue.Extracted, queue.Planned);
            ExtractAllButton.SetResult(_window, text);
            Plugin.Log.LogInfo("extract-all: " + queue.Extracted + "/" + queue.Planned
                + " (skipped " + queue.Skipped + ", steps " + queue.Steps + ")");
        }

        // Ждёт появления модалки шага, подтверждает её (или закрывает, если
        // подтверждение недоступно) и выдерживает задержку между шагами.
        private IEnumerator ConfirmStep(CellKind kind, float delay)
        {
            if (kind == CellKind.Organ)
            {
                UICraftSelectionWindow win = null;
                for (float t = 0f; t < PollTimeout; t += PollStep)
                {
                    win = GetCraftWindow();
                    if (win != null && win.IsShown) break;
                    yield return new WaitForSeconds(PollStep);
                }
                if (win != null && win.IsShown)
                {
                    bool confirmed = false;
                    _confirming = true;
                    try { confirmed = _confirmCraft != null && (bool)_confirmCraft.Invoke(win, null); }
                    catch (Exception ex) { Plugin.Log.LogWarning("confirm organ: " + ex.Message); }
                    finally { _confirming = false; }
                    if (!confirmed) CloseCraftWindow(win);
                }
            }
            else
            {
                UIDialogWindow win = null;
                for (float t = 0f; t < PollTimeout; t += PollStep)
                {
                    win = GetDialogWindow();
                    if (win != null && win.IsShown) break;
                    yield return new WaitForSeconds(PollStep);
                }
                if (win != null && win.IsShown)
                {
                    _confirming = true;
                    try { ConfirmDialog(win); }
                    catch (Exception ex) { Plugin.Log.LogWarning("confirm pocket: " + ex.Message); }
                    finally { _confirming = false; }
                }
            }
            yield return new WaitForSeconds(delay);
        }

        private static UICraftSelectionWindow GetCraftWindow()
        {
            try { return LazyUI.GetWindow<UICraftSelectionWindow>(); }
            catch { return null; }
        }

        private static UIDialogWindow GetDialogWindow()
        {
            try { return LazyUI.GetWindow<UIDialogWindow>(); }
            catch { return null; }
        }

        private static void CloseCraftWindow(UICraftSelectionWindow win)
        {
            try { if (win != null) win.Close(); }
            catch (Exception ex) { Plugin.Log.LogWarning("dismiss craft: " + ex.Message); }
        }

        private void ConfirmDialog(UIDialogWindow win)
        {
            var data = _dialogDataField != null ? _dialogDataField.GetValue(win) : null;
            var buttons = data != null && _buttonsDataProp != null
                ? _buttonsDataProp.GetValue(data, null) as IList : null;
            if (buttons != null && buttons.Count > 0)
            {
                var first = buttons[0];
                var action = first != null && _onPressedField != null
                    ? _onPressedField.GetValue(first) as Action : null;
                if (action != null) { action.Invoke(); return; }
            }
            try { if (win != null) win.Close(); }
            catch (Exception ex) { Plugin.Log.LogWarning("dismiss dialog: " + ex.Message); }
        }

        private void ReopenIfClosed()
        {
            try
            {
                if (_window == null || _window.IsShown) return;
                var data = ExtractAllButton.DataOf(_window);
                if (data != null) _window.Open(data);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("reopen autopsy: " + ex.Message); }
        }

        private bool HasData()
        {
            try { return _window != null && ExtractAllButton.DataOf(_window) != null; }
            catch { return false; }
        }

        private List<UIFixedTypeItemCell> OrganCells()
        {
            try
            {
                var w = _organsField != null ? _organsField.GetValue(_window) as BodyOrgansInventoryWidget : null;
                if (w == null || _organCellsField == null) return null;
                return _organCellsField.GetValue(w) as List<UIFixedTypeItemCell>;
            }
            catch { return null; }
        }

        private List<UIItemCell> PocketCells()
        {
            try
            {
                var w = _pocketsField != null ? _pocketsField.GetValue(_window) as BodyPocketInventoryWidget : null;
                if (w == null || _pocketCellsField == null) return null;
                return _pocketCellsField.GetValue(w) as List<UIItemCell>;
            }
            catch { return null; }
        }

        private int CountItems()
        {
            if (!HasData()) return 0;
            int n = 0;
            var organs = OrganCells();
            if (organs != null)
                foreach (var c in organs)
                    if (IsFilled(OrganCell(c))) n++;
            var pockets = PocketCells();
            if (pockets != null)
                foreach (var c in pockets)
                    if (IsFilled(c)) n++;
            return n;
        }

        private List<CellRef> Collect()
        {
            var list = new List<CellRef>();
            if (!HasData()) return list;
            var organs = OrganCells();
            if (organs != null)
                for (int i = 0; i < organs.Count; i++)
                    if (IsUsable(OrganCell(organs[i])))
                        list.Add(new CellRef("organ:" + i, CellKind.Organ));
            var pockets = PocketCells();
            if (pockets != null)
                for (int i = 0; i < pockets.Count; i++)
                    if (IsUsable(pockets[i]))
                        list.Add(new CellRef("pocket:" + i, CellKind.Pocket));
            return list;
        }

        private void Trigger(CellRef cell)
        {
            try
            {
                var data = ExtractAllButton.DataOf(_window);
                if (data == null) return;
                var c = Resolve(cell.Id);
                if (c == null) return;
                if (cell.Kind == CellKind.Organ)
                {
                    if (_extractOrgan != null) _extractOrgan.Invoke(data, new object[] { c });
                }
                else
                {
                    if (_extractPocket != null) _extractPocket.Invoke(data, new object[] { c });
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("extract step: " + ex.Message); }
        }

        private UIItemCell Resolve(string id)
        {
            var parts = id.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int i)) return null;
            if (parts[0] == "organ")
            {
                var w = OrganCells();
                return w != null && i >= 0 && i < w.Count ? OrganCell(w[i]) : null;
            }
            var p = PocketCells();
            return p != null && i >= 0 && i < p.Count ? p[i] : null;
        }

        private static UIItemCell OrganCell(UIFixedTypeItemCell c) => c != null ? c.UIItemCell : null;

        private static bool IsFilled(UIItemCell c) => c != null && c.DisplayingItem != null;

        private static bool IsUsable(UIItemCell c) => IsFilled(c) && c.IsInteractable;
    }
}
