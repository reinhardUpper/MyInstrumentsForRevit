using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Forms;
using MyInstrumentsForRevit.Views;
using WinForms = System.Windows.Forms;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DuplicatePlacedFloorPlanToActiveSheetCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document document = commandData.Application.ActiveUIDocument.Document;
            var activeSheet = document.ActiveView as ViewSheet;
            if (activeSheet == null)
            {
                TaskDialog.Show("Копирование вида", "Вы должны находиться на листе.");
                return Result.Cancelled;
            }

            List<PlacedViewOption> placedViews = GetPlacedViews(document);
            if (placedViews.Count == 0)
            {
                TaskDialog.Show(
                    "Копирование вида",
                    "В проекте не найдено размещенных на листах планов этажей, планов несущих конструкций или видов-узлов.");
                return Result.Cancelled;
            }

            using (var form = new ViewSelectForm(placedViews))
            {
                if (form.ShowDialog() != WinForms.DialogResult.OK || form.SelectedOption == null)
                {
                    return Result.Cancelled;
                }

                PlacedViewOption selected = form.SelectedOption;
                if (!selected.View.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
                {
                    TaskDialog.Show("Копирование вида", "Выбранный вид нельзя дублировать с детализацией.");
                    return Result.Cancelled;
                }

                using (var transaction = new Transaction(document, "Copy placed view to active sheet"))
                {
                    transaction.Start();
                    ElementId newViewId = selected.View.Duplicate(ViewDuplicateOption.WithDetailing);
                    Viewport.Create(document, activeSheet.Id, newViewId, selected.Center);
                    transaction.Commit();
                }
            }

            return Result.Succeeded;
        }

        private static List<PlacedViewOption> GetPlacedViews(Document document)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Select(viewport => CreateOption(document, viewport))
                .Where(option => option != null)
                .Cast<PlacedViewOption>()
                .OrderBy(option => option.ToString(), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static PlacedViewOption? CreateOption(Document document, Viewport viewport)
        {
            View? view = document.GetElement(viewport.ViewId) as View;
            if (view == null || !IsSupportedViewType(view.ViewType))
            {
                return null;
            }

            ViewSheet? sheet = document.GetElement(viewport.OwnerViewId) as ViewSheet;
            string sheetNumber = sheet == null ? "No sheet" : sheet.SheetNumber;
            string sheetName = sheet == null ? string.Empty : sheet.Name;
            return new PlacedViewOption(view, viewport.GetBoxCenter(), sheetNumber, sheetName);
        }

        private static bool IsSupportedViewType(ViewType viewType)
        {
            return viewType == ViewType.FloorPlan
                || viewType == ViewType.EngineeringPlan
                || viewType == ViewType.Detail;
        }
    }
}
