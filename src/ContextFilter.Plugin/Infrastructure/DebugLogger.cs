using System.Diagnostics;
using ContextFilter.Core.Logging;

namespace ContextFilter.Plugin.Infrastructure;

/// <summary>
/// Debug-output logger suitable for Revit add-in diagnostics.
/// </summary>
public sealed class DebugLogger : ILogger
{
    /// <inheritdoc />
    public void Debug(string message) => Trace.WriteLine($"[ContextFilter][DEBUG] {message}");

    /// <inheritdoc />
    public void Information(string message) => Trace.WriteLine($"[ContextFilter][INFO] {message}");

    /// <inheritdoc />
    public void Warning(string message) => Trace.WriteLine($"[ContextFilter][WARN] {message}");

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null) =>
        Trace.WriteLine($"[ContextFilter][ERROR] {message}{Environment.NewLine}{exception}");
}
