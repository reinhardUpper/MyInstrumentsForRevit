using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace MyInstrumentsForRevit.RebarSketch
{
    internal static class RebarDataResolver
    {
        private static readonly Guid VariableLengthParameterGuid =
            new Guid("ee8d35b0-e2d7-47b3-8b8a-adb31eedac30");

        public static IReadOnlyList<string> GetFormCandidates(Document document, Element element)
        {
            var result = new List<string>();

            if (element is Rebar rebar)
            {
                foreach (ElementId shapeId in rebar.GetAllRebarShapeIds())
                {
                    AddName(result, document.GetElement(shapeId)?.Name);
                }
            }
            else if (element is RebarInSystem rebarInSystem)
            {
                AddName(result, document.GetElement(rebarInSystem.RebarShapeId)?.Name);
            }
            else if (element is FamilyInstance familyInstance)
            {
                AddName(result, familyInstance.Symbol?.FamilyName);
                AddName(result, familyInstance.Symbol?.Family?.Name);
            }

            AddParameterValue(result, document, element.get_Parameter(BuiltInParameter.REBAR_SHAPE));

            foreach (string parameterName in new[]
                     {
                         "Форма арматурного стержня",
                         "Форма арматуры",
                         "Форма",
                         "Rebar Shape"
                     })
            {
                AddParameterValue(result, document, FindParameter(element, parameterName));
            }

            Element? elementType = document.GetElement(element.GetTypeId());
            if (elementType is FamilySymbol symbol)
            {
                AddName(result, symbol.FamilyName);
                AddName(result, symbol.Family?.Name);
            }
            else if (elementType is ElementType type)
            {
                AddName(result, type.FamilyName);
            }

            AddName(result, element.Name);
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static bool IsVariableLength(Element element)
        {
            Parameter? parameter = element.get_Parameter(VariableLengthParameterGuid);
            if (parameter == null || !parameter.HasValue)
            {
                parameter = element.Document.GetElement(element.GetTypeId())?.get_Parameter(VariableLengthParameterGuid);
            }

            return parameter != null
                && parameter.HasValue
                && parameter.StorageType == StorageType.Integer
                && parameter.AsInteger() == 1;
        }

        public static string GetMark(Element element)
        {
            return element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()?.Trim() ?? string.Empty;
        }

        public static bool TryGetParameterValue(
            Element element,
            string parameterName,
            out RebarParameterValue value)
        {
            Parameter? parameter = FindParameter(element, parameterName);
            if (parameter == null || !parameter.HasValue)
            {
                Element? type = element.Document.GetElement(element.GetTypeId());
                parameter = type == null ? null : FindParameter(type, parameterName);
            }

            if (parameter == null || !parameter.HasValue)
            {
                value = RebarParameterValue.Empty;
                return false;
            }

            switch (parameter.StorageType)
            {
                case StorageType.Double:
                    double convertedValue = ConvertFromInternalUnits(parameter, parameter.AsDouble());
                    bool isAngle = parameter.Definition.ParameterType == ParameterType.Angle;
                    value = new RebarParameterValue(convertedValue, isAngle);
                    return true;

                case StorageType.Integer:
                    value = new RebarParameterValue(parameter.AsInteger());
                    return true;

                case StorageType.String:
                    value = new RebarParameterValue(parameter.AsString() ?? string.Empty);
                    return true;

                case StorageType.ElementId:
                    Element? referencedElement = element.Document.GetElement(parameter.AsElementId());
                    value = new RebarParameterValue(referencedElement?.Name ?? parameter.AsValueString() ?? string.Empty);
                    return true;

                default:
                    value = RebarParameterValue.Empty;
                    return false;
            }
        }

        public static Parameter? FindParameter(Element element, string parameterName)
        {
            Parameter? exact = element.LookupParameter(parameterName);
            if (exact != null)
            {
                return exact;
            }

            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter.Definition != null
                    && string.Equals(parameter.Definition.Name, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    return parameter;
                }
            }

            return null;
        }

        private static double ConvertFromInternalUnits(Parameter parameter, double value)
        {
            MethodInfo? getUnitTypeId = parameter.GetType().GetMethod("GetUnitTypeId", Type.EmptyTypes);
            if (getUnitTypeId != null)
            {
                object? unitTypeId = getUnitTypeId.Invoke(parameter, null);
                if (unitTypeId != null)
                {
                    MethodInfo? converter = typeof(UnitUtils)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(method =>
                        {
                            ParameterInfo[] args = method.GetParameters();
                            return method.Name == "ConvertFromInternalUnits"
                                && args.Length == 2
                                && args[0].ParameterType == typeof(double)
                                && args[1].ParameterType.IsInstanceOfType(unitTypeId);
                        });

                    if (converter != null)
                    {
                        return (double)converter.Invoke(null, new[] { (object)value, unitTypeId });
                    }
                }
            }

            PropertyInfo? displayUnitType = parameter.GetType().GetProperty("DisplayUnitType");
            object? legacyUnit = displayUnitType?.GetValue(parameter);
            if (legacyUnit != null)
            {
                MethodInfo? converter = typeof(UnitUtils)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(method =>
                    {
                        ParameterInfo[] args = method.GetParameters();
                        return method.Name == "ConvertFromInternalUnits"
                            && args.Length == 2
                            && args[0].ParameterType == typeof(double)
                            && args[1].ParameterType.IsInstanceOfType(legacyUnit);
                    });

                if (converter != null)
                {
                    return (double)converter.Invoke(null, new[] { (object)value, legacyUnit });
                }
            }

            return value;
        }

        private static void AddParameterValue(ICollection<string> result, Document document, Parameter? parameter)
        {
            if (parameter == null || !parameter.HasValue)
            {
                return;
            }

            if (parameter.StorageType == StorageType.ElementId)
            {
                AddName(result, document.GetElement(parameter.AsElementId())?.Name);
            }

            AddName(result, parameter.AsValueString());
            AddName(result, parameter.AsString());
        }

        private static void AddName(ICollection<string> result, string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(name!.Trim());
            }
        }
    }

    internal sealed class RebarParameterValue
    {
        public static RebarParameterValue Empty { get; } = new RebarParameterValue(string.Empty);

        public RebarParameterValue(double numericValue, bool isAngle = false)
        {
            NumericValue = numericValue;
            IsNumeric = true;
            IsAngle = isAngle;
            TextValue = string.Empty;
        }

        public RebarParameterValue(int numericValue)
            : this((double)numericValue)
        {
        }

        public RebarParameterValue(string textValue)
        {
            TextValue = textValue;
        }

        public bool IsNumeric { get; }

        public bool IsAngle { get; }

        public double NumericValue { get; }

        public string TextValue { get; } = string.Empty;

        public string Format(double roundingAccuracy = 5.0)
        {
            if (!IsNumeric)
            {
                return TextValue;
            }

            double rounded = roundingAccuracy * Math.Round(NumericValue / roundingAccuracy);
            return rounded.ToString("F0", CultureInfo.InvariantCulture) + (IsAngle ? "\u02DA" : string.Empty);
        }
    }
}
