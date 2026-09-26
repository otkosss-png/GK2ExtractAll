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
    }
}
