namespace ContextFilter.Core.Services;

/// <summary>
/// Displays short user-facing messages from the view model layer.
/// </summary>
public interface INotificationService
{
    /// <summary>Displays a non-blocking status message.</summary>
    void ShowStatus(string message);
}
