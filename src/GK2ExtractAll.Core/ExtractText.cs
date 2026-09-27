namespace GK2ExtractAll.Core
{
    public enum Lang { En, Ru }

    public static class ExtractText
    {
        public static string Button(Lang lang)
            => lang == Lang.Ru ? "Извлечь всё" : "Extract all";

        public static string Summary(Lang lang, int extracted, int total)
            => lang == Lang.Ru
                ? "Извлечено " + extracted + " из " + total
                : "Extracted " + extracted + " of " + total;

        public static string Nothing(Lang lang)
            => lang == Lang.Ru ? "Нечего извлекать" : "Nothing to extract";

        public static string SelectButton(Lang lang)
            => lang == Lang.Ru ? "Выбрать…" : "Select...";

        public static string PanelTitle(Lang lang)
            => lang == Lang.Ru ? "Что вырезать" : "What to extract";

        public static string GroupOrgans(Lang lang) => lang == Lang.Ru ? "ОРГАНЫ" : "ORGANS";
        public static string GroupPockets(Lang lang) => lang == Lang.Ru ? "КАРМАНЫ" : "POCKETS";

        public static string PresetName(Lang lang, ExtractPreset preset)
        {
            switch (preset)
            {
                case ExtractPreset.All: return lang == Lang.Ru ? "Всё" : "All";
                case ExtractPreset.Organs: return lang == Lang.Ru ? "Только органы" : "Organs only";
                case ExtractPreset.Pockets: return lang == Lang.Ru ? "Только карманы" : "Pockets only";
                default: return lang == Lang.Ru ? "Ничего" : "None";
            }
        }

        public static string RunSelected(Lang lang)
            => lang == Lang.Ru ? "Вырезать" : "Extract";

        public static string CloseButton(Lang lang)
            => lang == Lang.Ru ? "Закрыть" : "Close";

        public static string Counter(Lang lang, int selected, int total)
            => lang == Lang.Ru
                ? "Выбрано: " + selected + " из " + total
                : "Selected: " + selected + " of " + total;

        public static string NothingToSelect(Lang lang)
            => lang == Lang.Ru ? "В трупе ничего нет" : "Nothing to extract from this body";

        public static string Hints(Lang lang)
            => lang == Lang.Ru
                ? "Мышь: клик — галочка. Геймпад: D-pad — перемещение, A — переключить, B/Esc — закрыть"
                : "Mouse: click to tick. Gamepad: D-pad to move, A to toggle, B/Esc to close";
    }
}
