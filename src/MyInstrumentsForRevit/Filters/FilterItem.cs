using Autodesk.Revit.DB;

namespace MyInstrumentsForRevit.Filters
{
    public sealed class FilterItem
    {
        public FilterItem(ElementId id, string name)
        {
            Id = id;
            Name = name;
        }

        public ElementId Id { get; }

        public string Name { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}

