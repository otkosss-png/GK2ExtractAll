namespace GK2ExtractAll.Core
{
    public enum CellKind { Organ, Pocket }

    public sealed class CellRef
    {
        public readonly string Id;
        public readonly CellKind Kind;

        public CellRef(string id, CellKind kind)
        {
            Id = id;
            Kind = kind;
        }
    }
}
