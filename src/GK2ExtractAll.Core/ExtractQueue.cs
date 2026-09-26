using System.Collections.Generic;

namespace GK2ExtractAll.Core
{
    // Чистая логика прохода по ячейкам: решает, что извлекать следующим,
    // и считает итог. Не знает про Unity/игру.
    public sealed class ExtractQueue
    {
        private readonly HashSet<string> _attempted = new HashSet<string>();

        public int Planned { get; private set; }
        public int Extracted { get; private set; }
        public int Skipped { get; private set; }
        public int Steps { get; private set; }
        public int MaxSteps { get; set; } = 200;

        public void Begin(int itemCount)
        {
            _attempted.Clear();
            Planned = itemCount;
            Extracted = 0;
            Skipped = 0;
            Steps = 0;
        }

        public CellRef TakeNext(IEnumerable<CellRef> present)
        {
            if (present == null) return null;
            foreach (var c in present)
                if (c != null && !string.IsNullOrEmpty(c.Id) && !_attempted.Contains(c.Id))
                {
                    // Взятая ячейка помечается как обработанная, чтобы её нельзя было
                    // взять повторно, даже если вызывающий забыл вызвать MarkAttempted.
                    MarkAttempted(c.Id);
                    return c;
                }
            return null;
        }

        public void MarkAttempted(string id)
        {
            if (!string.IsNullOrEmpty(id)) _attempted.Add(id);
        }

        public void RecordExtracted()
        {
            Steps++;
            Extracted++;
        }

        public void RecordSkipped(string id)
        {
            Steps++;
            Skipped++;
            MarkAttempted(id);
        }

        public bool ShouldStop(int currentItemCount)
            => Steps >= MaxSteps
               || currentItemCount <= 0
               || Extracted + Skipped >= Planned;
    }
}
