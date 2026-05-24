using System.Collections.Concurrent;
using Autodesk.Revit.UI;
using ContextFilter.Core.Logging;

namespace ContextFilter.Plugin.Infrastructure;

/// <summary>
/// Serializes Revit API work onto Revit's ExternalEvent execution context.
/// </summary>
public sealed class ExternalEventDispatcher : IExternalEventHandler, IDisposable
{
    private readonly ConcurrentQueue<IExternalEventRequest> _queue = new();
    private readonly ILogger _logger;
    private readonly ExternalEvent _externalEvent;
    private bool _disposed;

    /// <summary>Creates a dispatcher and its Revit ExternalEvent.</summary>
    public ExternalEventDispatcher(ILogger logger)
    {
        _logger = logger;
        _externalEvent = ExternalEvent.Create(this);
    }

    /// <summary>Enqueues Revit API work and returns a task completed by the ExternalEvent handler.</summary>
    public Task<T> InvokeAsync<T>(Func<UIApplication, T> action, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ExternalEventDispatcher));
        }

        var request = new ExternalEventRequest<T>(action, cancellationToken);
        _queue.Enqueue(request);
        _externalEvent.Raise();
        return request.Task;
    }

    /// <summary>Enqueues Revit API work that does not return a value.</summary>
    public Task InvokeAsync(Action<UIApplication> action, CancellationToken cancellationToken = default)
    {
        return InvokeAsync(
            uiApplication =>
            {
                action(uiApplication);
                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public void Execute(UIApplication app)
    {
        while (_queue.TryDequeue(out var request))
        {
            try
            {
                request.Execute(app);
            }
            catch (Exception exception)
            {
                _logger.Error("ExternalEvent request failed.", exception);
                request.Fail(exception);
            }
        }
    }

    /// <inheritdoc />
    public string GetName() => "Context Filter External Event Dispatcher";

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _externalEvent.Dispose();
        _disposed = true;
    }

    private interface IExternalEventRequest
    {
        void Execute(UIApplication application);

        void Fail(Exception exception);
    }

    private sealed class ExternalEventRequest<T> : IExternalEventRequest
    {
        private readonly Func<UIApplication, T> _action;
        private readonly CancellationToken _cancellationToken;
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ExternalEventRequest(Func<UIApplication, T> action, CancellationToken cancellationToken)
        {
            _action = action;
            _cancellationToken = cancellationToken;
        }

        public Task<T> Task => _completion.Task;

        public void Execute(UIApplication application)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(_cancellationToken);
                return;
            }

            _completion.TrySetResult(_action(application));
        }

        public void Fail(Exception exception) => _completion.TrySetException(exception);
    }
}
