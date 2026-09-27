using System.Collections.Generic;
using GK2ExtractAll.Core;
using Xunit;

namespace GK2ExtractAll.Core.Tests
{
    public class SelectionTests
    {
        private static List<ExtractEntry> Body() => new List<ExtractEntry>
        {
            new ExtractEntry { Id = "organ:0", Category = ExtractCategory.Organ },
            new ExtractEntry { Id = "organ:1", Category = ExtractCategory.Organ },
            new ExtractEntry { Id = "pocket:0", Category = ExtractCategory.Pocket },
            new ExtractEntry { Id = "pocket:1", Category = ExtractCategory.Pocket },
        };

        [Fact] public void All_preset_selects_everything()
        {
            var s = new ExtractSelection();
            s.Begin(Body(), ExtractPreset.All);
            Assert.Equal(4, s.SelectedCount);
            Assert.True(s.IsSelected("organ:0"));
            Assert.True(s.IsSelected("pocket:1"));
        }

        [Fact] public void Organs_preset_selects_only_organs()
        {
            var s = new ExtractSelection();
            s.Begin(Body(), ExtractPreset.Organs);
            Assert.Equal(2, s.SelectedCount);
            Assert.True(s.IsSelected("organ:1"));
            Assert.False(s.IsSelected("pocket:0"));
        }

        [Fact] public void Pockets_preset_selects_only_pockets()
        {
            var s = new ExtractSelection();
            s.Begin(Body(), ExtractPreset.Pockets);
            Assert.Equal(2, s.SelectedCount);
            Assert.False(s.IsSelected("organ:0"));
            Assert.True(s.IsSelected("pocket:0"));
        }

        [Fact] public void None_preset_selects_nothing()
        {
            var s = new ExtractSelection();
            s.Begin(Body(), ExtractPreset.None);
            Assert.Equal(0, s.SelectedCount);
        }

        [Fact] public void Toggle_flips_selection()
        {
            var s = new ExtractSelection();
            s.Begin(Body(), ExtractPreset.None);
            s.Toggle("organ:0");
            Assert.True(s.IsSelected("organ:0"));
            s.Toggle("organ:0");
            Assert.False(s.IsSelected("organ:0"));
        }

        [Fact] public void Toggle_unknown_id_is_ignored()
        {
            var s = new ExtractSelection();
            s.Begin(Body(), ExtractPreset.None);
            s.Toggle("nope");
            Assert.Equal(0, s.SelectedCount);
        }

        [Fact] public void SetAll_toggles_everything()
        {
            var s = new ExtractSelection();
            s.Begin(Body(), ExtractPreset.None);
            s.SetAll(true);
            Assert.Equal(4, s.SelectedCount);
            s.SetAll(false);
            Assert.Equal(0, s.SelectedCount);
        }

        [Fact] public void Ids_keep_list_order_and_duplicates_are_dropped()
        {
            var list = Body();
            list.Add(new ExtractEntry { Id = "organ:0", Category = ExtractCategory.Organ });
            var s = new ExtractSelection();
            s.Begin(list, ExtractPreset.All);
            Assert.Equal(new[] { "organ:0", "organ:1", "pocket:0", "pocket:1" }, s.Ids);
        }

        [Fact] public void Empty_body_has_no_entries()
        {
            var s = new ExtractSelection();
            s.Begin(new List<ExtractEntry>(), ExtractPreset.All);
            Assert.Equal(0, s.Count);
            Assert.Equal(0, s.SelectedCount);
        }

        [Fact] public void Preset_names_localized()
        {
            Assert.Equal("Всё", ExtractText.PresetName(Lang.Ru, ExtractPreset.All));
            Assert.Equal("Organs only", ExtractText.PresetName(Lang.En, ExtractPreset.Organs));
            Assert.Equal("Только карманы", ExtractText.PresetName(Lang.Ru, ExtractPreset.Pockets));
            Assert.Equal("None", ExtractText.PresetName(Lang.En, ExtractPreset.None));
        }

        [Fact] public void Counter_text_localized()
        {
            Assert.Equal("Выбрано: 2 из 5", ExtractText.Counter(Lang.Ru, 2, 5));
            Assert.Equal("Selected: 0 of 3", ExtractText.Counter(Lang.En, 0, 3));
        }
    }
}
