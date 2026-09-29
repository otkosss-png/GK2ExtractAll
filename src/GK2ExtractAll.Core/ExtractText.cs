namespace GK2ExtractAll.Core
{
    // Надписи мода. Сами строки — в TextPack (встроенные en/ru + Localization\<код>.json).
    public static class ExtractText
    {
        public static string Button(TextPack t) => t.Get("button");

        public static string SelectButton(TextPack t) => t.Get("select");

        public static string RunSelected(TextPack t) => t.Get("run_selected");

        public static string Counter(TextPack t, int selected, int total) => t.Format("counter", selected, total);

        public static string Summary(TextPack t, int extracted, int total) => t.Format("summary", extracted, total);

        public static string Nothing(TextPack t) => t.Get("nothing");
    }
}
