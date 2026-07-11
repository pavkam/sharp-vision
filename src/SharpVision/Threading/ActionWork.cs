namespace SharpVision.Threading;

/// <summary>Executes one observed dispatcher action.</summary>
internal sealed class ActionWork: Work
{
    private readonly Action _action;
    private readonly CancellationToken _cancellationToken;
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Initializes one validated observed action.</summary>
    /// <param name="action">The non-null action to execute.</param>
    /// <param name="cancellationToken">The token observed before execution.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    internal ActionWork(Action action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        _action = action;
        _cancellationToken = cancellationToken;
    }

    /// <summary>Gets the action completion.</summary>
    internal Task Completion => _completion.Task;

    /// <inheritdoc/>
    internal override void Execute()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            _ = _completion.TrySetCanceled(_cancellationToken);
            return;
        }

        try
        {
            _action();
            _ = _completion.TrySetResult();
        }
        catch (Exception exception)
        {
            _ = _completion.TrySetException(exception);
        }
    }

    /// <inheritdoc/>
    internal override void Cancel() =>
        _completion.TrySetCanceled(new CancellationToken(canceled: true));
}
