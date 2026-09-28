using System.Collections.Generic;

namespace GK2ExtractAll.Core
{
    // Чистая логика прохода: решает, что извлекать следующим, и считает итог.
    // Цели — ключи предметов (UniqueId) с количеством: одинаковые предметы
    // (например, куски мяса) делят один ключ, поэтому ключ может встречаться
    // несколько раз. Не знает про Unity/игру.
    public sealed class ExtractQueue
    {
        private readonly Dictionary<string, int> _left = new Dictionary<string, int>();

        public int Planned { get; private set; }
        public int Extracted { get; private set; }
        public int Skipped { get; private set; }
        public int Steps { get; private set; }
        public int MaxSteps { get; set; } = 200;

        public void Begin(IEnumerable<string> keys)
        {
            _left.Clear();
            Planned = 0;
            Extracted = 0;
            Skipped = 0;
            Steps = 0;
            if (keys == null) return;
            foreach (var k in keys)
            {
                if (string.IsNullOrEmpty(k)) continue;
                _left[k] = Left(k) + 1;
                Planned++;
            }
        }

        public CellRef TakeNext(IEnumerable<CellRef> present)
        {
            if (present == null) return null;
            foreach (var c in present)
                if (c != null && !string.IsNullOrEmpty(c.Id) && Left(c.Id) > 0)
                    return c;
            return null;
        }

        public void RecordExtracted(string key)
        {
            Steps++;
            Extracted++;
            if (Left(key) > 0) _left[key]--;
        }

        // Не вышло — пропускаем ключ целиком: копии того же предмета упрутся в ту же причину.
        public void RecordSkipped(string key)
        {
            Steps++;
            int left = Left(key);
            Skipped += left > 0 ? left : 1;
            if (!string.IsNullOrEmpty(key)) _left[key] = 0;
        }

        public bool ShouldStop(int currentItemCount)
            => Steps >= MaxSteps
               || currentItemCount <= 0
               || Extracted + Skipped >= Planned;

        private int Left(string key)
            => !string.IsNullOrEmpty(key) && _left.TryGetValue(key, out var n) ? n : 0;
    }
}
