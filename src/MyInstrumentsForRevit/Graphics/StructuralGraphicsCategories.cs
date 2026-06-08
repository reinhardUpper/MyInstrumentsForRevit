using Autodesk.Revit.DB;

namespace MyInstrumentsForRevit.Graphics
{
    internal static class StructuralGraphicsCategories
    {
        public static readonly BuiltInCategory[] MainCategories =
        {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralColumns
        };

        public static readonly BuiltInCategory[] RebarCategories =
        {
            BuiltInCategory.OST_Rebar,
            BuiltInCategory.OST_AreaRein
        };

        public static readonly BuiltInCategory[] LinkAndImportCategories =
        {
            BuiltInCategory.OST_RvtLinks,
            BuiltInCategory.OST_ImportObjectStyles
        };
    }
}
