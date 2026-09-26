using GK2ExtractAll.Core;
using Xunit;

public class TextTests
{
    [Fact] public void Button_en() => Assert.Equal("Extract all", ExtractText.Button(Lang.En));
    [Fact] public void Summary_en() => Assert.Equal("Extracted 3 of 8", ExtractText.Summary(Lang.En, 3, 8));
    [Fact] public void Summary_ru() => Assert.Equal("Извлечено 3 из 8", ExtractText.Summary(Lang.Ru, 3, 8));
    [Fact] public void Nothing_ru() => Assert.Equal("Нечего извлекать", ExtractText.Nothing(Lang.Ru));
}
