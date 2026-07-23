using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using ContextFilter.Core.Models;
using ContextFilter.Plugin.RevitContext;

namespace ContextFilter.Plugin.Services;

/// <summary>
/// Reads the active Revit selection and builds an immutable category-family-type tree.
/// </summary>
public sealed class SelectionService
{
    private readonly IRevitContext _context;

    /// <summary>Creates the selection service.</summary>
    public SelectionService(IRevitContext context)
    {
        _context = context;
    }

    /// <summary>Builds a cached snapshot for the current selection.</summary>
    public SelectionSnapshot BuildSnapshot()
    {
        return BuildSnapshot(ElementPreFilterOptions.Default);
    }

    /// <summary>Builds a cached snapshot for the current selection.</summary>
    public SelectionSnapshot BuildSnapshot(ElementPreFilterOptions preFilterOptions)
    {
        var uiDocument = _context.UiDocument;
        var document = _context.Document;
        var selectedIds = uiDocument.Selection.GetElementIds()
            .Where(id => id != ElementId.InvalidElementId)
            .Select(id => document.GetElement(id))
            .Where(element => element is not null)
            .Where(element => PassesPreFilter(element!, preFilterOptions))
            .Select(element => element!.Id)
            .OrderBy(id => id.IntegerValue)
            .ToList();

        return BuildSnapshot(selectedIds, "selection");
    }

    /// <summary>Builds a cached snapshot for all selectable elements visible in the active view.</summary>
    public SelectionSnapshot BuildActiveViewSnapshot()
    {
        return BuildActiveViewSnapshot(ElementPreFilterOptions.Default);
    }

    /// <summary>Builds a cached snapshot for all selectable elements visible in the active view.</summary>
    public SelectionSnapshot BuildActiveViewSnapshot(ElementPreFilterOptions preFilterOptions)
    {
        var document = _context.Document;
        var activeView = document.ActiveView;
        var ids = new FilteredElementCollector(document, activeView.Id)
            .WhereElementIsNotElementType()
            .ToElements()
            .Where(element => element.Category is not null)
            .Where(element => CanSelectInView(activeView, element))
            .Where(element => PassesPreFilter(element, preFilterOptions))
            .Select(element => element.Id)
            .Where(id => id != ElementId.InvalidElementId)
            .OrderBy(id => id.IntegerValue)
            .ToList();

        return BuildSnapshot(ids, $"view:{activeView.Id.IntegerValue}");
    }

    private static bool PassesPreFilter(Element element, ElementPreFilterOptions options)
    {
        if (!options.IsEnabled)
        {
            return true;
        }

        if (!PassesClassFilter(element, options))
        {
            return false;
        }

        return true;
    }

    private static bool PassesClassFilter(Element element, ElementPreFilterOptions options)
    {
        if (!options.IncludeRebar && !options.IncludeWalls && !options.IncludeFloors &&
            !options.IncludeFoundations && !options.IncludeGenericModels)
        {
            return true;
        }

        if (options.IncludeRebar && element is Rebar)
        {
            return true;
        }

        int categoryId = element.Category?.Id.IntegerValue ?? 0;
        return (options.IncludeRebar && categoryId == (int)BuiltInCategory.OST_Rebar)
            || (options.IncludeWalls && categoryId == (int)BuiltInCategory.OST_Walls)
            || (options.IncludeFloors && categoryId == (int)BuiltInCategory.OST_Floors)
            || (options.IncludeFoundations && categoryId == (int)BuiltInCategory.OST_StructuralFoundation)
            || (options.IncludeGenericModels && categoryId == (int)BuiltInCategory.OST_GenericModel);
    }

    private SelectionSnapshot BuildSnapshot(IReadOnlyList<ElementId> selectedIds, string source)
    {
        var document = _context.Document;

        if (selectedIds.Count == 0)
        {
            return SelectionSnapshot.Empty with
            {
                Signature = source,
                CreatedAt = DateTimeOffset.Now
            };
        }

        var items = selectedIds
            .Select(id => document.GetElement(id))
            .Where(element => element is not null)
            .Select(element => CreateItem(document, element!))
            .ToList();

        var roots = items
            .GroupBy(item => item.CategoryName)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(categoryGroup => BuildCategoryNode(categoryGroup.Key, categoryGroup))
            .ToList();

        var signature = string.Join(";", selectedIds.Select(id => id.IntegerValue.ToString()));
        return new SelectionSnapshot(
            roots,
            items.Select(item => item.ElementId).ToList(),
            items.Count,
            $"{source}:{signature}",
            DateTimeOffset.Now);
    }

