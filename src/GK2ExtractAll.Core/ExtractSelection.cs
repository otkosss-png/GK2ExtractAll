using System;
using System.Collections.Generic;

namespace GK2ExtractAll.Core
{
    // Что за предмет в списке выбора: орган или карман.
    public enum ExtractCategory { Organ, Pocket }

    public enum ExtractPreset { All, Organs, Pockets, None }

    public sealed class ExtractEntry
    {
        public string Id;
        public ExtractCategory Category;
        // Подпись для UI (имя предмета из игры); Core её не использует.
        public string Label;
    }

    // Выбор того, что вырезать из трупа. Логика чистая (без Unity): состав списка
    // фиксируется Begin, дальше галочки переключаются/сбрасываются пресетами.
    public sealed class ExtractSelection
    {
        private readonly List<string> _ids = new List<string>();
        private readonly Dictionary<string, ExtractCategory> _categories =
            new Dictionary<string, ExtractCategory>(StringComparer.Ordinal);
        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.Ordinal);

        public void Begin(IEnumerable<ExtractEntry> entries, ExtractPreset preset)
        {
            _ids.Clear();
            _categories.Clear();
            _selected.Clear();
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.Id) || _categories.ContainsKey(e.Id)) continue;
                    _ids.Add(e.Id);
                    _categories[e.Id] = e.Category;
                }
            }
            ApplyPreset(preset);
        }

        public IReadOnlyList<string> Ids => _ids;
        public int Count => _ids.Count;
        public int SelectedCount => _selected.Count;

        public bool IsSelected(string id) => id != null && _selected.Contains(id);

        public void Toggle(string id)
        {
            if (id == null || !_categories.ContainsKey(id)) return;
            if (!_selected.Add(id)) _selected.Remove(id);
        }

        public void SetAll(bool selected)
        {
            _selected.Clear();
            if (!selected) return;
            foreach (var id in _ids) _selected.Add(id);
        }

        public void ApplyPreset(ExtractPreset preset)
        {
            if (preset == ExtractPreset.All) { SetAll(true); return; }
            if (preset == ExtractPreset.None) { SetAll(false); return; }

            _selected.Clear();
            var want = preset == ExtractPreset.Organs ? ExtractCategory.Organ : ExtractCategory.Pocket;
            foreach (var id in _ids)
                if (_categories[id] == want) _selected.Add(id);
        }
    }
}
