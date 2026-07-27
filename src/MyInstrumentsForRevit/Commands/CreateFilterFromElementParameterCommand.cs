using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using MyInstrumentsForRevit.Filters;
using MyInstrumentsForRevit.Windows;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateFilterFromElementParameterCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Run(commandData.Application);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Фильтр по параметру", exception.Message);
                return Result.Failed;
            }
        }

        public static void ExecuteFromCommandLine(UIApplication uiApplication)
        {
            Run(uiApplication);
        }

        private static void Run(UIApplication application)
        {
            UIDocument uiDocument = application.ActiveUIDocument;
            if (uiDocument == null)
            {
                return;
            }

            Document document = uiDocument.Document;
            View view = document.ActiveView;
            if (!ViewFilterApplicator.CanUseFilters(view))
            {
                TaskDialog.Show("Фильтр по параметру", "Активный вид не поддерживает фильтры.");
                return;
            }

            IReadOnlyList<Element> sourceElements = GetSourceElements(uiDocument);
            if (sourceElements.Count == 0)
            {
                TaskDialog.Show("Фильтр по параметру", "У выбранных элементов нет категорий.");
                return;
            }

            List<ElementId> categoryIds = GetCategoryIds(sourceElements);
            IReadOnlyList<ElementParameterFilterCandidate> parameters = GetFilterableParameters(document, sourceElements, categoryIds);
            if (parameters.Count == 0)
            {
                TaskDialog.Show("Фильтр по параметру", "У выбранных элементов не найдено общих параметров со значениями для фильтра.");
                return;
            }

            var window = new ElementParameterFilterWindow(parameters);
            if (window.ShowDialog() != true || window.SelectedParameter == null)
            {
                return;
            }

            using (var transaction = new Transaction(document, "Create view filter from element parameter"))
            {
                transaction.Start();
                ParameterFilterElement filter = CreateFilter(document, sourceElements[0], categoryIds, window.SelectedParameter, window.IsolateSimilar);
                ViewFilterApplicator.ApplyVisibility(view, filter.Id, false);
                transaction.Commit();
            }
        }

        private static IReadOnlyList<Element> GetSourceElements(UIDocument uiDocument)
        {
            Document document = uiDocument.Document;
            List<Element> selectedElements = uiDocument.Selection.GetElementIds()
                .Select(id => document.GetElement(id))
                .Where(element => element?.Category != null)
                .Cast<Element>()
                .ToList();

            if (selectedElements.Count > 1)
            {
                return selectedElements;
            }

            Reference reference = uiDocument.Selection.PickObject(ObjectType.Element, "Выберите элемент для фильтра по параметру");
            Element element = document.GetElement(reference.ElementId);
            return element?.Category == null
                ? new List<Element>()
                : new List<Element> { element };
        }

        private static List<ElementId> GetCategoryIds(IReadOnlyList<Element> elements)
        {
            return elements
                .Where(element => element.Category != null)
                .Select(element => element.Category.Id)
                .GroupBy(id => id.IntegerValue)
                .Select(group => group.First())
                .ToList();
        }

        private static IReadOnlyList<ElementParameterFilterCandidate> GetFilterableParameters(
            Document document,
            IReadOnlyList<Element> elements,
            ICollection<ElementId> categories)
        {
            Element sourceElement = elements[0];
            HashSet<int> filterableParameterIds = ParameterFilterUtilities
                .GetFilterableParametersInCommon(document, categories)
                .Select(id => id.IntegerValue)
                .ToHashSet();

            filterableParameterIds.IntersectWith(GetCommonElementParameterIds(elements));

            return sourceElement.Parameters
                .Cast<Parameter>()
                .Where(parameter => parameter.Definition != null
                    && parameter.Id != ElementId.InvalidElementId
                    && filterableParameterIds.Contains(parameter.Id.IntegerValue)
                    && parameter.StorageType != StorageType.None
                    && HasValue(parameter))
                .Select(parameter => new ElementParameterFilterCandidate(parameter))
                .GroupBy(parameter => parameter.Name + "\n" + parameter.DisplayValue + "\n" + parameter.StorageType)
                .Select(group => group.First())
                .OrderBy(parameter => parameter.Name)
                .ToList();
        }

        private static HashSet<int> GetCommonElementParameterIds(IReadOnlyList<Element> elements)
        {
            HashSet<int> common = GetValuedParameterIds(elements[0]);
            foreach (Element element in elements.Skip(1))
            {
                common.IntersectWith(GetValuedParameterIds(element));
            }

            return common;
        }

        private static HashSet<int> GetValuedParameterIds(Element element)
        {
            return element.Parameters
                .Cast<Parameter>()
                .Where(parameter => parameter.Definition != null
                    && parameter.Id != ElementId.InvalidElementId
                    && parameter.StorageType != StorageType.None
                    && HasValue(parameter))
                .Select(parameter => parameter.Id.IntegerValue)
                .ToHashSet();
        }

        private static bool HasValue(Parameter parameter)
        {
            if (!parameter.HasValue)
            {
                return false;
            }

            if (parameter.StorageType == StorageType.String)
            {
                return !string.IsNullOrEmpty(parameter.AsString());
            }

            return true;
        }

        private static ParameterFilterElement CreateFilter(
            Document document,
            Element element,
            ICollection<ElementId> categories,
            ElementParameterFilterCandidate selected,
            bool isolateSimilar)
        {
            FilterRule rule = CreateRule(selected.Parameter, isolateSimilar);
            string filterName = BuildFilterName(document, element, categories, selected, isolateSimilar);
            ParameterFilterElement filter = ParameterFilterElement.Create(document, filterName, categories);
            filter.SetElementFilter(new ElementParameterFilter(rule));
            return filter;
        }

        private static FilterRule CreateRule(Parameter parameter, bool isolateSimilar)
        {
            ElementId parameterId = parameter.Id;
            if (parameter.StorageType == StorageType.String)
            {
                string value = parameter.AsString() ?? string.Empty;
                return isolateSimilar
                    ? ParameterFilterRuleFactory.CreateNotEqualsRule(parameterId, value, false)
                    : ParameterFilterRuleFactory.CreateEqualsRule(parameterId, value, false);
            }

            if (parameter.StorageType == StorageType.Integer)
            {
                int value = parameter.AsInteger();
                return isolateSimilar
                    ? ParameterFilterRuleFactory.CreateNotEqualsRule(parameterId, value)
                    : ParameterFilterRuleFactory.CreateEqualsRule(parameterId, value);
            }

            if (parameter.StorageType == StorageType.Double)
            {
                double value = parameter.AsDouble();
                const double tolerance = 1.0e-9;
                return isolateSimilar
                    ? ParameterFilterRuleFactory.CreateNotEqualsRule(parameterId, value, tolerance)
                    : ParameterFilterRuleFactory.CreateEqualsRule(parameterId, value, tolerance);
            }

            if (parameter.StorageType == StorageType.ElementId)
            {
                ElementId value = parameter.AsElementId();
                return isolateSimilar
                    ? ParameterFilterRuleFactory.CreateNotEqualsRule(parameterId, value)
                    : ParameterFilterRuleFactory.CreateEqualsRule(parameterId, value);
            }

            throw new InvalidOperationException("Тип параметра не поддерживается для фильтра.");
        }

        private static string BuildFilterName(
            Document document,
            Element element,
            ICollection<ElementId> categories,
            ElementParameterFilterCandidate selected,
            bool isolateSimilar)
        {
            string mode = isolateSimilar ? "Изолировать" : "Скрыть";
            string category = BuildCategoryName(element, categories);
            string value = string.IsNullOrWhiteSpace(selected.DisplayValue) ? "пусто" : selected.DisplayValue;
            string baseName = SafeFilterName($"{mode}: {category} | {selected.Name} = {value}");
            string name = baseName;
            int index = 2;
            while (new FilteredElementCollector(document)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .Any(filter => string.Equals(filter.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                name = $"{baseName} ({index})";
                index++;
            }

            return name;
        }

        private static string BuildCategoryName(Element element, ICollection<ElementId> categories)
        {
            if (categories.Count == 1)
            {
                return element.Category?.Name ?? "Без категории";
            }

            return categories.Count + " категорий";
        }

        private static string SafeFilterName(string value)
        {
            char[] invalid = { '\\', '/', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };
            string result = value;
            foreach (char character in invalid)
            {
                result = result.Replace(character, '-');
            }

            return result.Length > 120 ? result.Substring(0, 120) : result;
        }
    }
}
