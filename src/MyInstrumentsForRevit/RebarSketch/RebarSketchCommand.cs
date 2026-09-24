using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace MyInstrumentsForRevit.RebarSketch
{
    [Transaction(TransactionMode.Manual)]
    public class RebarSketchCommand : IExternalCommand
    {
        private const string LibraryPath =
            @"\\picompany.ru\pikp\lib\09_Программы\BIMTools\KR\SketchDetails\library";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            Document document = uiDocument.Document;

            if (!(document.ActiveView is ViewSheet activeSheet))
            {
                TaskDialog.Show("RebarSketch", "Откройте лист и повторите команду.");
                return Result.Cancelled;
            }

            ScheduleSheetInstance? scheduleInstance = PickSchedule(uiDocument, document, activeSheet);
            if (scheduleInstance == null)
            {
                return Result.Cancelled;
            }

            ViewSchedule? schedule = document.GetElement(scheduleInstance.ScheduleId) as ViewSchedule;
            if (schedule == null || schedule.IsTitleblockRevisionSchedule || schedule.Definition.IsKeySchedule)
            {
                TaskDialog.Show("RebarSketch", "Выбранная таблица не является обычной спецификацией элементов.");
                return Result.Cancelled;
            }

            if (schedule.Definition.IncludeLinkedFiles)
            {
                TaskDialog.Show(
                    "RebarSketch",
                    "Спецификации с элементами из связанных файлов пока не поддерживаются: "
                    + "Revit API не возвращает для них надёжное соответствие строк элементам связи.");
                return Result.Cancelled;
            }

            List<Element> scheduledElements;
            try
            {
                scheduledElements = CollectScheduledElements(document, schedule);
            }
            catch (Exception exception)
            {
                TaskDialog.Show("RebarSketch", exception.Message);
                return Result.Failed;
            }

            if (scheduledElements.Count == 0)
            {
                TaskDialog.Show("RebarSketch", "В выбранную спецификацию не входит ни одного элемента модели.");
                return Result.Cancelled;
            }

            SketchLibraryCatalog catalog;
            try
            {
                catalog = SketchLibraryCatalog.Load(LibraryPath);
            }
            catch (Exception exception)
            {
                TaskDialog.Show("RebarSketch", exception.Message);
                return Result.Failed;
            }

            ScheduleImageParameterResolver imageParameter;
            try
            {
                imageParameter = ScheduleImageParameterResolver.Resolve(document, schedule, scheduledElements);
            }
            catch (Exception exception)
            {
                TaskDialog.Show("RebarSketch", exception.Message);
                return Result.Failed;
            }

            string outputDirectory = CreateOutputDirectory();
            var issues = new List<string>();
            var unmatchedForms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<RenderedSketch> renderedSketches;

            try
            {
                renderedSketches = PrepareSketches(
                    document,
                    schedule,
                    scheduledElements,
                    catalog,
                    outputDirectory,
                    unmatchedForms,
                    issues);
            }
            catch (Exception exception)
            {
                TaskDialog.Show("RebarSketch", "Не удалось сформировать эскизы:\n" + exception.Message);
                TryDeleteDirectory(outputDirectory);
                return Result.Failed;
            }

            if (renderedSketches.Count == 0)
            {
                ShowResult(0, 0, unmatchedForms, issues, catalog.Warnings, imageParameter.DisplayName);
                TryDeleteDirectory(outputDirectory);
                return Result.Cancelled;
            }

            int updatedCount = 0;
            try
            {
                using (var transaction = new Transaction(document, "RebarSketch: создать эскизы"))
                {
                    transaction.Start();
                    DeletePreviousImages(document, schedule.Id);

                    foreach (RenderedSketch renderedSketch in renderedSketches)
                    {
                        ImageType imageType = CreateImageType(document, renderedSketch.ImagePath);
                        foreach (Element element in renderedSketch.Elements)
                        {
                            Parameter? parameter = imageParameter.GetParameter(element);
                            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.ElementId)
                            {
                                issues.Add(
                                    "Id " + element.Id.IntegerValue + ": параметр «"
                                    + imageParameter.DisplayName + "» недоступен для записи.");
                                continue;
                            }

                            parameter.Set(imageType.Id);
                            updatedCount++;
                        }
                    }

                    transaction.Commit();
                }
            }
            catch (Exception exception)
            {
                TaskDialog.Show("RebarSketch", "Не удалось загрузить эскизы в Revit:\n" + exception.Message);
                return Result.Failed;
            }
            finally
            {
                TryDeleteDirectory(outputDirectory);
            }

            ShowResult(
                renderedSketches.Count,
                updatedCount,
                unmatchedForms,
                issues,
                catalog.Warnings,
                imageParameter.DisplayName);
            return updatedCount > 0 ? Result.Succeeded : Result.Failed;
        }

        private static ScheduleSheetInstance? PickSchedule(
            UIDocument uiDocument,
            Document document,
            ViewSheet activeSheet)
        {
            try
            {
                Reference reference = uiDocument.Selection.PickObject(
                    ObjectType.Element,
                    new ScheduleOnSheetSelectionFilter(activeSheet.Id),
                    "Выберите размещённую спецификацию");
                return document.GetElement(reference) as ScheduleSheetInstance;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        private static List<Element> CollectScheduledElements(Document document, ViewSchedule schedule)
        {
            if (!FilteredElementCollector.IsViewValidForElementIteration(document, schedule.Id))
            {
                throw new InvalidOperationException(
                    "Revit не поддерживает получение элементов для выбранного типа спецификации.");
            }

            // A view-scoped collector delegates category, phase, design-option and schedule filters to Revit.
            return new FilteredElementCollector(document, schedule.Id)
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(element => element != null && element.IsValidObject)
                .GroupBy(element => element.Id.IntegerValue)
                .Select(group => group.First())
                .OrderBy(element => element.Id.IntegerValue)
                .ToList();
        }

        private static List<RenderedSketch> PrepareSketches(
            Document document,
            ViewSchedule schedule,
            IReadOnlyList<Element> elements,
            SketchLibraryCatalog catalog,
            string outputDirectory,
            ISet<string> unmatchedForms,
            ICollection<string> issues)
        {
            var matched = new List<MatchedElement>();

            foreach (Element element in elements)
            {
                IReadOnlyList<string> formCandidates = RebarDataResolver.GetFormCandidates(document, element);
                if (!catalog.TryFind(formCandidates, out SketchTemplate? template, out string formName)
                    || template == null)
                {
                    unmatchedForms.Add(formName);
                    continue;
                }

                matched.Add(new MatchedElement(element, template));
            }

            IEnumerable<IGrouping<string, MatchedElement>> groups = matched.GroupBy(GetElementGroupKey);
            var sketchesByContent = new Dictionary<string, RenderedSketch>(StringComparer.Ordinal);

            foreach (IGrouping<string, MatchedElement> group in groups)
            {
                List<MatchedElement> groupItems = group.ToList();
                SketchTemplate template = groupItems[0].Template;
                IReadOnlyDictionary<string, string>? values = ResolveValues(groupItems, issues);
                if (values == null)
                {
                    continue;
                }

                string contentKey = BuildContentKey(template, values);
                if (sketchesByContent.TryGetValue(contentKey, out RenderedSketch? existing))
                {
                    existing.Elements.AddRange(groupItems.Select(item => item.Element));
                    continue;
                }

                string hash = GetStableHash(contentKey);
                string imagePath = Path.Combine(
                    outputDirectory,
                    "RS_" + schedule.Id.IntegerValue + "_" + hash + ".png");
                SketchImageRenderer.Render(template, values, imagePath);

                var rendered = new RenderedSketch(imagePath, groupItems.Select(item => item.Element));
                sketchesByContent.Add(contentKey, rendered);
            }

            return sketchesByContent.Values.ToList();
        }

        private static string GetElementGroupKey(MatchedElement item)
        {
            if (RebarDataResolver.IsVariableLength(item.Element))
            {
                string mark = RebarDataResolver.GetMark(item.Element);
                if (mark.Length > 0)
                {
                    return "variable|" + item.Template.FolderPath + "|" + mark;
                }
            }

            return "element|" + item.Element.Id.IntegerValue;
        }

        private static IReadOnlyDictionary<string, string>? ResolveValues(
            IReadOnlyList<MatchedElement> items,
            ICollection<string> issues)
        {
            SketchTemplate template = items[0].Template;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string parameterName in template.Parameters
                         .Select(parameter => parameter.ParameterName)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var parameterValues = new List<RebarParameterValue>();
                foreach (MatchedElement item in items)
                {
                    if (!RebarDataResolver.TryGetParameterValue(item.Element, parameterName, out RebarParameterValue value))
                    {
                        issues.Add(
                            "Id " + item.Element.Id.IntegerValue + ": не найден параметр «"
                            + parameterName + "».");
                        return null;
                    }

                    parameterValues.Add(value);
                }

                values[parameterName] = FormatValues(parameterValues);
            }

            return values;
        }

        private static string FormatValues(IReadOnlyList<RebarParameterValue> values)
        {
            if (values.All(value => value.IsNumeric))
            {
                const double accuracy = 5.0;
                double[] rounded = values
                    .Select(value => accuracy * Math.Round(value.NumericValue / accuracy))
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArray();
                string suffix = values.Any(value => value.IsAngle) ? "\u02DA" : string.Empty;
                if (rounded.Length <= 1)
                {
                    return rounded[0].ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + suffix;
                }

                return rounded.First().ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
                    + "..."
                    + rounded.Last().ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
                    + suffix;
            }

            string[] textValues = values
                .Select(value => value.Format())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return textValues.Length == 1 ? textValues[0] : string.Join("...", textValues);
        }

        private static string BuildContentKey(
            SketchTemplate template,
            IReadOnlyDictionary<string, string> values)
        {
            var builder = new StringBuilder(template.FolderPath);
            foreach (KeyValuePair<string, string> pair in values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append('|').Append(pair.Key).Append('=').Append(pair.Value.Replace('=', '~'));
            }

            return builder.ToString();
        }

        private static string GetStableHash(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                return string.Concat(hash.Take(8).Select(item => item.ToString("x2")));
            }
        }

        private static void DeletePreviousImages(Document document, ElementId scheduleId)
        {
            string prefix = "RS_" + scheduleId.IntegerValue + "_";
            List<ElementId> imageIds = new FilteredElementCollector(document)
                .OfClass(typeof(ImageType))
                .WhereElementIsElementType()
                .Where(element => element.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Id)
                .ToList();

            if (imageIds.Count > 0)
            {
                document.Delete(imageIds);
            }
        }

        private static ImageType CreateImageType(Document document, string imagePath)
        {
#pragma warning disable CS0618
            return ImageType.Create(document, imagePath);
#pragma warning restore CS0618
        }

        private static string CreateOutputDirectory()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "MyInstrumentsForRevit",
                "RebarSketch",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (IOException)
            {
                // Revit has already imported the images; a locked temporary file can be removed later.
            }
            catch (UnauthorizedAccessException)
            {
                // A temporary folder left behind is harmless and preferable to failing the command.
            }
        }

        private static void ShowResult(
            int imageCount,
            int updatedCount,
            IEnumerable<string> unmatchedForms,
            IReadOnlyCollection<string> issues,
            IReadOnlyList<string> libraryWarnings,
            string imageParameterName)
        {
            var lines = new List<string>
            {
                "Создано изображений: " + imageCount,
                "Обновлено элементов: " + updatedCount,
                "Параметр изображения: " + imageParameterName
            };

            string[] missing = unmatchedForms.Where(value => value.Length > 0).Take(8).ToArray();
            if (missing.Length > 0)
            {
                lines.Add("Не найдены формы в библиотеке: " + string.Join(", ", missing));
            }

            if (issues.Count > 0)
            {
                lines.Add("Замечания: " + issues.Count);
                lines.AddRange(issues.Take(5));
            }

            if (libraryWarnings.Count > 0)
            {
                lines.Add("Замечания библиотеки: " + libraryWarnings.Count);
                lines.AddRange(libraryWarnings.Take(3));
            }

            TaskDialog.Show("RebarSketch", string.Join("\n", lines));
        }

        private sealed class MatchedElement
        {
            public MatchedElement(Element element, SketchTemplate template)
            {
                Element = element;
                Template = template;
            }

            public Element Element { get; }

            public SketchTemplate Template { get; }
        }

        private sealed class RenderedSketch
        {
            public RenderedSketch(string imagePath, IEnumerable<Element> elements)
            {
                ImagePath = imagePath;
                Elements = elements.ToList();
            }

            public string ImagePath { get; }

            public List<Element> Elements { get; }
        }

        private sealed class ScheduleOnSheetSelectionFilter : ISelectionFilter
        {
            private readonly ElementId _sheetId;

            public ScheduleOnSheetSelectionFilter(ElementId sheetId)
            {
                _sheetId = sheetId;
            }

            public bool AllowElement(Element element)
            {
                return element is ScheduleSheetInstance instance
                    && instance.OwnerViewId == _sheetId
                    && !instance.IsTitleblockRevisionSchedule;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
