using Autodesk.Revit.Attributes;

namespace MyRevitTools.DimensionQuickCommands
{
    [Transaction(TransactionMode.Manual)]
    public class DimensionQuickCommandSlot03 : BaseDimensionQuickCommandSlotCommand
    {
        protected override int SlotNumber => 3;
    }
}
