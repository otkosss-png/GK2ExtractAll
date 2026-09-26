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
    public void Takes_present_cells_in_order()
    {
        var q = new ExtractQueue();
        q.Begin(3);
        var cells = Cells("organ:0", "organ:1", "organ:2");
        Assert.Equal("organ:0", q.TakeNext(cells).Id);
        Assert.Equal("organ:1", q.TakeNext(cells).Id);
    }

    [Fact]
    public void Skipped_cell_is_never_taken_again()
    {
        var q = new ExtractQueue();
        q.Begin(2);
        var cells = Cells("organ:0", "organ:1");
        var first = q.TakeNext(cells);
        q.RecordSkipped(first.Id);
        Assert.Equal("organ:1", q.TakeNext(cells).Id);
        Assert.Equal(1, q.Skipped);
    }

    [Fact]
    public void Extracted_cell_is_not_retaken_when_still_present()
    {
        var q = new ExtractQueue();
        q.Begin(2);
        var cells = Cells("organ:0", "organ:1");
        var first = q.TakeNext(cells);
        q.RecordExtracted();
        q.MarkAttempted(first.Id);
        Assert.Equal("organ:1", q.TakeNext(cells).Id);
        Assert.Equal(1, q.Extracted);
    }

    [Fact]
    public void Returns_null_when_nothing_left()
    {
        var q = new ExtractQueue();
        q.Begin(1);
        Assert.Null(q.TakeNext(new List<CellRef>()));
    }

    [Fact]
    public void Stops_when_all_planned_are_done()
    {
        var q = new ExtractQueue();
        q.Begin(1);
        q.RecordExtracted();
        Assert.True(q.ShouldStop(1));
    }

    [Fact]
    public void Stops_on_empty_corpse()
    {
        var q = new ExtractQueue();
        q.Begin(3);
        Assert.True(q.ShouldStop(0));
    }

    [Fact]
    public void Stops_at_max_steps()
    {
        var q = new ExtractQueue();
        q.Begin(1000);
        q.MaxSteps = 2;
        q.RecordSkipped("a");
        q.RecordSkipped("b");
        Assert.True(q.ShouldStop(999));
    }
}
