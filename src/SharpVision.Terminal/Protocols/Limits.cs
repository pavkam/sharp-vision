namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Defines finite limits for protocol parsing, clipboard transactions, and
/// capability queries.
/// </summary>
/// <remarks>
/// Instances are immutable after construction. Use a <see langword="with"/>
/// expression to derive a stricter or more permissive profile. Every limit must
/// remain positive; boundedness cannot be disabled.
/// </remarks>
/// <example>
/// <code>
/// var limits = Limits.Default with { MaxStringBytes = 64 * 1024 };
/// </code>
/// </example>
public sealed record Limits
{
    /// <summary>
    /// Gets the conservative limits used when no profile is supplied.
    /// </summary>
    public static Limits Default { get; } = new();

    /// <summary>
    /// Gets the maximum retained CSI or DCS parameter bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxParameterBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxParameterBytes));
    } = 256;

    /// <summary>
    /// Gets the maximum retained ESC, CSI, or DCS intermediate bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxIntermediateBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxIntermediateBytes));
    } = 16;

    /// <summary>
    /// Gets the maximum retained OSC, DCS, APC, PM, or SOS payload bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxStringBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxStringBytes));
    } = 1_048_576;

    /// <summary>
    /// Gets the maximum decoded clipboard bytes retained by one transaction.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxClipboardBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxClipboardBytes));
    } = 16_777_216;

    /// <summary>
    /// Gets the maximum OSC 5522 metadata bytes accepted in one packet.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxMetadataBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxMetadataBytes));
    } = 8_192;

    /// <summary>
    /// Gets the maximum number of capability or clipboard queries in flight.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxConcurrentQueries
    {
        get;
        init => field = RequirePositive(value, nameof(MaxConcurrentQueries));
    } = 32;

    /// <summary>
    /// Gets the deadline applied to a terminal query before safe fallback.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is zero, negative, or infinite.
    /// </exception>
    public TimeSpan QueryTimeout
    {
        get;
        init => field = RequireFinitePositive(value, nameof(QueryTimeout));
    } = TimeSpan.FromMilliseconds(750);

    /// <summary>
    /// Gets whether BEL may terminate an incoming OSC string.
    /// </summary>
    public bool AcceptBellTerminatedOsc { get; init; } = true;

    /// <summary>
    /// Gets whether raw C1 bytes are controls rather than UTF-8 data.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/> so a UTF-8 continuation byte is
    /// never reinterpreted as a C1 introducer.
    /// </remarks>
    public bool AcceptEightBitControls { get; init; }

    private static int RequirePositive(int value, string parameterName)
    {
        return value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The limit must be positive.");
    }

    private static TimeSpan RequireFinitePositive(TimeSpan value, string parameterName)
    {
        return value > TimeSpan.Zero && value != Timeout.InfiniteTimeSpan
            ? value
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The query timeout must be finite and positive.");
    }
}
