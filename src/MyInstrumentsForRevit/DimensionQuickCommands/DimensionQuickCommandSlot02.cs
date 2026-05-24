using Autodesk.Revit.Attributes;

namespace MyRevitTools.DimensionQuickCommands
{
    [Transaction(TransactionMode.Manual)]
    public class DimensionQuickCommandSlot02 : BaseDimensionQuickCommandSlotCommand
    {
        protected override int SlotNumber => 2;
    }
}
