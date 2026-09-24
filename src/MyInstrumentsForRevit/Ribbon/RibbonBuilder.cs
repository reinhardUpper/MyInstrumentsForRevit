using System;
using System.Reflection;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Commands;
using MyInstrumentsForRevit.RebarSketch;

namespace MyInstrumentsForRevit.Ribbon
{
    internal static class RibbonBuilder
    {
        private const string TabName = "\u041C\u043E\u0438 \u0438\u043D\u0441\u0442\u0440\u0443\u043C\u0435\u043D\u0442\u044B";
        private const string GraphicsPanelName = "\u041D\u0430\u0441\u0442\u0440\u043E\u0439\u043A\u0430 \u0433\u0440\u0430\u0444\u0438\u043A\u0438";
        private const string ViewWorkPanelName = "\u0420\u0430\u0431\u043E\u0442\u0430 \u0441 \u0432\u0438\u0434\u0430\u043C\u0438";
        private const string ViewDecorationPanelName = "\u041E\u0444\u043E\u0440\u043C\u043B\u0435\u043D\u0438\u0435 \u0432\u0438\u0434\u0430";
        private const string ViewFiltersPanelName = "\u0420\u0430\u0431\u043E\u0442\u0430 \u0441 \u0444\u0438\u043B\u044C\u0442\u0440\u0430\u043C\u0438";
        private const string CommandLinePanelName = "\u041A\u043E\u043C\u0430\u043D\u0434\u044B";
        private const string RebarPanelName = "\u0410\u0440\u043C\u0430\u0442\u0443\u0440\u0430";

        public static void Build(UIControlledApplication application)
        {
            EnsureTab(application, TabName);

            BuildGraphicsPanel(application);
            BuildViewWorkPanel(application);
            BuildViewDecorationPanel(application);
            BuildViewFiltersPanel(application);
            BuildRebarPanel(application);
            BuildCommandLinePanel(application);
        }

        private static void BuildGraphicsPanel(UIControlledApplication application)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, GraphicsPanelName);
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            var graphicsTools = new PulldownButtonData(
                "MyInstruments_GraphicsSettings",
                "\u041D\u0430\u0441\u0442\u0440\u043E\u0439\u043A\u0430\n\u0433\u0440\u0430\u0444\u0438\u043A\u0438");
            PulldownButton button = panel.AddItem(graphicsTools) as PulldownButton
                ?? throw new InvalidOperationException("Failed to create graphics settings button.");

            button.ToolTip = "\u0418\u043D\u0441\u0442\u0440\u0443\u043C\u0435\u043D\u0442\u044B \u0434\u043B\u044F \u043D\u0430\u0441\u0442\u0440\u043E\u0439\u043A\u0438 \u0433\u0440\u0430\u0444\u0438\u043A\u0438 \u0430\u043A\u0442\u0438\u0432\u043D\u043E\u0433\u043E \u0432\u0438\u0434\u0430.";

            AddPushButton<ToggleStructuralHatchesCommand>(
                button,
                assemblyPath,
                "MyInstruments_ToggleStructuralHatches",
                "\u0428\u0442\u0440\u0438\u0445\u043E\u0432\u043A\u0430\n\u043A\u0430\u0442\u0435\u0433\u043E\u0440\u0438\u0439",
                "\u0421\u043A\u0440\u044B\u0432\u0430\u0435\u0442 \u0438\u043B\u0438 \u0432\u043E\u0437\u0432\u0440\u0430\u0449\u0430\u0435\u0442 \u0448\u0442\u0440\u0438\u0445\u043E\u0432\u043A\u0443 \u0438 \u0442\u043E\u043B\u0449\u0438\u043D\u0443 \u043B\u0438\u043D\u0438\u0439 \u0434\u043B\u044F \u043E\u0441\u043D\u043E\u0432\u043D\u044B\u0445 \u043A\u043E\u043D\u0441\u0442\u0440\u0443\u043A\u0446\u0438\u0439.");

