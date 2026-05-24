using Autodesk.Revit.Attributes;

namespace MyRevitTools.DimensionQuickCommands
{
    [Transaction(TransactionMode.Manual)]
    public class DimensionQuickCommandSlot01 : BaseDimensionQuickCommandSlotCommand
    {
        protected override int SlotNumber => 1;
    }
}
