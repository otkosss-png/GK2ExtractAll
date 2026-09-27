using System;
using System.Collections.Generic;
using GK2ExtractAll.Core;
using HarmonyLib;
using LazyBearTechnology;

namespace GK2ExtractAll
{
    // Нативные геймпад-шорткаты окна вскрытия (игра раздаёт их через
    // LazyWindow<T>.GetGameKeyDelegates() -> Dictionary<GameKey, Func<bool>>):
    //   1) «Извлечь всё»;
    //   2) «Вырезать выбранное» (значки-галочки на ячейках);
    //   3) переключить значок ячейки, на которой фокус (как «пин» в Recipe Pin).
    // Ключи ставим только если окно их не использует (иначе затрём игровое действие).
    [HarmonyPatch(typeof(UIAutopsyWindow), "GetGameKeyDelegates")]
    internal static class AutopsyWindowGameKeysPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIAutopsyWindow __instance,
            Dictionary<GameKey, Func<bool>> __result)
        {
            try
            {
                if (__result == null || __instance == null) return;

                // F (= «переместить всё») — если выбор активен, вырезаем ТОЛЬКО отмеченное.
                TryAdd(__result, GameKey.MoveAllItemsToPlayer, () =>
                {
                    if (ExtractMarks.ModeOn) ExtractMarks.RunSelected(__instance);
                    else ExtractRunner.Start(__instance);
                    return true;
                });

                TryAdd(__result, GameKey.MoveAllItemsFromPlayer, () =>
                {
                    if (ExtractMarks.ModeOn) ExtractMarks.RunSelected(__instance);
                    else ExtractMarks.EnterMode(__instance);
                    return true;
                });

                TryAdd(__result, GameKey.RightBumper, () => ExtractMarks.ToggleFocused());
            }
            catch (Exception ex) { Plugin.Log.LogWarning("game keys: " + ex.Message); }
        }

        private static void TryAdd(Dictionary<GameKey, Func<bool>> dict, GameKey key, Func<bool> action)
        {
            if (key == null || dict.ContainsKey(key)) return;
            dict[key] = action;
            Plugin.Log.LogInfo("game keys: registered controller shortcut " + key);
        }
    }
}
