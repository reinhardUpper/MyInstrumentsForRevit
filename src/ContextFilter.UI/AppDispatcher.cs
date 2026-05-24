using System.Windows;
using System.Windows.Threading;

namespace ContextFilter.UI;

/// <summary>
/// Small WPF dispatcher facade that keeps view models testable and UI-thread safe.
/// </summary>
public static class AppDispatcher
{
    /// <summary>Invokes the action on the WPF dispatcher when required.</summary>
    public static void Invoke(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
