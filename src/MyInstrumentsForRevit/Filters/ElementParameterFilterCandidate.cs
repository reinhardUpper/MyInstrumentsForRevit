using Autodesk.Revit.DB;

namespace MyInstrumentsForRevit.Filters
{
    public sealed class ElementParameterFilterCandidate
    {
        public ElementParameterFilterCandidate(Parameter parameter)
        {
            Parameter = parameter;
            Name = parameter.Definition?.Name ?? "<Без имени>";
            StorageType = parameter.StorageType;
            DisplayValue = GetDisplayValue(parameter);
            SearchText = (Name + " " + DisplayValue).ToLowerInvariant();
        }

        public Parameter Parameter { get; }

        public string Name { get; }

        public string DisplayValue { get; }

        public StorageType StorageType { get; }

        public string SearchText { get; }

        public string StorageTypeText => StorageType.ToString();

        private static string GetDisplayValue(Parameter parameter)
        {
            string value = parameter.AsValueString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (parameter.StorageType == StorageType.String)
            {
                return parameter.AsString() ?? string.Empty;
            }

            if (parameter.StorageType == StorageType.Integer)
            {
                return parameter.AsInteger().ToString();
            }

            if (parameter.StorageType == StorageType.Double)
            {
                return parameter.AsDouble().ToString("G6");
            }

            if (parameter.StorageType == StorageType.ElementId)
            {
                return parameter.AsElementId().IntegerValue.ToString();
            }

            return string.Empty;
        }
    }
}
