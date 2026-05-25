using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DuplicateActiveSheetCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document document = commandData.Application.ActiveUIDocument.Document;
            return Duplicate(document);
        }

        public static Result Duplicate(Document document)
        {
            var sourceSheet = document.ActiveView as ViewSheet;
            if (sourceSheet == null)
            {
                TaskDialog.Show("Дублировать активный лист", "Активный вид не является листом.");
                return Result.Cancelled;
            }

            var errors = new List<string>();
            string newSheetNumber = GetNextSheetNumber(document, sourceSheet.SheetNumber);
            ViewSheet? newSheet = null;

            using (var transaction = new Transaction(document, "Duplicate active sheet"))
            {
                transaction.Start();

                FamilyInstance? sourceTitleBlock = GetTitleBlock(document, sourceSheet);
                ElementId titleBlockTypeId = sourceTitleBlock?.GetTypeId() ?? ElementId.InvalidElementId;
                newSheet = ViewSheet.Create(document, titleBlockTypeId);
                newSheet.SheetNumber = newSheetNumber;
                newSheet.Name = sourceSheet.Name + " - копия";

                CopyWritableParameters(sourceSheet, newSheet, errors, "Параметры листа");

                FamilyInstance? newTitleBlock = GetTitleBlock(document, newSheet);
                if (sourceTitleBlock != null && newTitleBlock != null)
                {
                CopyWritableParameters(sourceTitleBlock, newTitleBlock, errors, "Параметры основной надписи");
                }

                CopyTextNotes(document, sourceSheet, newSheet, errors);
                CopyViewports(document, sourceSheet, newSheet, errors);
                CopySchedules(document, sourceSheet, newSheet, errors);

                transaction.Commit();
            }

            if (newSheet == null)
            {
                TaskDialog.Show("Дублировать активный лист", "Не удалось создать копию листа.");
                return Result.Failed;
            }

            if (errors.Count == 0)
            {
                TaskDialog.Show("Дублировать активный лист", "Лист успешно продублирован: " + newSheetNumber);
                return Result.Succeeded;
            }

            string report = "Лист продублирован: " + newSheetNumber
                + "\n\nНе удалось скопировать:\n"
                + string.Join("\n", errors.Take(20));
            if (errors.Count > 20)
            {
                report += "\n...и еще: " + (errors.Count - 20);
            }

            TaskDialog.Show("Дублировать активный лист", report);
            return Result.Succeeded;
        }

        private static string GetNextSheetNumber(Document document, string sourceNumber)
        {
            var existingNumbers = new HashSet<string>(
                new FilteredElementCollector(document)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Select(sheet => sheet.SheetNumber),
                StringComparer.OrdinalIgnoreCase);

            for (int index = 1; index < 10000; index++)
            {
                string candidate = sourceNumber + "." + index;
                if (!existingNumbers.Contains(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Не удалось подобрать свободный номер листа.");
        }

        private static FamilyInstance? GetTitleBlock(Document document, ViewSheet sheet)
        {
            return new FilteredElementCollector(document, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .FirstOrDefault();
        }

        private static void CopyViewports(Document document, ViewSheet sourceSheet, ViewSheet newSheet, ICollection<string> errors)
        {
            List<Viewport> viewports = new FilteredElementCollector(document, sourceSheet.Id)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            foreach (Viewport viewport in viewports)
            {
                View? sourceView = document.GetElement(viewport.ViewId) as View;
                if (sourceView == null)
                {
                    errors.Add("Viewport " + viewport.Id.IntegerValue + ": исходный вид не найден.");
                    continue;
                }

                try
                {
                    ElementId viewIdToPlace;
                    if (sourceView.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
                    {
                        viewIdToPlace = sourceView.Duplicate(ViewDuplicateOption.WithDetailing);
                    }
                    else if (sourceView.ViewType == ViewType.Legend)
                    {
                        viewIdToPlace = sourceView.Id;
                    }
                    else
                    {
                        errors.Add(sourceView.Name + ": вид нельзя дублировать с детализацией.");
                        continue;
                    }

                    if (!Viewport.CanAddViewToSheet(document, newSheet.Id, viewIdToPlace))
                    {
                        errors.Add(sourceView.Name + ": вид нельзя разместить на новом листе.");
                        continue;
                    }

                    Viewport.Create(document, newSheet.Id, viewIdToPlace, viewport.GetBoxCenter());
                }
                catch (Exception exception)
                {
                    errors.Add(sourceView.Name + ": " + exception.Message);
                }
            }
        }

        private static void CopyTextNotes(Document document, ViewSheet sourceSheet, ViewSheet newSheet, ICollection<string> errors)
        {
            List<ElementId> textNoteIds = new FilteredElementCollector(document, sourceSheet.Id)
                .OfClass(typeof(TextNote))
                .WhereElementIsNotElementType()
                .Select(element => element.Id)
                .ToList();

            if (textNoteIds.Count == 0)
            {
                return;
            }

            try
            {
                ElementTransformUtils.CopyElements(
                    sourceSheet,
                    textNoteIds,
                    newSheet,
                    Transform.Identity,
                    new CopyPasteOptions());
            }
            catch (Exception exception)
            {
                errors.Add("Текстовые примечания: " + exception.Message);
            }
        }

        private static void CopySchedules(Document document, ViewSheet sourceSheet, ViewSheet newSheet, ICollection<string> errors)
        {
            List<ScheduleSheetInstance> scheduleInstances = new FilteredElementCollector(document, sourceSheet.Id)
                .OfClass(typeof(ScheduleSheetInstance))
                .Cast<ScheduleSheetInstance>()
                .ToList();

            foreach (ScheduleSheetInstance instance in scheduleInstances)
            {
                ViewSchedule? sourceSchedule = document.GetElement(instance.ScheduleId) as ViewSchedule;
                if (sourceSchedule == null)
                {
                    errors.Add("Спецификация " + instance.Id.IntegerValue + ": исходная спецификация не найдена.");
                    continue;
                }

                try
                {
                    if (!CanCopyScheduleSheetInstance(sourceSchedule))
                    {
                        continue;
                    }

                    if (!sourceSchedule.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
                    {
                        errors.Add(sourceSchedule.Name + ": спецификацию нельзя дублировать.");
                        continue;
                    }

                    ElementId newScheduleId = sourceSchedule.Duplicate(ViewDuplicateOption.Duplicate);
                    ScheduleSheetInstance.Create(document, newSheet.Id, newScheduleId, instance.Point);
                }
                catch (Exception exception)
                {
                    errors.Add(sourceSchedule.Name + ": " + exception.Message);
                }
            }
        }

        private static bool CanCopyScheduleSheetInstance(ViewSchedule schedule)
        {
            return !schedule.IsInternalKeynoteSchedule
                && !schedule.IsTitleblockRevisionSchedule;
        }

        private static void CopyWritableParameters(Element source, Element target, ICollection<string> errors, string context)
        {
            foreach (Parameter sourceParameter in source.Parameters)
            {
                if (sourceParameter == null || sourceParameter.Definition == null)
                {
                    continue;
                }

                if (ShouldSkipParameter(sourceParameter))
                {
                    continue;
                }

                Parameter? targetParameter = FindWritableTargetParameter(target, sourceParameter);
                if (targetParameter == null)
                {
                    continue;
                }

                try
                {
                    CopyParameterValue(sourceParameter, targetParameter);
                }
                catch (Exception exception)
                {
                    errors.Add(context + " / " + sourceParameter.Definition.Name + ": " + exception.Message);
                }
            }
        }

        private static bool ShouldSkipParameter(Parameter parameter)
        {
            if (parameter.IsReadOnly)
            {
                return true;
            }

            BuiltInParameter builtInParameter = (BuiltInParameter)parameter.Id.IntegerValue;
            return builtInParameter == BuiltInParameter.SHEET_NUMBER
                || builtInParameter == BuiltInParameter.SHEET_NAME;
        }

        private static Parameter? FindWritableTargetParameter(Element target, Parameter sourceParameter)
        {
            Parameter? targetParameter = null;
            try
            {
                targetParameter = target.get_Parameter(sourceParameter.Definition);
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                targetParameter = null;
            }

            if (targetParameter == null && !string.IsNullOrWhiteSpace(sourceParameter.Definition.Name))
            {
                targetParameter = target.LookupParameter(sourceParameter.Definition.Name);
            }

            if (targetParameter == null || targetParameter.IsReadOnly)
            {
                return null;
            }

            return targetParameter.StorageType == sourceParameter.StorageType ? targetParameter : null;
        }

        private static void CopyParameterValue(Parameter source, Parameter target)
        {
            switch (source.StorageType)
            {
                case StorageType.String:
                    target.Set(source.AsString());
                    break;
                case StorageType.Integer:
                    target.Set(source.AsInteger());
                    break;
                case StorageType.Double:
                    target.Set(source.AsDouble());
                    break;
                case StorageType.ElementId:
                    target.Set(source.AsElementId());
                    break;
            }
        }
    }
}
