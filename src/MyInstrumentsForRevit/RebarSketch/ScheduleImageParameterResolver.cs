using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace MyInstrumentsForRevit.RebarSketch
{
    internal sealed class ScheduleImageParameterResolver
    {
        private readonly BuiltInParameter? _builtInParameter;
        private readonly Definition? _definition;
        private readonly string? _fallbackName;

        private ScheduleImageParameterResolver(
            string displayName,
            BuiltInParameter? builtInParameter,
            Definition? definition,
            string? fallbackName)
        {
            DisplayName = displayName;
            _builtInParameter = builtInParameter;
            _definition = definition;
            _fallbackName = fallbackName;
        }

        public string DisplayName { get; }

        public static ScheduleImageParameterResolver Resolve(
            Document document,
            ViewSchedule schedule,
            IReadOnlyList<Element> elements)
        {
            var imageFields = new List<ScheduleField>();
            ScheduleDefinition scheduleDefinition = schedule.Definition;

            foreach (ScheduleFieldId fieldId in scheduleDefinition.GetFieldOrder())
            {
                ScheduleField field = scheduleDefinition.GetField(fieldId);
                if (IsImageField(document, field, elements))
                {
                    imageFields.Add(field);
                }
            }

            ScheduleField? selectedField = imageFields
                .OrderByDescending(GetFieldScore)
                .FirstOrDefault();

            if (selectedField != null)
            {
                ElementId parameterId = selectedField.ParameterId;
                if (parameterId.IntegerValue < 0)
                {
                    return new ScheduleImageParameterResolver(
                        selectedField.GetName(),
                        (BuiltInParameter)parameterId.IntegerValue,
                        null,
                        null);
                }

                if (document.GetElement(parameterId) is ParameterElement parameterElement)
                {
                    return new ScheduleImageParameterResolver(
                        selectedField.GetName(),
                        null,
                        parameterElement.GetDefinition(),
                        null);
                }
            }

            if (elements.Any(element => IsWritableImageParameter(element.LookupParameter("RebarImage"))))
            {
                return new ScheduleImageParameterResolver("RebarImage", null, null, "RebarImage");
            }

            throw new InvalidOperationException(
                "В выбранной спецификации нет поля типа «Изображение», а у элементов не найден параметр RebarImage.");
        }

        public Parameter? GetParameter(Element element)
        {
            if (_builtInParameter.HasValue)
            {
                return element.get_Parameter(_builtInParameter.Value);
            }

            if (_definition != null)
            {
                return element.get_Parameter(_definition);
            }

            return _fallbackName == null ? null : element.LookupParameter(_fallbackName);
        }

        public bool CanWrite(Element element)
        {
            return IsWritableImageParameter(GetParameter(element));
        }

        private static bool IsImageField(
            Document document,
            ScheduleField field,
            IReadOnlyList<Element> elements)
        {
            if (field.IsCalculatedField || field.IsCombinedParameterField)
            {
                return false;
            }

            ElementId parameterId = field.ParameterId;
            Definition? definition = null;

            if (parameterId.IntegerValue < 0 && elements.Count > 0)
            {
                definition = elements[0].get_Parameter((BuiltInParameter)parameterId.IntegerValue)?.Definition;
            }
            else if (document.GetElement(parameterId) is ParameterElement parameterElement)
            {
                definition = parameterElement.GetDefinition();
            }

            return definition != null && definition.ParameterType == ParameterType.Image;
        }

        private static int GetFieldScore(ScheduleField field)
        {
            string name = (field.ColumnHeading + " " + field.GetName()).ToUpperInvariant();
            int score = field.IsHidden ? 0 : 10;
            if (name.Contains("ЭСКИЗ") || name.Contains("SKETCH"))
            {
                score += 100;
            }
            else if (name.Contains("ИЗОБРАЖ"))
            {
                score += 50;
            }

            return score;
        }

        private static bool IsWritableImageParameter(Parameter? parameter)
        {
            return parameter != null
                && !parameter.IsReadOnly
                && parameter.StorageType == StorageType.ElementId;
        }
    }
}
