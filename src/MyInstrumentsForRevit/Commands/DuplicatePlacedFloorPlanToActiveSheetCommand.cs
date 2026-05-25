using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DuplicatePlacedFloorPlanToActiveSheetCommand : IExternalCommand
    {
        private const double OffsetXMillimeters = 80.0;
        private const double OffsetYMillimeters = 55.0;
        private const int Columns = 3;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            Document document = uiDocument.Document;
            ViewSheet? activeSheet = GetCurrentSheet(document, uiDocument);
            if (activeSheet == null)
            {
                TaskDialog.Show("Копирование вида", "Откройте лист и повторите команду.");
                return Result.Cancelled;
            }

            List<View> selectedViews = GetSelectedViews(document, uiDocument)
                .Where(view => IsSupportedViewType(view.ViewType))
                .OrderBy(view => view.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (selectedViews.Count == 0)
            {
                TaskDialog.Show(
                    "Копирование вида",
                    "Выделите один или несколько видов в диспетчере проекта: план, план конструкций, узел, разрез или чертежный вид.");
                return Result.Cancelled;
            }

            XYZ basePoint = GetSheetCenter(activeSheet);
            double offsetX = UnitUtils.ConvertToInternalUnits(OffsetXMillimeters, UnitTypeId.Millimeters);
            double offsetY = UnitUtils.ConvertToInternalUnits(OffsetYMillimeters, UnitTypeId.Millimeters);
            int createdCount = 0;
            var errors = new List<string>();

            using (var transaction = new Transaction(document, "Copy selected views to active sheet"))
            {
                transaction.Start();

                foreach (View sourceView in selectedViews)
                {
                    try
                    {
                        if (!sourceView.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
                        {
                            errors.Add(sourceView.Name + ": вид нельзя дублировать с детализацией.");
                            continue;
                        }

                        ElementId newViewId = sourceView.Duplicate(ViewDuplicateOption.WithDetailing);
                        if (!Viewport.CanAddViewToSheet(document, activeSheet.Id, newViewId))
                        {
                            errors.Add(sourceView.Name + ": копию нельзя разместить на текущем листе.");
                            continue;
                        }

                        XYZ point = GetPlacementPoint(basePoint, createdCount, offsetX, offsetY);
                        Viewport.Create(document, activeSheet.Id, newViewId, point);
                        createdCount++;
                    }
                    catch (Exception exception)
                    {
                        errors.Add(sourceView.Name + ": " + exception.Message);
                    }
                }

                transaction.Commit();
            }

            if (createdCount == 0)
            {
                TaskDialog.Show("Копирование вида", "Не удалось создать копии выбранных видов.\n\n" + string.Join("\n", errors.Take(8)));
                return Result.Cancelled;
            }

            if (errors.Count > 0)
            {
                TaskDialog.Show("Копирование вида", "Создано копий: " + createdCount + "\nЗамечания: " + errors.Count);
            }

            return Result.Succeeded;
        }

        private static IEnumerable<View> GetSelectedViews(Document document, UIDocument uiDocument)
        {
            return uiDocument.Selection.GetElementIds()
                .Select(document.GetElement)
                .OfType<View>()
                .Where(view => !view.IsTemplate);
        }

        private static ViewSheet? GetCurrentSheet(Document document, UIDocument uiDocument)
        {
            if (document.ActiveView is ViewSheet activeSheet)
            {
                return activeSheet;
            }

            foreach (UIView uiView in uiDocument.GetOpenUIViews())
            {
                if (document.GetElement(uiView.ViewId) is ViewSheet sheet)
                {
                    return sheet;
                }
            }

            return null;
        }

        private static XYZ GetSheetCenter(ViewSheet sheet)
        {
            BoundingBoxUV outline = sheet.Outline;
            double x = (outline.Min.U + outline.Max.U) / 2.0;
            double y = (outline.Min.V + outline.Max.V) / 2.0;
            return new XYZ(x, y, 0);
        }

        private static XYZ GetPlacementPoint(XYZ basePoint, int index, double offsetX, double offsetY)
        {
            int column = index % Columns;
            int row = index / Columns;
            return new XYZ(basePoint.X + column * offsetX, basePoint.Y - row * offsetY, basePoint.Z);
        }

        private static bool IsSupportedViewType(ViewType viewType)
        {
            return viewType == ViewType.FloorPlan
                || viewType == ViewType.EngineeringPlan
                || viewType == ViewType.Detail
                || viewType == ViewType.Section
                || viewType == ViewType.DraftingView;
        }
    }
}
