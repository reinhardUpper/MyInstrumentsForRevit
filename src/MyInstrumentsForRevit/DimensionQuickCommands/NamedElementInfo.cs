using Autodesk.Revit.DB;

namespace MyRevitTools.DimensionQuickCommands
{
    public class NamedElementInfo
    {
        public ElementId Id { get; set; } = ElementId.InvalidElementId;

        public int IntegerId { get; set; }

        public string UniqueId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
