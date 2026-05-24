using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DuplicateSelectedLegendsAndSchedulesCommand : IExternalCommand
    {
        private const double OffsetMillimeters = 30.0;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            Document document = uiDocument.Document;
            ICollection<ElementId> selectedIds = uiDocument.Selection.GetElementIds();

            if (selectedIds == null || selectedIds.Count == 0)
            {
                TaskDialog.Show("Копирование", "Выделите легенды или спецификации на листе.");
                return Result.Cancelled;
            }

            double offsetFeet = UnitUtils.ConvertToInternalUnits(OffsetMillimeters, UnitTypeId.Millimeters);
            int createdCount = 0;
            var errors = new List<string>();

            using (var transaction = new Transaction(document, "Duplicate legends and schedules"))
            {
                transaction.Start();

                foreach (ElementId id in selectedIds)
                {
                    Element element = document.GetElement(id);
                    if (element == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (TryDuplicateSchedule(document, element, offsetFeet))
                        {
                            createdCount++;
                            continue;
                        }

                        if (TryDuplicateLegend(document, element, offsetFeet))
                        {
                            createdCount++;
                            continue;
                        }
                    }
                    catch (Exception exception)
                    {
                        errors.Add("Element Id " + id.IntegerValue + ": " + exception.Message);
                    }
                }

                transaction.Commit();
            }

            string result = "Готово!\nСоздано копий: " + createdCount;
            if (errors.Count > 0)
            {
                result += "\n\nЗамечания: " + errors.Count;
            }

            TaskDialog.Show("Результат", result);
            return Result.Succeeded;
        }

        private static bool TryDuplicateSchedule(Document document, Element element, double offsetFeet)
        {
            var scheduleInstance = element as ScheduleSheetInstance;
            if (scheduleInstance == null)
            {
                return false;
            }

            View? sourceView = document.GetElement(scheduleInstance.ScheduleId) as View;
            ViewSheet? sheet = document.GetElement(scheduleInstance.OwnerViewId) as ViewSheet;
            if (sourceView == null || sheet == null || !sourceView.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
            {
                return false;
            }

            ElementId newViewId = sourceView.Duplicate(ViewDuplicateOption.Duplicate);
            XYZ point = scheduleInstance.Point;
            var newPoint = new XYZ(point.X + offsetFeet, point.Y, point.Z);
            ScheduleSheetInstance.Create(document, sheet.Id, newViewId, newPoint);
            return true;
        }

        private static bool TryDuplicateLegend(Document document, Element element, double offsetFeet)
        {
            var viewport = element as Viewport;
            if (viewport == null)
            {
                return false;
            }

            View? sourceView = document.GetElement(viewport.ViewId) as View;
            ViewSheet? sheet = document.GetElement(viewport.OwnerViewId) as ViewSheet;
            if (sourceView == null || sheet == null || sourceView.ViewType != ViewType.Legend)
            {
                return false;
            }

            if (!sourceView.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
            {
                return false;
            }

            ElementId newViewId = sourceView.Duplicate(ViewDuplicateOption.WithDetailing);
            XYZ center = viewport.GetBoxCenter();
            var newCenter = new XYZ(center.X + offsetFeet, center.Y, center.Z);
            Viewport.Create(document, sheet.Id, newViewId, newCenter);
            return true;
        }
    }
}
