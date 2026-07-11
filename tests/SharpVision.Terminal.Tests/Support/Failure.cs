namespace SharpVision.Terminal.Tests.Support;

/// <summary>Stores one deterministic transport failure and optional written prefix.</summary>
internal sealed record Failure
{
    /// <summary>Initializes one validated deterministic failure.</summary>
    /// <param name="exception">The exact failure to throw.</param>
    /// <param name="prefixBytes">The non-negative maximum prefix to commit first.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="prefixBytes"/> is negative.</exception>
    internal Failure(Exception exception, int prefixBytes)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentOutOfRangeException.ThrowIfNegative(prefixBytes);

        Exception = exception;
        PrefixBytes = prefixBytes;
    }

    /// <summary>Gets the exact failure to throw.</summary>
    internal Exception Exception { get; }

    /// <summary>Gets the maximum prefix to commit before throwing.</summary>
    internal int PrefixBytes { get; }
}
