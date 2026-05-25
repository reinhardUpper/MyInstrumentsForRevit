using Autodesk.Revit.Attributes;

namespace MyRevitTools.DimensionQuickCommands
{
    [Transaction(TransactionMode.Manual)]
    public class DimensionQuickCommandSlot04 : BaseDimensionQuickCommandSlotCommand
    {
        protected override int SlotNumber => 4;
    }
}
