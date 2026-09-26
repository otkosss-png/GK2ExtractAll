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
        }
    }

    [HarmonyPatch(typeof(UIAutopsyWindow), "Hide")]
    internal static class AutopsyWindowHidePatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIAutopsyWindow __instance)
        {
            ExtractAllButton.Clear();
        }
    }
}
