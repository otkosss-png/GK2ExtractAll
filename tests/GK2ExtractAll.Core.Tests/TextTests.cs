using System.Collections.Generic;
using GK2ExtractAll.Core;
using Xunit;

public class TextTests
{
    private static readonly TextPack En = TextPack.Builtin("en");
    private static readonly TextPack Ru = TextPack.Builtin("ru");

    [Fact] public void Button_en() => Assert.Equal("Extract all", ExtractText.Button(En));
    [Fact] public void Summary_en() => Assert.Equal("Extracted 3 of 8", ExtractText.Summary(En, 3, 8));
    [Fact] public void Summary_ru() => Assert.Equal("Извлечено 3 из 8", ExtractText.Summary(Ru, 3, 8));
    [Fact] public void Nothing_ru() => Assert.Equal("Нечего извлекать", ExtractText.Nothing(Ru));
    [Fact] public void Counter_ru() => Assert.Equal("Выбрано: 2 из 5", ExtractText.Counter(Ru, 2, 5));
    [Fact] public void Counter_en() => Assert.Equal("Selected: 0 of 3", ExtractText.Counter(En, 0, 3));

    [Fact]
    public void Unknown_builtin_language_is_english()
        => Assert.Equal("Extract all", ExtractText.Button(TextPack.Builtin("de")));

    [Fact]
    public void Translation_file_overrides_builtin()
    {
        var de = TextPack.Create("de", new Dictionary<string, string>
        {
            { "button", "Alles entnehmen" },
            { "summary", "{0} von {1} entnommen" },
        });
        Assert.Equal("de", de.Code);
        Assert.Equal("Alles entnehmen", ExtractText.Button(de));
        Assert.Equal("3 von 8 entnommen", ExtractText.Summary(de, 3, 8));
    }

    [Fact]
    public void Missing_or_empty_key_falls_back_to_english()
    {
        var de = TextPack.Create("de", new Dictionary<string, string> { { "button", "" } });
        Assert.Equal("Extract all", ExtractText.Button(de));
        Assert.Equal("Select...", ExtractText.SelectButton(de));
    }

    [Fact]
    public void Missing_key_in_russian_file_falls_back_to_builtin_russian()
    {
        var ru = TextPack.Create("ru", new Dictionary<string, string> { { "button", "Всё наружу" } });
        Assert.Equal("Всё наружу", ExtractText.Button(ru));
        Assert.Equal("Вырезать", ExtractText.RunSelected(ru));
    }

    [Fact]
    public void Broken_format_falls_back_to_english_template()
    {
        var de = TextPack.Create("de", new Dictionary<string, string> { { "summary", "{0} von {5}" } });
        Assert.Equal("Extracted 3 of 8", ExtractText.Summary(de, 3, 8));
    }

    [Fact]
    public void Null_overrides_are_allowed()
        => Assert.Equal("Extract all", ExtractText.Button(TextPack.Create("xx", null)));

    [Fact]
    public void Builtin_keys_cover_every_text()
    {
        foreach (var key in TextPack.Keys)
        {
            Assert.False(string.IsNullOrEmpty(TextPack.Builtin("en").Get(key)));
            Assert.False(string.IsNullOrEmpty(TextPack.Builtin("ru").Get(key)));
        }
    }
}
