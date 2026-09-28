using System.Collections.Generic;
using GK2ExtractAll.Core;
using Xunit;

public class QueueTests
{
    private static List<CellRef> Cells(params string[] ids)
    {
        var list = new List<CellRef>();
        foreach (var id in ids) list.Add(new CellRef(id, CellKind.Organ));
        return list;
    }

    [Fact]
    public void Takes_present_targets_in_order()
    {
        var q = new ExtractQueue();
        q.Begin(new[] { "a", "b" });
        var cells = Cells("a", "b");
        Assert.Equal("a", q.TakeNext(cells).Id);
        q.RecordExtracted("a");
        Assert.Equal("b", q.TakeNext(Cells("b")).Id);
    }

    [Fact]
    public void Ignores_present_items_that_are_not_targets()
    {
        var q = new ExtractQueue();
        q.Begin(new[] { "b" });
        Assert.Equal("b", q.TakeNext(Cells("a", "b")).Id);
    }

    [Fact]
    public void Same_key_counts_as_several_targets()
    {
        // Три куска мяса с одинаковым UniqueId: вырезать надо все три.
        var q = new ExtractQueue();
        q.Begin(new[] { "bone", "meat", "meat", "meat" });
        Assert.Equal(4, q.Planned);
        q.RecordExtracted("bone");
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal("meat", q.TakeNext(Cells("meat", "meat", "meat")).Id);
            q.RecordExtracted("meat");
        }
        Assert.Equal(4, q.Extracted);
        Assert.True(q.ShouldStop(1));
    }

    [Fact]
    public void Extra_copies_beyond_selection_are_not_taken()
    {
        // Отмечено 1 мясо из 3 — после него остальные не трогаем.
        var q = new ExtractQueue();
        q.Begin(new[] { "meat" });
        q.TakeNext(Cells("meat", "meat", "meat"));
        q.RecordExtracted("meat");
        Assert.Null(q.TakeNext(Cells("meat", "meat")));
    }

    [Fact]
    public void Skipped_key_skips_all_its_copies()
    {
        var q = new ExtractQueue();
        q.Begin(new[] { "meat", "meat", "bone" });
        q.RecordSkipped("meat");
        Assert.Equal(2, q.Skipped);
        Assert.Equal("bone", q.TakeNext(Cells("meat", "meat", "bone")).Id);
    }

    [Fact]
    public void Returns_null_when_nothing_left()
    {
        var q = new ExtractQueue();
        q.Begin(new[] { "a" });
        Assert.Null(q.TakeNext(new List<CellRef>()));
    }

    [Fact]
    public void Stops_when_all_planned_are_done()
    {
        var q = new ExtractQueue();
        q.Begin(new[] { "a" });
        q.RecordExtracted("a");
        Assert.True(q.ShouldStop(1));
    }

    [Fact]
    public void Stops_on_empty_corpse()
    {
        var q = new ExtractQueue();
        q.Begin(new[] { "a", "b", "c" });
        Assert.True(q.ShouldStop(0));
    }

    [Fact]
    public void Stops_at_max_steps()
    {
        var q = new ExtractQueue();
        var keys = new List<string>();
        for (int i = 0; i < 1000; i++) keys.Add("k" + i);
        q.Begin(keys);
        q.MaxSteps = 2;
        q.RecordSkipped("k0");
        q.RecordSkipped("k1");
        Assert.True(q.ShouldStop(999));
    }
}
