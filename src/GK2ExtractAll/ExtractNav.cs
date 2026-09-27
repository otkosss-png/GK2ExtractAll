using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.UI;

namespace GK2ExtractAll
{
    // Геймпад-навигация наших кнопок.
    // Игра в UIAutopsyWindow.Redraw зовёт controller.ReinitItems(true, null, null), т.е. список
    // элементов контроллер строит САМ (наши кнопки он не видит). Поэтому мы после каждого
    // ReinitItems добавляем свои GamepadNavigationItem в приватный selectableItems контроллера
    // и пересчитываем индексы (RemoveNullsAndSetIndexes) + связываем навигацию по кругу.
    internal static class ExtractNav
    {
        private sealed class Entry
        {
            public GameObject Window;
            public GamepadNavigationItem Item;
        }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static readonly HashSet<int> _logged = new HashSet<int>();

        private static readonly FieldInfo _selectableField =
            AccessTools.Field(typeof(GamepadNavigationController), "selectableItems");
        private static readonly FieldInfo _buttonField =
            AccessTools.Field(typeof(GamepadNavigationItem), "configuredForButton");
        private static readonly FieldInfo _guiScaleField =
            AccessTools.Field(typeof(GamepadNavigationController), "guiScale");
        private static readonly PropertyInfo _controllerProp =
            AccessTools.Property(typeof(LazyWindow<UIAutopsyWindowData>), "GamepadNavigationController");

        private static GamepadNavigationController ControllerOf(GameObject window)
        {
            try
            {
                if (window == null || _controllerProp == null) return null;
                var w = window.GetComponent(typeof(LazyWindow<UIAutopsyWindowData>))
                        ?? window.GetComponentInParent(typeof(LazyWindow<UIAutopsyWindowData>));
                if (w == null) return null;
                return _controllerProp.GetValue(w, null) as GamepadNavigationController;
            }
            catch { return null; }
        }

        // Добавить наши элементы в навигацию окна ПРЯМО СЕЙЧАС: окно вызывает ReinitItems только
        // в своём Redraw, а наши кнопки создаются уже ПОСЛЕ этого (в постфиксе) — без явного
        // добавления контроллер о них не узнает до следующего Redraw.
        internal static void Attach(GameObject window)
        {
            try
            {
                var ctrl = ControllerOf(window);
                if (ctrl == null || _selectableField == null) return;
                var list = _selectableField.GetValue(ctrl) as IList;
                if (list == null) return;
                float guiScale = 1f;
                if (_guiScaleField != null) { var v = _guiScaleField.GetValue(ctrl); if (v is float f) guiScale = f; }

                int index = list.Count;
                bool added = false;
                foreach (var e in _entries.ToList())
                {
                    if (e.Window != window || e.Item == null) continue;
                    if (ContainsOurItem(list, e.Item)) continue;
                    list.Add(e.Item);
                    try { e.Item.Init(index++, ctrl, guiScale); } catch { }
                    added = true;
                }
                if (!added) return;
                if (_removeNulls != null) _removeNulls.Invoke(ctrl, new object[] { list });
                if (_linkRound != null) _linkRound.Invoke(ctrl, null);
                Plugin.Log.LogInfo("nav: attached our items, controller items now " + list.Count);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("nav attach: " + ex.Message); }
        }
        private static readonly MethodInfo _removeNulls =
            AccessTools.Method(typeof(GamepadNavigationController), "RemoveNullsAndSetIndexes");
        private static readonly MethodInfo _linkRound =
            AccessTools.Method(typeof(GamepadNavigationController), "LinkRoundNavigation");

        internal static void Register(GameObject window, Button button, Action action)
        {
            try
            {
                if (window == null || button == null) return;
                var item = button.GetComponent<GamepadNavigationItem>();
                if (item == null) item = button.gameObject.AddComponent<GamepadNavigationItem>();
                if (_buttonField != null) _buttonField.SetValue(item, button);
                item.Active = true;
                item.SetCallbacks(null, null, () => action());
                _entries.RemoveAll(e => e.Item == null);
                if (!_entries.Any(e => e.Item == item)) _entries.Add(new Entry { Window = window, Item = item });
            }
            catch (Exception ex) { Plugin.Log.LogWarning("nav register: " + ex.Message); }
        }

        internal static void Unregister(GameObject window)
        {
            if (window == null) { _entries.RemoveAll(e => e.Item == null); return; }
            _entries.RemoveAll(e => e.Item == null || e.Window == window ||
                e.Item.transform.IsChildOf(window.transform));
        }

        private static bool ContainsOurItem(IList list, object item)
        {
            foreach (var o in list) if (ReferenceEquals(o, item)) return true;
            return false;
        }

        [HarmonyPatch(typeof(GamepadNavigationController), "ReinitItems")]
        internal static class ReinitItemsPatch
        {
            [HarmonyPostfix]
            private static void Postfix(GamepadNavigationController __instance)
            {
                try
                {
                    if (__instance == null || _selectableField == null) return;
                    var list = _selectableField.GetValue(__instance) as IList;
                    if (list == null || list.Count == 0) return;

                    _entries.RemoveAll(e => e.Item == null || e.Window == null);
                    bool added = false;
                    foreach (var entry in _entries.ToList())
                    {
                        // Наш элемент играем только для «своего» окна: проверяем, что в списке
                        // контроллера уже есть элемент, лежащий внутри этого же окна.
                        bool belongs = false;
                        foreach (var o in list)
                        {
                            var c = o as Component;
                            if (c != null && c.gameObject != null && entry.Window != null &&
                                (c.gameObject == entry.Window || c.transform.IsChildOf(entry.Window.transform)))
                            { belongs = true; break; }
                        }
                        if (!belongs) continue;
                        if (ContainsOurItem(list, entry.Item)) continue;
                        list.Add(entry.Item);
                        added = true;
                    }
                    if (!added) return;

                    if (_removeNulls != null) _removeNulls.Invoke(__instance, new object[] { list });
                    if (_linkRound != null) _linkRound.Invoke(__instance, null);
                    Plugin.Log.LogInfo("nav: controller items now " + list.Count + " (ours added)");
                }
                catch (Exception ex) { Plugin.Log.LogWarning("nav reinit: " + ex.Message); }
            }
        }
    }
}
