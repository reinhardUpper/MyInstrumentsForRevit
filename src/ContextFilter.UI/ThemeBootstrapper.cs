using System.Windows;

namespace ContextFilter.UI;

/// <summary>
/// Loads WPF resources needed by the dockable pane inside the host Revit process.
/// </summary>
public static class ThemeBootstrapper
{
    private static bool _isLoaded;

    /// <summary>Ensures the dark theme resource dictionary is available to pane views.</summary>
    public static void EnsureLoaded()
    {
        var application = Application.Current ?? new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        if (_isLoaded)
        {
            return;
        }

        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/ContextFilter.UI;component/Themes/DarkTheme.xaml", UriKind.Relative)
        });
        _isLoaded = true;
    }
}
