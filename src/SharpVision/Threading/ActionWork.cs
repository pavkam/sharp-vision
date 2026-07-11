namespace SharpVision.Threading;

/// <summary>Executes one observed dispatcher action.</summary>
internal sealed class ActionWork(Action action, CancellationToken cancellationToken): Work
{
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the action completion.</summary>
    internal Task Completion => _completion.Task;

    /// <inheritdoc/>
    internal override void Execute()
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _ = _completion.TrySetCanceled(cancellationToken);
            return;
        }

        try
        {
            action();
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
