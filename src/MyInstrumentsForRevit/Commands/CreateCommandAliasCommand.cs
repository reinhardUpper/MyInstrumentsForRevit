using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.CommandLine;
using MyInstrumentsForRevit.Windows;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateCommandAliasCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var window = new CreateAliasWindow();
            if (window.ShowDialog() != true)
            {
                return Result.Cancelled;
            }

            if (!CommandRegistry.HasBaseCommand(window.CommandName))
            {
                TaskDialog.Show("Создать alias", "Команда не найдена: " + window.CommandName);
                return Result.Cancelled;
            }

            CommandAliasService.SaveAlias(window.Alias, window.CommandName);
            TaskDialog.Show("Создать alias", "Alias сохранен:\n" + window.Alias + " = " + window.CommandName);
            return Result.Succeeded;
        }
    }
}

