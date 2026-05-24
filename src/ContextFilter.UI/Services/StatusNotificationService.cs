using ContextFilter.Core.Services;

namespace ContextFilter.UI.Services;

/// <summary>
/// Default notification sink. The pane already exposes status text, so this service is intentionally quiet.
/// </summary>
public sealed class StatusNotificationService : INotificationService
{
    /// <inheritdoc />
    public void ShowStatus(string message)
    {
    }
}
