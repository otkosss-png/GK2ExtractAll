using System;
using HarmonyLib;

namespace GK2ExtractAll
{
    [HarmonyPatch(typeof(UIAutopsyWindow), "Redraw")]
    internal static class AutopsyWindowRedrawPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIAutopsyWindow __instance)
        {
            ExtractAllButton.Ensure(__instance);
            // В режиме выбора состав ячеек мог измениться (вырезали предмет) — ок.
            if (ExtractMarks.ModeOn && !ExtractMarks.IsModeFor(__instance)) ExtractMarks.ExitMode();
        }
    }

    [HarmonyPatch(typeof(UIAutopsyWindow), "Hide")]
    internal static class AutopsyWindowHidePatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIAutopsyWindow __instance)
        {
            ExtractMarks.ExitMode();
            ExtractAllButton.Clear();
        }
    }

    // Геймпад в режиме выбора: нажатие A (Select) на ячейке не вырезает орган, а ставит/снимает
    // отметку — как «пин» в Recipe Pin. Мышь работает как раньше (клик по ячейке = вырезать).
    [HarmonyPatch(typeof(UIAutopsyWindowData), "OnItemCellPressOrgans")]
    internal static class CellPressOrgansPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(UIItemCell itemCell)
        {
            try
            {
                if (!ExtractMarks.ModeOn) return true;
                if (!LazyBearTechnology.LazyInput.IsGamepadActive) return true;
                return !ExtractMarks.ToggleByCell(itemCell);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("cell press organs: " + ex.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(UIAutopsyWindowData), "OnItemCellPressPocket")]
    internal static class CellPressPocketPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(UIItemCell itemCell)
        {
            try
            {
                if (!ExtractMarks.ModeOn) return true;
                if (!LazyBearTechnology.LazyInput.IsGamepadActive) return true;
                return !ExtractMarks.ToggleByCell(itemCell);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("cell press pocket: " + ex.Message); return true; }
        }
    }
}
