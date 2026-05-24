using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevitTools.DimensionQuickCommands
{
    [Transaction(TransactionMode.Manual)]
    public abstract class BaseDimensionQuickCommandSlotCommand : IExternalCommand
    {
        protected abstract int SlotNumber { get; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var configs = DimensionQuickCommandStorage.Load();
            DimensionQuickCommandConfig? config = configs.FirstOrDefault(item => item.SlotNumber == SlotNumber);
            if (config == null)
            {
                TaskDialog.Show("\u0411\u044B\u0441\u0442\u0440\u044B\u0439 \u0440\u0430\u0437\u043C\u0435\u0440", $"\u0414\u043B\u044F \u0441\u043B\u043E\u0442\u0430 \u0411\u041A{SlotNumber} \u043A\u043E\u043C\u0430\u043D\u0434\u0430 \u043D\u0435 \u043D\u0430\u0437\u043D\u0430\u0447\u0435\u043D\u0430.");
                return Result.Cancelled;
            }

            return DimensionQuickCommandExecutor.Execute(commandData.Application, config);
        }
    }
}
