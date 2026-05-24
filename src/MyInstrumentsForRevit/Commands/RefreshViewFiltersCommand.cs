using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Filters;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RefreshViewFiltersCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument == null)
            {
                TaskDialog.Show("Фильтры вида", "Нет активного документа.");
                return Result.Cancelled;
            }

            Document document = uiDocument.Document;
            var filters = FilterCacheService.Refresh(document);
            TaskDialog.Show("Фильтры вида", "Список фильтров обновлен.\nНайдено фильтров: " + filters.Count);
            return Result.Succeeded;
        }
    }
}