            AddPushButton<ConfigureRebarViewCommand>(
                button,
                assemblyPath,
                "MyInstruments_ConfigureRebarView",
                "\u0412\u0438\u0434\n\u0430\u0440\u043C\u0438\u0440\u043E\u0432\u0430\u043D\u0438\u044F",
                "\u041B\u0438\u043D\u0438\u0438 2, \u0448\u0442\u0440\u0438\u0445\u043E\u0432\u043A\u0430 \u0432\u044B\u043A\u043B\u044E\u0447\u0435\u043D\u0430, \u0430\u0440\u043C\u0430\u0442\u0443\u0440\u0430 \u043F\u043E\u043A\u0430\u0437\u0430\u043D\u0430.");

            AddPushButton<ConfigureFormworkViewCommand>(
                button,
                assemblyPath,
                "MyInstruments_ConfigureFormworkView",
                "\u0412\u0438\u0434\n\u043E\u043F\u0430\u043B\u0443\u0431\u043A\u0438",
                "\u041B\u0438\u043D\u0438\u0438 4, \u0448\u0442\u0440\u0438\u0445\u043E\u0432\u043A\u0430 \u0432\u044B\u043A\u043B\u044E\u0447\u0435\u043D\u0430, \u0430\u0440\u043C\u0430\u0442\u0443\u0440\u0430 \u0441\u043A\u0440\u044B\u0442\u0430.");

            AddPushButton<ToggleRebarCategoryCommand>(
                button,
                assemblyPath,
                "MyInstruments_ToggleRebarCategory",
                "\u0412\u043A\u043B/\u0432\u044B\u043A\u043B\n\u0430\u0440\u043C\u0430\u0442\u0443\u0440\u0443",
                "\u041F\u0435\u0440\u0435\u043A\u043B\u044E\u0447\u0430\u0435\u0442 \u0432\u0438\u0434\u0438\u043C\u043E\u0441\u0442\u044C \u043A\u0430\u0442\u0435\u0433\u043E\u0440\u0438\u0438 \u043D\u0435\u0441\u0443\u0449\u0435\u0439 \u0430\u0440\u043C\u0430\u0442\u0443\u0440\u044B \u043D\u0430 \u0430\u043A\u0442\u0438\u0432\u043D\u043E\u043C \u0432\u0438\u0434\u0435.");

            AddPushButton<Configure3DViewCommand>(
                button,
                assemblyPath,
                "MyInstruments_Configure3DView",
                "\u041D\u0430\u0441\u0442\u0440\u043E\u0438\u0442\u044C\n3D \u0432\u0438\u0434",
                "\u041F\u0440\u0438\u043C\u0435\u043D\u044F\u0435\u0442 \u0441\u0442\u0430\u043D\u0434\u0430\u0440\u0442\u043D\u0443\u044E \u043F\u0440\u0435\u0434\u043D\u0430\u0441\u0442\u0440\u043E\u0439\u043A\u0443 \u043A \u0430\u043A\u0442\u0438\u0432\u043D\u043E\u043C\u0443 3D \u0432\u0438\u0434\u0443.");