    private static FilterNode BuildCategoryNode(string name, IEnumerable<SelectionItem> items)
    {
        var materialized = items.ToList();
        var children = materialized
            .GroupBy(item => item.FamilyName)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(familyGroup => BuildFamilyNode(familyGroup.Key, familyGroup))
            .ToList();

        return new FilterNode(
            name,
            FilterNodeKind.Category,
            materialized.Count,
            materialized.Select(item => item.ElementId).ToList(),
            name,
            BuildParameters(materialized),
            children);
    }

    private static FilterNode BuildFamilyNode(string name, IEnumerable<SelectionItem> items)
    {
        var materialized = items.ToList();
        var children = materialized
            .GroupBy(item => item.TypeName)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(typeGroup => BuildTypeNode(typeGroup.Key, typeGroup))
            .ToList();

        return new FilterNode(
            name,
            FilterNodeKind.Family,
            materialized.Count,
            materialized.Select(item => item.ElementId).ToList(),
            BuildGroupSearchText(materialized),
            BuildParameters(materialized),
            children);
    }

    private static FilterNode BuildTypeNode(string name, IEnumerable<SelectionItem> items)
    {
        var materialized = items.ToList();
        var parameters = BuildParameters(materialized);
        var children = BuildParameterNodes(parameters);
        return new FilterNode(
            name,
            FilterNodeKind.Type,
            materialized.Count,
            materialized.Select(item => item.ElementId).ToList(),
            BuildGroupSearchText(materialized),
            parameters,
            children);
    }

    private static SelectionItem CreateItem(Document document, Element element)
    {
        var type = GetElementType(document, element);
        var categoryName = element.Category?.Name ?? "<No category>";
        var familyName = GetFamilyName(element, type);
        var typeName = type?.Name ?? element.Name ?? $"Element {element.Id.IntegerValue}";
        var parameters = BuildParameterValues(element, type);
        var classSearchText = BuildClassSearchText(element);
        var parameterText = string.Join(" ", parameters.Select(parameter => $"{parameter.Name}: {parameter.Value}"));

        return new SelectionItem(element.Id.IntegerValue, categoryName, familyName, typeName, classSearchText, parameterText, parameters);
    }

    private static ElementType? GetElementType(Document document, Element element)
    {
        var typeId = element.GetTypeId();
        if (typeId == ElementId.InvalidElementId)
        {
            return element as ElementType;
        }

        return document.GetElement(typeId) as ElementType;
    }

    private static string GetFamilyName(Element element, ElementType? type)
    {
        if (element is FamilyInstance familyInstance)
        {
            return familyInstance.Symbol?.Family?.Name ?? type?.FamilyName ?? "<No family>";
        }

        return type?.FamilyName ?? element.Category?.Name ?? "<No family>";
    }

