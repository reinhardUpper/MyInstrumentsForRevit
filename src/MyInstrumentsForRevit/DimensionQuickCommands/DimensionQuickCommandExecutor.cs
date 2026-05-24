using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevitTools.DimensionQuickCommands
{
    public static class DimensionQuickCommandExecutor
    {
        public static Result Execute(UIApplication uiapp, DimensionQuickCommandConfig config)
        {
            if (uiapp == null)
            {
                TaskDialog.Show("Быстрый размер", "Не удалось получить контекст Revit.");
                return Result.Cancelled;
            }

            UIDocument? uidoc = uiapp.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (doc == null)
            {
                TaskDialog.Show("Быстрый размер", "Нет открытого документа Revit.");
                return Result.Cancelled;
            }

            DimensionType? dimensionType = FindDimensionType(doc, config);
            if (dimensionType == null)
            {
                TaskDialog.Show(
                    "Быстрый размер",
                    $"\u041D\u0435 \u043D\u0430\u0439\u0434\u0435\u043D \u0442\u0438\u043F \u0440\u0430\u0437\u043C\u0435\u0440\u0430 \"{config.DimensionTypeName}\".\n\n\u041E\u0442\u043A\u0440\u043E\u0439\u0442\u0435 \u043C\u0435\u043D\u0435\u0434\u0436\u0435\u0440 \u0438 \u043E\u0431\u043D\u043E\u0432\u0438\u0442\u0435 \u043D\u0430\u0441\u0442\u0440\u043E\u0439\u043A\u0443 \u0441\u043B\u043E\u0442\u0430 \u0411\u041A{config.SlotNumber}.");
                return Result.Cancelled;
            }

            if (!TrySetDefaultLinearDimensionType(doc, dimensionType))
            {
                return Result.Cancelled;
            }

            return TryPostAlignedDimensionCommand(uiapp);
        }

        private static DimensionType? FindDimensionType(Document doc, DimensionQuickCommandConfig config)
        {
            if (config.DimensionTypeElementId > 0)
            {
                var id = new ElementId(config.DimensionTypeElementId);
                if (doc.GetElement(id) is DimensionType byId && IsLinear(byId))
                {
                    return byId;
                }
            }

            if (!string.IsNullOrWhiteSpace(config.DimensionTypeUniqueId)
                && doc.GetElement(config.DimensionTypeUniqueId) is DimensionType byUniqueId
                && IsLinear(byUniqueId))
            {
                return byUniqueId;
            }

            if (!string.IsNullOrWhiteSpace(config.DimensionTypeName))
            {
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(DimensionType))
                    .Cast<DimensionType>()
                    .FirstOrDefault(type =>
                        IsLinear(type)
                        && string.Equals(type.Name, config.DimensionTypeName, StringComparison.CurrentCultureIgnoreCase));
            }

            return null;
        }

        private static bool TrySetDefaultLinearDimensionType(Document doc, DimensionType dimensionType)
        {
            ElementId typeId = dimensionType.Id;
            const ElementTypeGroup group = ElementTypeGroup.LinearDimensionType;

            // Revit 2021 API provides Document.SetDefaultElementTypeId for LinearDimensionType.
            // If a future Revit API/version does not honor this for PostCommand, the fallback is
            // a custom dimension creation tool or changing the created Dimension type after creation.
            if (!doc.IsDefaultElementTypeIdValid(group, typeId))
            {
                TaskDialog.Show("Быстрый размер", $"Тип \"{dimensionType.Name}\" нельзя назначить типом линейного размера по умолчанию.");
                return false;
            }

            try
            {
                using (var transaction = new Transaction(doc, "Set quick linear dimension type"))
                {
                    transaction.Start();
                    doc.SetDefaultElementTypeId(group, typeId);
                    transaction.Commit();
                }

                return true;
            }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ArgumentException || ex is Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                TaskDialog.Show("Быстрый размер", "Не удалось назначить тип размера текущим.\n\n" + ex.Message);
                return false;
            }
        }

        private static Result TryPostAlignedDimensionCommand(UIApplication uiapp)
        {
            RevitCommandId commandId = RevitCommandId.LookupPostableCommandId(PostableCommand.AlignedDimension);
            if (commandId == null)
            {
                TaskDialog.Show("Быстрый размер", "Системная команда линейного размера недоступна в этой версии Revit.");
                return Result.Cancelled;
            }

            try
            {
                uiapp.PostCommand(commandId);
                return Result.Succeeded;
            }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.InvalidOperationException || ex is Autodesk.Revit.Exceptions.ArgumentException)
            {
                TaskDialog.Show(
                    "Быстрый размер",
                    "Не удалось запустить системную команду линейного размера.\n\nВозможно, Revit уже находится внутри другой команды или текущий вид не поддерживает создание размеров.\n\n" + ex.Message);
                return Result.Cancelled;
            }
        }

        private static bool IsLinear(DimensionType type)
        {
            return type.StyleType == DimensionStyleType.Linear
                || type.StyleType == DimensionStyleType.LinearFixed;
        }
    }
}