            AddPushButton<ToggleRevitLinksCommand>(
                button,
                assemblyPath,
                "MyInstruments_ToggleRevitLinks",
                "Revit\n\u0441\u0432\u044F\u0437\u0438",
                "\u0421\u043A\u0440\u044B\u0432\u0430\u0435\u0442 \u0438\u043B\u0438 \u0432\u043E\u0437\u0432\u0440\u0430\u0449\u0430\u0435\u0442 Revit-\u0441\u0432\u044F\u0437\u0438 \u0442\u043E\u043B\u044C\u043A\u043E \u043D\u0430 \u0430\u043A\u0442\u0438\u0432\u043D\u043E\u043C \u0432\u0438\u0434\u0435.");
        }

        private static void BuildViewWorkPanel(UIControlledApplication application)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, ViewWorkPanelName);
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            AddPushButton<DuplicateSelectedLegendsAndSchedulesCommand>(
                panel,
                assemblyPath,
                "MyInstruments_DuplicateLegendsAndSchedules",
                "\u041A\u043E\u043F\u0438\u044F\n\u043B\u0435\u0433\u0435\u043D\u0434/\u0441\u043F\u0435\u0446.",
                "\u0414\u0443\u0431\u043B\u0438\u0440\u0443\u0435\u0442 \u0432\u044B\u0431\u0440\u0430\u043D\u043D\u044B\u0435 \u043B\u0435\u0433\u0435\u043D\u0434\u044B \u0438 \u0441\u043F\u0435\u0446\u0438\u0444\u0438\u043A\u0430\u0446\u0438\u0438 \u043D\u0430 \u043B\u0438\u0441\u0442\u0435 \u0441\u043E \u0441\u043C\u0435\u0449\u0435\u043D\u0438\u0435\u043C 30 \u043C\u043C \u0432\u043F\u0440\u0430\u0432\u043E.");

            AddPushButton<ShowScheduleRowsCommand>(
                panel,
                assemblyPath,
                "MyInstruments_ShowScheduleRows",
                "\u0421\u0442\u0440\u043e\u043a\u0438\n\u0441\u043f\u0435\u0446.",
                "\u041f\u043e\u043a\u0430\u0437\u044b\u0432\u0430\u0435\u0442 \u0441\u0442\u0440\u043e\u043a\u0438 \u0441\u043f\u0435\u0446\u0438\u0444\u0438\u043a\u0430\u0446\u0438\u0438 \u043d\u0430 \u043b\u0438\u0441\u0442\u0435 \u0438 \u0432\u044b\u0434\u0435\u043b\u044f\u0435\u0442 \u0432 \u043c\u043e\u0434\u0435\u043b\u0438 \u044d\u043b\u0435\u043c\u0435\u043d\u0442\u044b \u0432\u044b\u0431\u0440\u0430\u043d\u043d\u043e\u0439 \u0441\u0442\u0440\u043e\u043a\u0438.");

            AddPushButton<DuplicatePlacedFloorPlanToActiveSheetCommand>(
                panel,
                assemblyPath,
                "MyInstruments_DuplicatePlacedFloorPlan",
                "\u041A\u043E\u043F\u0438\u044F\n\u0432\u0438\u0434\u0430",
                "\u0414\u0443\u0431\u043B\u0438\u0440\u0443\u0435\u0442 \u0432\u044B\u0434\u0435\u043B\u0435\u043D\u043D\u044B\u0435 \u0432 \u0434\u0438\u0441\u043F\u0435\u0442\u0447\u0435\u0440\u0435 \u043F\u0440\u043E\u0435\u043A\u0442\u0430 \u0432\u0438\u0434\u044B \u0441 \u0434\u0435\u0442\u0430\u043B\u0438\u0437\u0430\u0446\u0438\u0435\u0439 \u0438 \u0440\u0430\u0437\u043C\u0435\u0449\u0430\u0435\u0442 \u0438\u0445 \u043D\u0430 \u0442\u0435\u043A\u0443\u0449\u0435\u043C \u043B\u0438\u0441\u0442\u0435.");

            AddPushButton<DuplicateActiveSheetCommand>(
                panel,
                assemblyPath,
                "MyInstruments_DuplicateActiveSheet",
                "\u0414\u0443\u0431\u043B\u044C\n\u043B\u0438\u0441\u0442\u0430",
                "\u0414\u0443\u0431\u043B\u0438\u0440\u0443\u0435\u0442 \u0430\u043A\u0442\u0438\u0432\u043D\u044B\u0439 \u043B\u0438\u0441\u0442, \u043E\u0441\u043D\u043E\u0432\u043D\u0443\u044E \u043D\u0430\u0434\u043F\u0438\u0441\u044C, \u0432\u0438\u0434\u044B \u0438 \u0441\u043F\u0435\u0446\u0438\u0444\u0438\u043A\u0430\u0446\u0438\u0438.");

            AddPushButton<ShowLinkedElementByIdCommand>(
                panel,
                assemblyPath,
                "MyInstruments_ShowLinkedElementById",
                "\u0421\u0432\u044F\u0437\u044C\nID",
                "\u0412 3D \u0432\u0438\u0434\u0435 \u0432\u044B\u0431\u0438\u0440\u0430\u0435\u0442 Revit-\u0441\u0432\u044F\u0437\u044C, \u043F\u0440\u043E\u0441\u0438\u0442 ElementId \u044D\u043B\u0435\u043C\u0435\u043D\u0442\u0430 \u0432 \u0441\u0432\u044F\u0437\u0438 \u0438 \u0441\u0442\u0430\u0432\u0438\u0442 Section Box \u0432\u043E\u043A\u0440\u0443\u0433 \u043D\u0435\u0433\u043E.");
        }

        private static void BuildViewFiltersPanel(UIControlledApplication application)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, ViewFiltersPanelName);
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            AddPushButton<RefreshViewFiltersCommand>(
                panel,
                assemblyPath,
                "MyInstruments_RefreshViewFilters",
                "\u041E\u0431\u043D\u043E\u0432\u0438\u0442\u044C\n\u0444\u0438\u043B\u044C\u0442\u0440\u044B",
                "\u0421\u043E\u0431\u0438\u0440\u0430\u0435\u0442 \u0432\u0441\u0435 ParameterFilterElement \u0432 \u0434\u043E\u043A\u0443\u043C\u0435\u043D\u0442\u0435 \u0438 \u043E\u0431\u043D\u043E\u0432\u043B\u044F\u0435\u0442 \u043A\u044D\u0448 \u0431\u044B\u0441\u0442\u0440\u043E\u0433\u043E \u043F\u043E\u0438\u0441\u043A\u0430.");

            AddPushButton<AddViewFilterCommand>(
                panel,
                assemblyPath,
                "MyInstruments_AddViewFilter",
                "\u0414\u043E\u0431\u0430\u0432\u0438\u0442\u044C\n\u043D\u0430 \u0432\u0438\u0434",
                "\u041E\u0442\u043A\u0440\u044B\u0432\u0430\u0435\u0442 \u043F\u043E\u0438\u0441\u043A \u0444\u0438\u043B\u044C\u0442\u0440\u0430 \u0438 \u043F\u043E\u0437\u0432\u043E\u043B\u044F\u0435\u0442 \u0441\u043A\u0440\u044B\u0442\u044C \u0438\u043B\u0438 \u043F\u043E\u043A\u0430\u0437\u0430\u0442\u044C \u0435\u0433\u043E \u043D\u0430 \u0430\u043A\u0442\u0438\u0432\u043D\u043E\u043C \u0432\u0438\u0434\u0435.");

            AddPushButton<CreateFilterFromElementParameterCommand>(
                panel,
                assemblyPath,
                "MyInstruments_CreateFilterFromElementParameter",
                "\u0424\u0438\u043B\u044C\u0442\u0440\n\u043F\u043E \u043F\u0430\u0440\u0430\u043C.",
                "\u0421\u043E\u0437\u0434\u0430\u0435\u0442 \u0444\u0438\u043B\u044C\u0442\u0440 \u0434\u043B\u044F \u0442\u0435\u043A\u0443\u0449\u0435\u0433\u043E \u0432\u0438\u0434\u0430 \u043F\u043E \u0437\u043D\u0430\u0447\u0435\u043D\u0438\u044E \u043F\u0430\u0440\u0430\u043C\u0435\u0442\u0440\u0430 \u0432\u044B\u0431\u0440\u0430\u043D\u043D\u043E\u0433\u043E \u044D\u043B\u0435\u043C\u0435\u043D\u0442\u0430.");
        }

        private static void BuildViewDecorationPanel(UIControlledApplication application)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, ViewDecorationPanelName);
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            AddPushButton<ArrangeGridsOnCurrentViewCommand>(
                panel,
                assemblyPath,
                "MyInstruments_ArrangeGridsOnCurrentView",
                "\u041E\u0441\u0438\n\u0432\u0438\u0434\u0430",
                "\u041F\u043E\u0434\u0440\u0435\u0437\u0430\u0435\u0442 \u043E\u0441\u0438 \u043D\u0430 \u0442\u0435\u043A\u0443\u0449\u0435\u043C \u0432\u0438\u0434\u0435 \u0438 \u0441\u043A\u0440\u044B\u0432\u0430\u0435\u0442 \u043B\u0438\u0448\u043D\u0438\u0435 \u043E\u0441\u0438.");
        }

        private static void BuildCommandLinePanel(UIControlledApplication application)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, CommandLinePanelName);
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            AddPushButton<OpenCommandLineCommand>(
                panel,
                assemblyPath,
                "MyInstruments_OpenCommandLine",
                "\u041A\u043E\u043C\u0430\u043D\u0434\u043D\u0430\u044F\n\u0441\u0442\u0440\u043E\u043A\u0430",
                "\u041E\u0442\u043A\u0440\u044B\u0432\u0430\u0435\u0442 \u0431\u044B\u0441\u0442\u0440\u044B\u0439 \u043F\u043E\u0438\u0441\u043A-\u043B\u0430\u0443\u043D\u0447\u0435\u0440 \u043A\u043E\u043C\u0430\u043D\u0434 \u043F\u043B\u0430\u0433\u0438\u043D\u0430.");

        }

        private static void BuildRebarPanel(UIControlledApplication application)
        {
            RibbonPanel panel = GetOrCreatePanel(application, TabName, RebarPanelName);
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            AddPushButton<ShowRebarAnchorageCommand>(
                panel,
                assemblyPath,
                "MyInstruments_ShowRebarAnchorage",
                "\u0410\u043D\u043A\u0435\u0440\u043E\u0432\u043A\u0430\n\u0430\u0440\u043C\u0430\u0442\u0443\u0440\u044B",
                "\u041E\u0442\u043A\u0440\u044B\u0432\u0430\u0435\u0442 \u0442\u0430\u0431\u043B\u0438\u0446\u0443 \u0430\u043D\u043A\u0435\u0440\u043E\u0432\u043A\u0438 \u0438 \u043D\u0430\u0445\u043B\u0435\u0441\u0442\u043A\u0438 \u0430\u0440\u043C\u0430\u0442\u0443\u0440\u044B.");

            AddPushButton<RebarSketchCommand>(
                panel,
                assemblyPath,
                "MyInstruments_RebarSketch",
                "\u042D\u0441\u043A\u0438\u0437\u044B\n\u0430\u0440\u043C\u0430\u0442\u0443\u0440\u044B",
                "\u0412\u044B\u0431\u0438\u0440\u0430\u0435\u0442 \u0441\u043F\u0435\u0446\u0438\u0444\u0438\u043A\u0430\u0446\u0438\u044E \u043D\u0430 \u043B\u0438\u0441\u0442\u0435 \u0438 \u0441\u043E\u0437\u0434\u0430\u0451\u0442 \u044D\u0441\u043A\u0438\u0437\u044B \u0434\u043B\u044F \u0432\u0445\u043E\u0434\u044F\u0449\u0435\u0439 \u0432 \u043D\u0435\u0451 \u0430\u0440\u043C\u0430\u0442\u0443\u0440\u044B.");
        }

        private static void EnsureTab(UIControlledApplication application, string tabName)
        {
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Revit throws when the tab already exists, for example after another add-in created it.
            }
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string tabName, string panelName)
        {
            foreach (RibbonPanel panel in application.GetRibbonPanels(tabName))
            {
                if (panel.Name == panelName)
                {
                    return panel;
                }
            }

            return application.CreateRibbonPanel(tabName, panelName);
        }

        private static void AddPushButton<TCommand>(
            PulldownButton parent,
            string assemblyPath,
            string name,
            string text,
            string toolTip)
            where TCommand : IExternalCommand
        {
            var data = new PushButtonData(name, text, assemblyPath, typeof(TCommand).FullName)
            {
                ToolTip = toolTip
            };

            parent.AddPushButton(data);
        }

        private static void AddPushButton<TCommand>(
            RibbonPanel panel,
            string assemblyPath,
            string name,
            string text,
            string toolTip)
            where TCommand : IExternalCommand
        {
            var data = new PushButtonData(name, text, assemblyPath, typeof(TCommand).FullName)
            {
                ToolTip = toolTip
            };

            panel.AddItem(data);
        }

    }
}
