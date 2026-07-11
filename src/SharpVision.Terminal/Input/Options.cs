using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Input;

/// <summary>Defines finite immutable input decoding policy.</summary>
public sealed record Options
{
    /// <summary>Gets conservative default input policy.</summary>
    public static Options Default { get; } = new();

    /// <summary>Gets the lone-Escape ambiguity timeout.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive and finite.</exception>
    public TimeSpan EscapeTimeout
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The Escape timeout must be positive and finite.");
            }

            field = value;
        }
    } = TimeSpan.FromMilliseconds(50);

    /// <summary>Gets the positive maximum retained paste byte count.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int MaxPasteBytes
    {
        get;
        init => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The paste limit must be positive.");
    } = 16 * 1024 * 1024;

    /// <summary>Gets protocol parser limits used by the decoder.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public Limits Limits
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = Limits.Default;

    /// <summary>Gets optional positive cell-pixel dimensions for pixel mouse inference.</summary>
    public Metrics? CellMetrics { get; init; }
}
