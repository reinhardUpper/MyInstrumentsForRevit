using System.Reflection;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using ContextFilter.Core.Logging;
using ContextFilter.Core.Services;
using ContextFilter.Plugin.Docking;
using ContextFilter.Plugin.Infrastructure;
using ContextFilter.Plugin.RevitContext;
using ContextFilter.Plugin.Services;
using ContextFilter.UI;
using ContextFilter.UI.Services;
using ContextFilter.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ContextFilter.Plugin;

/// <summary>
/// Revit add-in entry point that wires dependency injection, ribbon UI, dockable pane, and selection tracking.
/// </summary>
public sealed class App : IExternalApplication
{
    private static ServiceProvider? _serviceProvider;
    private SelectionWatcher? _selectionWatcher;

    /// <summary>Gets the active service provider for command entry points.</summary>
    public static IServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException("Context Filter has not been started.");

    /// <inheritdoc />
    public Result OnStartup(UIControlledApplication application)
    {
        ThemeBootstrapper.EnsureLoaded();

        var services = new ServiceCollection();
        services.AddSingleton<ILogger, DebugLogger>();
        services.AddSingleton<IRevitContext, RevitContext.RevitContext>();
        services.AddSingleton<ExternalEventDispatcher>();
        services.AddSingleton<SelectionService>();
        services.AddSingleton<IContextFilterHost, ContextFilterHost>();
        services.AddSingleton<INotificationService, StatusNotificationService>();
        services.AddTransient<ContextFilterPaneViewModel>();
        services.AddTransient<ContextFilterPaneProvider>();

        _serviceProvider = services.BuildServiceProvider();

        RegisterDockablePane(application);
        CreateRibbon(application);

        _selectionWatcher = new SelectionWatcher(
            _serviceProvider.GetRequiredService<IContextFilterHost>(),
            _serviceProvider.GetRequiredService<ILogger>());
        application.Idling += OnIdling;

        return Result.Succeeded;
    }

    /// <inheritdoc />
    public Result OnShutdown(UIControlledApplication application)
    {
        application.Idling -= OnIdling;
        _selectionWatcher?.Dispose();
        _selectionWatcher = null;
        _serviceProvider?.Dispose();
        _serviceProvider = null;
        return Result.Succeeded;
    }

    /// <summary>Shows the registered dockable pane.</summary>
    public static void ShowPane()
    {
        var pane = new DockablePane(PaneIds.ContextFilterPaneId);
        pane.Show();
    }

    private void OnIdling(object? sender, IdlingEventArgs e)
    {
        if (sender is UIApplication uiApplication)
        {
            _selectionWatcher?.Tick(uiApplication);
        }
    }

    private static void RegisterDockablePane(UIControlledApplication application)
    {
        var provider = Services.GetRequiredService<ContextFilterPaneProvider>();
        application.RegisterDockablePane(PaneIds.ContextFilterPaneId, "Context Filter", provider);
    }

    private static void CreateRibbon(UIControlledApplication application)
    {
        const string tabName = "\u041C\u043E\u0438 \u0438\u043D\u0441\u0442\u0440\u0443\u043C\u0435\u043D\u0442\u044B";
        const string panelName = "\u0424\u0438\u043B\u044C\u0442\u0440 \u0438 \u0438\u0433\u0440\u044B";

        try
        {
            application.CreateRibbonTab(tabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
        }

        var panel = application.GetRibbonPanels(tabName).FirstOrDefault(item => item.Name == panelName)
            ?? application.CreateRibbonPanel(tabName, panelName);

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var contextFilterButton = new PushButtonData(
            "ContextFilter.ShowPane",
            "Context\nFilter",
            assemblyPath,
            typeof(ShowContextFilterCommand).FullName)
        {
            ToolTip = "Open the context filter dockable pane.",
            LongDescription = "Groups the current Revit selection by category, family, and type.",
            LargeImage = RibbonIconFactory.CreateIcon(32),
            Image = RibbonIconFactory.CreateIcon(16)
        };

        var snakeButton = new PushButtonData(
            "ContextFilter.Snake",
            "Snake",
            assemblyPath,
            typeof(ShowSnakeGameCommand).FullName)
        {
            ToolTip = "Open a classic snake game inside Revit.",
            LongDescription = "Starts a small WPF snake game window without touching the Revit document.",
            LargeImage = RibbonIconFactory.CreateSnakeIcon(32),
            Image = RibbonIconFactory.CreateSnakeIcon(16)
        };

        panel.AddItem(contextFilterButton);
        panel.AddItem(snakeButton);
    }

}
