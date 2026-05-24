using Autodesk.Revit.DB;

namespace MyInstrumentsForRevit.Views
{
    internal sealed class PlacedViewOption
    {
        public PlacedViewOption(View view, XYZ center, string sheetNumber, string sheetName)
        {
            View = view;
            Center = center;
            SheetNumber = sheetNumber;
            SheetName = sheetName;
        }

        public View View { get; }

        public XYZ Center { get; }

        public string SheetNumber { get; }

        public string SheetName { get; }

        public override string ToString()
        {
            return View.Name + "  |  " + SheetNumber + " - " + SheetName;
        }
    }
}

