using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GK2ExtractAll.Core;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.UI;

namespace GK2ExtractAll
{
    internal sealed class ExtractRunner : MonoBehaviour
    {
        private const float PollStep = 0.05f;
        private const float PollTimeout = 1f;
        private const float StallTimeout = 15f;

        private static ExtractRunner _instance;
        private UIAutopsyWindow _window;
        private ExtractSelection _selection;
        private Coroutine _co;
        // Пока мы внутри шага подтверждения, игра сама закрывает окно вскрытия
        // (confirm-колбэки вызывают UIAutopsyWindow.Close()) — не считаем это
        // закрытием пользователем и не останавливаем проход.
        private bool _confirming;
        private bool _stalled;
        // Стол вскрытия (WgoData) — приватное поле данных окна; нужен его CraftComponent.
        private static readonly FieldInfo _autopsyTableField = AccessTools.Field(typeof(UIAutopsyWindowData), "autopsyTable");

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
        // Состояние окна выбора: data (есть ли вообще крафт) и кнопка старта
        // (её interactable == data.CanStartCraft, т.е. прошла ли валидация игры).
        private static readonly FieldInfo _craftDataField = AccessTools.Field(typeof(UIBaseCraftSelectionWindow), "data");
        private static readonly FieldInfo _startCraftButtonField = AccessTools.Field(typeof(UIBaseCraftSelectionWindow), "startCraftButton");
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
            Start(window, null); // без выбора — как раньше, всё подряд
        }

        // selection == null => всё; иначе только отмеченные ячейки.
        internal static void Start(UIAutopsyWindow window, ExtractSelection selection)
        {
            if (window == null) return;
            if (_instance == null)
            {
                var go = new GameObject("GK2ExtractAllRunner");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ExtractRunner>();
            }
            _instance.Run(window, selection);
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

        private void Run(UIAutopsyWindow window, ExtractSelection selection)
        {
            StopIfRunning();
            _window = window;
            _selection = selection;
            _confirming = false;
            _co = StartCoroutine(Loop());
        }

        private IEnumerator Loop()
        {
            var queue = new ExtractQueue();
            // Цели фиксируем ОДИН раз, по уникальным id предметов: ячейки "Прочее"
            // сдвигаются после каждого вырезания (кровь из слота 1 переезжает в 0),
            // поэтому номер ячейки между шагами указывает уже на другой предмет.
            // Одинаковые предметы (куски мяса) делят UniqueId — ключ идёт с повтором.
            // Planned считаем по тому же предикату, что и выбор (IsUsable), иначе
            // заполненная, но недоступная ячейка раздувает знаменатель итога.
            var targets = new List<string>();
            foreach (var c in Collect(_window, _selection))
            {
                var key = KeyOf(CellById(_window, c.Id));
                if (key != null) targets.Add(key);
            }
            queue.Begin(targets);
            Plugin.Log.LogInfo("extract-all: start, targets " + queue.Planned);
            float delay = Mathf.Clamp(Plugin.Mod.DelayMs.Value, 100, 2000) / 1000f;

            while (true)
            {
                var present = Present();
                if (queue.ShouldStop(present.Count)) break;

                var next = queue.TakeNext(present);
                if (next == null) break;

                int before = CopiesOf(next.Id);
                Trigger(next);
                yield return ConfirmStep(next.Kind, delay);

                // Вырезание — обычный крафт стола (у игрока идёт шкала прогресса).
                // Игра ставит каждый новый крафт вскрытия В НАЧАЛО очереди, поэтому
                // следующий шаг до окончания текущего перебивал бы его. Ждём.
                var craft = CraftOf(_window);
                yield return WaitCraftDone(craft);
                if (_stalled)
                {
                    Plugin.Log.LogInfo("extract-all: craft stalled (player left the table?), stopping");
                    queue.RecordSkipped(next.Id);
                    break;
                }

                // Confirm-колбэк закрывает окно вскрытия; возвращаем его, чтобы
                // следующие шаги шли штатно и итоговая подпись была видна.
                ReopenIfClosed();
                yield return null;

                bool ok = CopiesOf(next.Id) < before;
                if (ok) queue.RecordExtracted(next.Id);
                else queue.RecordSkipped(next.Id);
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
                    try
                    {
                        // OnStartCraft() входит в подтверждение только когда кнопка
                        // старта доступна (= data.CanStartCraft). Недоступность —
                        // штатный пропуск (нет инструмента/знания), не ошибка.
                        var craftData = _craftDataField != null ? _craftDataField.GetValue(win) : null;
                        var startButton = _startCraftButtonField != null
                            ? _startCraftButtonField.GetValue(win) as Selectable : null;
                        if (craftData != null && startButton != null && startButton.interactable)
                            confirmed = _confirmCraft != null && (bool)_confirmCraft.Invoke(win, null);
                        else
                            Plugin.Log.LogInfo("confirm organ: not available, skipping step");
                    }
                    catch (Exception ex)
                    {
                        // TargetInvocationException прячет настоящую причину в InnerException.
                        Plugin.Log.LogWarning("confirm organ: " + (ex.InnerException ?? ex));
                    }
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
                    catch (Exception ex) { Plugin.Log.LogWarning("confirm pocket: " + (ex.InnerException ?? ex)); }
                    finally { _confirming = false; }
                }
            }
            yield return new WaitForSeconds(delay);
        }

