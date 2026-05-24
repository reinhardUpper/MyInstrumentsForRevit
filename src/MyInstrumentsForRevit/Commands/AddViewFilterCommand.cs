using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Filters;
using MyInstrumentsForRevit.Windows;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AddViewFilterCommand : IExternalCommand
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
            View view = document.ActiveView;
            if (!ViewFilterApplicator.CanUseFilters(view))
            {
                TaskDialog.Show("Фильтры вида", "Активный вид не поддерживает фильтры.");
                return Result.Cancelled;
            }

            if (!FilterCacheService.HasFiltersFor(document))
            {
                FilterCacheService.Refresh(document);
            }

            if (FilterCacheService.Filters.Count == 0)
            {
                TaskDialog.Show("Фильтры вида", "В проекте не найдено существующих фильтров.");
                return Result.Cancelled;
            }

            var window = new FilterSearchWindow(FilterCacheService.Filters);
            bool? result = window.ShowDialog();
            if (result != true)
            {
                return Result.Cancelled;
            }

            FilterItem? selectedFilter = window.SelectedFilter;
            if (selectedFilter == null)
            {
                TaskDialog.Show("Фильтры вида", "Фильтр не выбран.");
                return Result.Cancelled;
            }

            if (!FilterCacheService.Exists(document, selectedFilter))
            {
                TaskDialog.Show("Фильтры вида", "Фильтр был удален или переименован после обновления списка. Обновите список фильтров.");
                return Result.Cancelled;
            }

            using (var transaction = new Transaction(document, "Apply view filter visibility"))
            {
                transaction.Start();
                ViewFilterApplicator.ApplyVisibility(view, selectedFilter.Id, window.MakeVisible);
                transaction.Commit();
            }

            return Result.Succeeded;
        }
    }
}
