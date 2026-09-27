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
}
