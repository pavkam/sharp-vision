namespace SharpVision.Threading;

/// <summary>Executes one observed dispatcher function.</summary>
/// <typeparam name="T">The function result type.</typeparam>
internal sealed class FunctionWork<T>(Func<T> function, CancellationToken cancellationToken): Work
{
    private readonly TaskCompletionSource<T> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the function completion and result.</summary>
    internal Task<T> Completion => _completion.Task;

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
            _ = _completion.TrySetResult(function());
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
