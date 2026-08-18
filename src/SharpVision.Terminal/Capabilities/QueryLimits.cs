// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Defines finite limits for outstanding terminal capability queries.</summary>
/// <remarks>
/// Instances are immutable after construction. Use a <see langword="with"/>
/// expression to derive a stricter or more permissive profile. Every limit
/// must remain positive; boundedness cannot be disabled.
/// </remarks>
/// <example>
/// <code>
/// var limits = QueryLimits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
/// </code>
/// </example>
[PublicAPI]
public sealed record QueryLimits
{
    /// <summary>Gets the conservative limits used when no profile is supplied.</summary>
    public static QueryLimits Default { get; } = new();

    /// <summary>Gets the maximum number of capability or clipboard queries in flight.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int MaxConcurrentQueries
    {
        get;
        init => field = RequirePositive(value, nameof(MaxConcurrentQueries));
    } = 32;

    /// <summary>Gets the deadline applied to a terminal query before safe fallback.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero, negative, or infinite.</exception>
    public TimeSpan QueryTimeout
    {
        get;
        init => field = RequireFinitePositive(value, nameof(QueryTimeout));
    } = TimeSpan.FromMilliseconds(750);

    /// <summary>Gets the maximum name/value pairs accepted in one XTGETTCAP reply.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 256.</exception>
    public int MaxCapabilityItems
    {
        get;
        init => field = RequireBoundedPositive(value, 256, nameof(MaxCapabilityItems));
    } = 32;

    /// <summary>Gets the maximum decoded bytes accepted for one XTGETTCAP value.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 64 KiB.</exception>
    public int MaxCapabilityValueBytes
    {
        get;
        init => field = RequireBoundedPositive(value, 65_536, nameof(MaxCapabilityValueBytes));
    } = 4_096;

    private static int RequirePositive(int value, string parameterName)
    {
        return value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The limit must be positive.");
    }

    private static int RequireBoundedPositive(int value, int maximum, string parameterName)
    {
        return value is > 0 && value <= maximum
            ? value
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The limit must be positive and no greater than {maximum}.");
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
