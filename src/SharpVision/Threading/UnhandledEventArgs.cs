namespace SharpVision.Threading;

/// <summary>Describes one dispatcher callback failure and its continuation policy.</summary>
public sealed class UnhandledEventArgs: EventArgs
{
    /// <summary>Initializes a dispatcher failure value.</summary>
    /// <param name="exception">The non-null original callback exception.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    public UnhandledEventArgs(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
    }

    /// <summary>Gets the original callback exception.</summary>
    public Exception Exception { get; }

    /// <summary>Gets or sets whether the dispatcher may continue processing work.</summary>
    public bool Handled { get; set; }
}