    private static string BuildClassSearchText(Element element)
    {
        var aliases = new List<string>
        {
            element.GetType().Name,
            element.Category?.Name ?? string.Empty
        };

        if (element is FamilyInstance)
        {
            aliases.Add("family instance");
            aliases.Add("экземпляр семейства");
        }

        if (element is Rebar)
        {
            aliases.Add("rebar");
            aliases.Add("reinforcement");
            aliases.Add("арматура");
            aliases.Add("несущая арматура");
        }

        int categoryId = element.Category?.Id.IntegerValue ?? 0;
        switch ((BuiltInCategory)categoryId)
        {
            case BuiltInCategory.OST_Walls:
                aliases.Add("wall");
                aliases.Add("walls");
                aliases.Add("стена");
                aliases.Add("стены");
                break;
            case BuiltInCategory.OST_Floors:
                aliases.Add("floor");
                aliases.Add("floors");
                aliases.Add("slab");
                aliases.Add("перекрытие");
                aliases.Add("перекрытия");
                break;
            case BuiltInCategory.OST_StructuralFoundation:
                aliases.Add("foundation");
                aliases.Add("foundations");
                aliases.Add("фундамент");
                aliases.Add("фундаменты");
                break;
            case BuiltInCategory.OST_GenericModel:
                aliases.Add("generic model");
                aliases.Add("generic models");
                aliases.Add("обобщенная модель");
                aliases.Add("обобщенные модели");
                break;
            case BuiltInCategory.OST_Doors:
                aliases.Add("door");
                aliases.Add("doors");
                aliases.Add("дверь");
                aliases.Add("двери");
                break;
            case BuiltInCategory.OST_Windows:
                aliases.Add("window");
                aliases.Add("windows");
                aliases.Add("окно");
                aliases.Add("окна");
                break;
            case BuiltInCategory.OST_Columns:
            case BuiltInCategory.OST_StructuralColumns:
                aliases.Add("column");
                aliases.Add("columns");
                aliases.Add("колонна");
                aliases.Add("колонны");
                break;
            case BuiltInCategory.OST_StructuralFraming:
                aliases.Add("framing");
                aliases.Add("beam");
                aliases.Add("beams");
                aliases.Add("каркас");
                aliases.Add("балка");
                aliases.Add("балки");
                break;
        }

        return string.Join(" ", aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)));
    }

    private static bool CanSelectInView(View view, Element element)
    {
        try
        {
            return element.CanBeHidden(view);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            return false;
        }
    }

    private static IReadOnlyList<FilterNode> BuildParameterNodes(IEnumerable<FilterParameter> parameters)
    {
        return parameters
            .Select(parameter =>
            {
                var valueNodes = parameter.Values
                    .Select(value => new FilterNode(
                        value.Value,
                        FilterNodeKind.ParameterValue,
                        value.Count,
                        value.ElementIds,
                        $"{parameter.Name} {value.Value}",
                        Array.Empty<FilterParameter>(),
                        FilterNode.EmptyChildren))
                    .ToList();

                return new FilterNode(
                    parameter.Name,
                    FilterNodeKind.Parameter,
                    parameter.ElementCount,
                    parameter.Values.SelectMany(value => value.ElementIds).Distinct().OrderBy(id => id).ToList(),
                    $"{parameter.Name} {parameter.Preview}",
                    Array.Empty<FilterParameter>(),
                    valueNodes);
            })
            .ToList();
    }

    private static string BuildGroupSearchText(IEnumerable<SelectionItem> items)
    {
        return string.Join(
            " ",
            items.Select(item => $"{item.CategoryName} {item.FamilyName} {item.TypeName} {item.ClassSearchText}"));
    }

    private static IReadOnlyList<FilterParameter> BuildParameters(IEnumerable<SelectionItem> items)
    {
        return items
            .SelectMany(item => item.Parameters.Select(parameter => new
            {
                parameter.Name,
                parameter.Value,
                item.ElementId
            }))
            .GroupBy(parameter => parameter.Name)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new FilterParameter(
                group.Key,
                group.Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                    .GroupBy(parameter => parameter.Value, StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(valueGroup => valueGroup.Key, StringComparer.CurrentCultureIgnoreCase)
                    .Select(valueGroup => new FilterParameterValue(
                        valueGroup.Key,
                        valueGroup.Select(parameter => parameter.ElementId).Distinct().OrderBy(id => id).ToList()))
                    .Take(25)
                    .ToList(),
                group.Select(parameter => parameter.ElementId).Distinct().Count()))
            .ToList();
    }

    private static IReadOnlyList<ParameterValue> BuildParameterValues(Element element, ElementType? type)
    {
        var values = new List<ParameterValue>();
        AppendParameters(values, element.Parameters);

        if (type is not null)
        {
            AppendParameters(values, type.Parameters);
        }

        return values
            .GroupBy(value => $"{value.Name}\u001F{value.Value}")
            .Select(group => group.First())
            .ToList();
    }

    private static void AppendParameters(ICollection<ParameterValue> values, ParameterSet parameters)
    {
        foreach (Parameter parameter in parameters)
        {
            var value = GetParameterValue(parameter);
            var name = parameter.Definition?.Name;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            values.Add(new ParameterValue(name!, value!));
        }
    }

    private static string? GetParameterValue(Parameter parameter)
    {
        try
        {
            var valueString = parameter.AsValueString();
            if (!string.IsNullOrWhiteSpace(valueString))
            {
                return valueString;
            }

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return parameter.AsString();
                case StorageType.Integer:
                    return parameter.AsInteger().ToString();
                case StorageType.Double:
                    return parameter.AsDouble().ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                case StorageType.ElementId:
                    return parameter.AsElementId().IntegerValue.ToString();
                default:
                    return null;
            }
        }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
        {
            return null;
        }
    }

    private sealed class SelectionItem
    {
        public SelectionItem(
            int elementId,
            string categoryName,
            string familyName,
            string typeName,
            string classSearchText,
            string parameterText,
            IReadOnlyList<ParameterValue> parameters)
        {
            ElementId = elementId;
            CategoryName = categoryName;
            FamilyName = familyName;
            TypeName = typeName;
            ClassSearchText = classSearchText;
            ParameterText = parameterText;
            Parameters = parameters;
        }

        public int ElementId { get; }

        public string CategoryName { get; }

        public string FamilyName { get; }

        public string TypeName { get; }

        public string ClassSearchText { get; }

        public string ParameterText { get; }

        public IReadOnlyList<ParameterValue> Parameters { get; }
    }

    private sealed class ParameterValue
    {
        public ParameterValue(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public string Value { get; }
    }
}