        // Ждёт, пока стол вскрытия доделает очередь крафтов. Если прогресс не
        // меняется StallTimeout секунд (игрок отошёл, крафт на паузе) — _stalled.
        private IEnumerator WaitCraftDone(CraftComponent craft)
        {
            _stalled = false;
            if (craft == null) yield break;
            float idle = 0f;
            string last = null;
            while (true)
            {
                string state;
                try
                {
                    if (!craft.HasCraftsInQueue) yield break;
                    var cur = craft.CurrentCraftElement;
                    state = craft.CraftElementsQueue.Count + "|" + craft.Status + "|"
                        + (cur != null ? cur.ProgressTimeNormalized.ToString("F3") : "-");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning("wait craft: " + ex.Message);
                    yield break;
                }
                if (state != last) { last = state; idle = 0f; }
                else if ((idle += PollStep) >= StallTimeout) { _stalled = true; yield break; }
                yield return new WaitForSeconds(PollStep);
            }
        }

        private static CraftComponent CraftOf(UIAutopsyWindow window)
        {
            try
            {
                var data = ExtractAllButton.DataOf(window);
                var table = data != null && _autopsyTableField != null
                    ? _autopsyTableField.GetValue(data) as WgoData : null;
                return table != null ? table.CraftComponent : null;
            }
            catch { return null; }
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
                // Черепа/данные тела UIAutopsyWindowData считает только в конструкторе,
                // поэтому старые данные показывали бы тело до вырезания. Как и сама игра
                // при открытии окна — создаём свежие данные по столу.
                var old = ExtractAllButton.DataOf(_window);
                var table = old != null && _autopsyTableField != null
                    ? _autopsyTableField.GetValue(old) as WgoData : null;
                var data = table != null ? new UIAutopsyWindowData(table) : old;
                if (data != null) _window.Open(data);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("reopen autopsy: " + ex.Message); }
        }

        private static List<UIFixedTypeItemCell> OrganCells(UIAutopsyWindow window)
        {
            try
            {
                var w = _organsField != null ? _organsField.GetValue(window) as BodyOrgansInventoryWidget : null;
                if (w == null || _organCellsField == null) return null;
                return _organCellsField.GetValue(w) as List<UIFixedTypeItemCell>;
            }
            catch { return null; }
        }

        private static List<UIItemCell> PocketCells(UIAutopsyWindow window)
        {
            try
            {
                var w = _pocketsField != null ? _pocketsField.GetValue(window) as BodyPocketInventoryWidget : null;
                if (w == null || _pocketCellsField == null) return null;
                return _pocketCellsField.GetValue(w) as List<UIItemCell>;
            }
            catch { return null; }
        }

        // Все заполненные ячейки окна: органы, затем "Прочее".
        private IEnumerable<KeyValuePair<UIItemCell, CellKind>> Cells()
        {
            var organs = OrganCells(_window);
            if (organs != null)
                foreach (var c in organs)
                {
                    var cell = OrganCell(c);
                    if (IsFilled(cell)) yield return new KeyValuePair<UIItemCell, CellKind>(cell, CellKind.Organ);
                }
            var pockets = PocketCells(_window);
            if (pockets != null)
                foreach (var cell in pockets)
                    if (IsFilled(cell)) yield return new KeyValuePair<UIItemCell, CellKind>(cell, CellKind.Pocket);
        }

        // Доступные предметы, что ещё лежат в теле; Id = ключ предмета.
        // Какие из них — цели, решает очередь (ключи с остатком).
        private List<CellRef> Present()
        {
            var list = new List<CellRef>();
            try
            {
                foreach (var kv in Cells())
                {
                    var key = KeyOf(kv.Key);
                    if (key != null && IsUsable(kv.Key))
                        list.Add(new CellRef(key, kv.Value));
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("present: " + ex.Message); }
            return list;
        }

        // Сколько ячеек сейчас показывают предмет с этим ключом.
        private int CopiesOf(string key)
        {
            int n = 0;
            try
            {
                foreach (var kv in Cells())
                    if (KeyOf(kv.Key) == key) n++;
            }
            catch { }
            return n;
        }

        private UIItemCell FindCell(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var kv in Cells())
                if (KeyOf(kv.Key) == key) return kv.Key;
            return null;
        }

        // Ключ предмета в ячейке: уникальный id игры, иначе — ссылка на объект.
        private static string KeyOf(UIItemCell c)
        {
            var item = c != null ? c.DisplayingItem : null;
            if (item == null) return null;
            var uid = item.UniqueId;
            var id = uid != null ? uid.Id : null;
            return !string.IsNullOrEmpty(id)
                ? id
                : "ref:" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item);
        }

        // Список того, что можно вырезать: id, категория и подпись предмета (для панели выбора).
        internal static List<ExtractEntry> Entries(UIAutopsyWindow window)
        {
            var list = new List<ExtractEntry>();
            if (window == null) return list;
            try
            {
                var organs = OrganCells(window);
                if (organs != null)
                    for (int i = 0; i < organs.Count; i++)
                    {
                        var c = OrganCell(organs[i]);
                        if (!IsUsable(c)) continue;
                        list.Add(new ExtractEntry
                        {
                            Id = "organ:" + i,
                            Category = ExtractCategory.Organ,
                            Label = ItemLabel(c)
                        });
                    }
                var pockets = PocketCells(window);
                if (pockets != null)
                    for (int i = 0; i < pockets.Count; i++)
                    {
                        var c = pockets[i];
                        if (!IsUsable(c)) continue;
                        list.Add(new ExtractEntry
                        {
                            Id = "pocket:" + i,
                            Category = ExtractCategory.Pocket,
                            Label = ItemLabel(c)
                        });
                    }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("entries: " + ex.Message); }
            return list;
        }

        private static List<CellRef> Collect(UIAutopsyWindow window, ExtractSelection selection)
        {
            var list = new List<CellRef>();
            foreach (var e in Entries(window))
            {
                if (selection != null && !selection.IsSelected(e.Id)) continue;
                list.Add(new CellRef(e.Id, e.Category == ExtractCategory.Organ ? CellKind.Organ : CellKind.Pocket));
            }
            return list;
        }

        // Хендлы ячеек окна: id <-> игровая ячейка (нужны режиму выбора).
        internal sealed class CellHandles
        {
            internal readonly Dictionary<string, UIItemCell> ById = new Dictionary<string, UIItemCell>();
            internal readonly Dictionary<int, string> ByCell = new Dictionary<int, string>();
        }

        internal static CellHandles Handles(UIAutopsyWindow window)
        {
            var h = new CellHandles();
            try
            {
                foreach (var e in Entries(window))
                {
                    var c = CellById(window, e.Id);
                    if (c == null) continue;
                    h.ById[e.Id] = c;
                    h.ByCell[c.GetInstanceID()] = e.Id;
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("handles: " + ex.Message); }
            return h;
        }

        internal static string IdOf(CellHandles handles, UIItemCell cell)
        {
            if (handles == null || cell == null) return null;
            return handles.ByCell.TryGetValue(cell.GetInstanceID(), out var id) ? id : null;
        }

        internal static UIItemCell CellOf(CellHandles handles, string id)
        {
            if (handles == null || string.IsNullOrEmpty(id)) return null;
            return handles.ById.TryGetValue(id, out var c) ? c : null;
        }

        private static UIItemCell CellById(UIAutopsyWindow window, string id)
        {
            var parts = id.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int i)) return null;
            if (parts[0] == "organ")
            {
                var organs = OrganCells(window);
                return organs != null && i >= 0 && i < organs.Count ? OrganCell(organs[i]) : null;
            }
            var pockets = PocketCells(window);
            return pockets != null && i >= 0 && i < pockets.Count ? pockets[i] : null;
        }

        private static string ItemLabel(UIItemCell c)        {
            try
            {
                var item = c != null ? c.DisplayingItem : null;
                var def = item != null ? item.Definition : null;
                var name = def != null ? def.GetHeader() : null;
                return string.IsNullOrEmpty(name) ? "?" : name;
            }
            catch { return "?"; }
        }

        private void Trigger(CellRef cell)
        {
            try
            {
                var data = ExtractAllButton.DataOf(_window);
                if (data == null) return;
                var c = FindCell(cell.Id);
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

        private static UIItemCell OrganCell(UIFixedTypeItemCell c) => c != null ? c.UIItemCell : null;

        private static bool IsFilled(UIItemCell c) => c != null && c.DisplayingItem != null;

        private static bool IsUsable(UIItemCell c) => IsFilled(c) && c.IsInteractable;
    }
}
