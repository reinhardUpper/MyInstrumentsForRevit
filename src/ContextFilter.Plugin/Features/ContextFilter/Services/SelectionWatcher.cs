using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ContextFilter.Core.Logging;
using ContextFilter.Core.Models;
using ContextFilter.Core.Services;

namespace ContextFilter.Plugin.Services;

/// <summary>
/// Lightweight Idling-based selection watcher that avoids rebuilding the tree unless ids change.
/// </summary>
public sealed class SelectionWatcher : IDisposable
{
    private readonly IContextFilterHost _host;
    private readonly ILogger _logger;
    private string _lastSignature = string.Empty;
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private bool _refreshInFlight;
    private bool _disposed;

    /// <summary>Creates the watcher.</summary>
    public SelectionWatcher(IContextFilterHost host, ILogger logger)
    {
        _host = host;
        _logger = logger;
    }

    /// <summary>Checks the current selection signature and schedules a refresh when it changes.</summary>
    public void Tick(UIApplication uiApplication)
    {
        if (_disposed || _refreshInFlight)
        {
            return;
        }

        if (_host.ElementSource != FilterElementSource.SelectedElements)
        {
            return;
        }

        if (DateTimeOffset.Now - _lastRefresh < TimeSpan.FromMilliseconds(250))
        {
            return;
        }

        var uiDocument = uiApplication.ActiveUIDocument;
        if (uiDocument is null)
        {
            return;
        }

        var signature = BuildSignature(uiDocument.Selection.GetElementIds());
        if (signature == _lastSignature)
        {
            return;
        }

        _lastSignature = signature;
        _lastRefresh = DateTimeOffset.Now;
        _refreshInFlight = true;
        _ = RefreshAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }

    private async Task RefreshAsync()
    {
        try
        {
            await _host.RefreshSelectionAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.Error("Failed to refresh selection snapshot.", exception);
        }
        finally
        {
            _refreshInFlight = false;
        }
    }

    private static string BuildSignature(ICollection<ElementId> ids)
    {
        if (ids.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(";", ids.OrderBy(id => id.IntegerValue).Select(id => id.IntegerValue.ToString()));
    }
}
