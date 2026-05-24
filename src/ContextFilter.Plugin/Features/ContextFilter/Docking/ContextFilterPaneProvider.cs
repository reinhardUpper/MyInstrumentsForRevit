using Autodesk.Revit.UI;
using ContextFilter.UI.ViewModels;
using ContextFilter.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ContextFilter.Plugin.Docking;

/// <summary>
/// Creates the WPF framework element hosted by Revit as a dockable pane.
/// </summary>
public sealed class ContextFilterPaneProvider : IDockablePaneProvider
{
    private readonly IServiceProvider _services;

    /// <summary>Creates a pane provider.</summary>
    public ContextFilterPaneProvider(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public void SetupDockablePane(DockablePaneProviderData data)
    {
        var view = new ContextFilterPaneView
        {
            DataContext = _services.GetRequiredService<ContextFilterPaneViewModel>()
        };

        data.FrameworkElement = view;
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right
        };
    }
}
