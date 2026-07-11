namespace SharpVision.Threading;

/// <summary>Executes one fire-and-observe dispatcher callback.</summary>
internal sealed class PostWork: Work
{
    private readonly Action _action;

    /// <summary>Initializes one validated fire-and-observe callback.</summary>
    /// <param name="action">The non-null callback to execute.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    internal PostWork(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
    }

    /// <inheritdoc/>
    internal override void Execute() => _action();
}
