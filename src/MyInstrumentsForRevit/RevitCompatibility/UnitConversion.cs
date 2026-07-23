using Autodesk.Revit.DB;

namespace MyInstrumentsForRevit.RevitCompatibility
{
    internal static class UnitConversion
    {
        public static double MillimetersToInternalUnits(double value)
        {
#pragma warning disable CS0618
            // Revit 2019 does not contain UnitTypeId, so the add-in must keep the legacy unit API here.
            return UnitUtils.ConvertToInternalUnits(value, DisplayUnitType.DUT_MILLIMETERS);
#pragma warning restore CS0618
        }
    }
}
