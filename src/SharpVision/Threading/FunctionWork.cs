namespace SharpVision.Threading;

/// <summary>Executes one observed dispatcher function.</summary>
/// <typeparam name="T">The function result type.</typeparam>
internal sealed class FunctionWork<T>: Work
{
    private readonly Func<T> _function;
    private readonly CancellationToken _cancellationToken;
    private readonly TaskCompletionSource<T> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Initializes one validated observed function.</summary>
    /// <param name="function">The non-null function to execute.</param>
    /// <param name="cancellationToken">The token observed before execution.</param>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is null.</exception>
    internal FunctionWork(Func<T> function, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(function);

        _function = function;
        _cancellationToken = cancellationToken;
    }

    /// <summary>Gets the function completion and result.</summary>
    internal Task<T> Completion => _completion.Task;

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
            _ = _completion.TrySetResult(_function());
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
