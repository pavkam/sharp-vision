// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

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
    [ValueRange(1, int.MaxValue)]
    public int MaxConcurrentQueries
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MaxConcurrentQueries));
            field = value;
        }
    } = 32;

    /// <summary>Gets the deadline applied to a terminal query before safe fallback. Bounded at
    /// 2,147,483,647 milliseconds (<see cref="int.MaxValue"/>), the tighter of the
    /// millisecond ceilings among the timers that ultimately schedule this deadline.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero, negative, infinite, or
    /// exceeds 2,147,483,647 milliseconds.</exception>
    public TimeSpan QueryTimeout
    {
        get;
        init => field = RequireFinitePositive(value, nameof(QueryTimeout));
    } = TimeSpan.FromMilliseconds(750);

    /// <summary>Gets the maximum name/value pairs accepted in one XTGETTCAP reply.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 256.</exception>
    [ValueRange(1, 256)]
    public int MaxCapabilityItems
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotPositiveOrGreaterThan(value, 256, nameof(MaxCapabilityItems));
            field = value;
        }
    } = 32;

    /// <summary>Gets the maximum decoded bytes accepted for one XTGETTCAP value.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 64 KiB.</exception>
    [ValueRange(1, 65_536)]
    public int MaxCapabilityValueBytes
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotPositiveOrGreaterThan(value, 65_536, nameof(MaxCapabilityValueBytes));
            field = value;
        }
    } = 4_096;

    private static TimeSpan RequireFinitePositive(TimeSpan value, string parameterName)
    {
        return value > TimeSpan.Zero
            && value != Timeout.InfiniteTimeSpan
            && value <= TimeSpan.FromMilliseconds(int.MaxValue)
            ? value
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The query timeout must be positive and within the dispatcher timer millisecond limit.");
    }
}
