using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GK2ExtractAll.Core;
using HarmonyLib;
using UnityEngine;

namespace GK2ExtractAll
{
    internal sealed class ExtractRunner : MonoBehaviour
    {
        private static ExtractRunner _instance;
        private UIAutopsyWindow _window;
        private Coroutine _co;

        // Поля виджетов приватные; списки ячеек тоже приватные.
        private static readonly FieldInfo _organsField = AccessTools.Field(typeof(UIAutopsyWindow), "bodyOrgansInventoryWidget");
        private static readonly FieldInfo _pocketsField = AccessTools.Field(typeof(UIAutopsyWindow), "bodyPocketInventoryWidget");
        private static readonly FieldInfo _organCellsField = AccessTools.Field(typeof(BodyOrgansInventoryWidget), "mainOrgansFixedTypeItemCells");
        private static readonly FieldInfo _pocketCellsField = AccessTools.Field(typeof(BodyPocketInventoryWidget), "cells");
        // Штатные методы извлечения приватные.
        private static readonly MethodInfo _extractOrgan = AccessTools.Method(typeof(UIAutopsyWindowData), "TryExtractMainOrgan", new[] { typeof(UIItemCell) });
        private static readonly MethodInfo _extractPocket = AccessTools.Method(typeof(UIAutopsyWindowData), "TryExtractItemFromPocket", new[] { typeof(UIItemCell) });

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

        private void Run(UIAutopsyWindow window)
        {
            StopIfRunning();
            _window = window;
            _co = StartCoroutine(Loop());
        }

        private IEnumerator Loop()
        {
            var queue = new ExtractQueue();
            queue.Begin(CountItems());
            float delay = Mathf.Clamp(Plugin.Mod.DelayMs.Value, 100, 2000) / 1000f;

            while (true)
            {
                int items = CountItems();
                if (queue.ShouldStop(items)) break;

                var next = queue.TakeNext(Collect());
                if (next == null) break;

                int before = items;
                Trigger(next);
                yield return new WaitForSeconds(delay);

                bool ok = CountItems() < before;
                if (ok) queue.RecordExtracted(); // TakeNext уже пометил ячейку обработанной
                else queue.RecordSkipped(next.Id);
            }

            string text = queue.Planned == 0
                ? ExtractText.Nothing(Plugin.Lang)
                : ExtractText.Summary(Plugin.Lang, queue.Extracted, queue.Planned);
            ExtractAllButton.SetResult(_window, text);
            Plugin.Log.LogInfo("extract-all: " + queue.Extracted + "/" + queue.Planned
                + " (skipped " + queue.Skipped + ", steps " + queue.Steps + ")");
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
            catch (System.Exception ex) { Plugin.Log.LogWarning("extract step: " + ex.Message); }
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
