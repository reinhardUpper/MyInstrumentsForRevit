namespace ContextFilter.Core.Logging;

/// <summary>
/// Minimal logging abstraction used by all layers without binding Core to a logging framework.
/// </summary>
public interface ILogger
{
    /// <summary>Writes a debug-level diagnostic message.</summary>
    void Debug(string message);

    /// <summary>Writes an informational message.</summary>
    void Information(string message);

    /// <summary>Writes a warning message.</summary>
    void Warning(string message);

    /// <summary>Writes an error message with an optional exception.</summary>
    void Error(string message, Exception? exception = null);
}
