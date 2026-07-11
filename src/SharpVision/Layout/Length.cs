namespace SharpVision.Layout;

/// <summary>Represents one immutable fixed, percentage, intrinsic, or weighted length.</summary>
/// <example>
/// <code>
/// var fixedWidth = Length.Cells(20);
/// var halfWidth = Length.Percent(50);
/// var remaining = Length.Star(1);
/// </code>
/// </example>
public readonly record struct Length
{
    /// <summary>Initializes and validates one layout length.</summary>
    /// <param name="kind">The resolution strategy.</param>
    /// <param name="value">
    /// Zero for automatic, an integer for cells, 0 through 100 for percent, or
    /// a positive weight for star.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The kind is unknown or the numeric value is outside its finite range.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// An automatic length carries a non-zero value.
    /// </exception>
    public Length(Kind kind, double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The length value must be finite.");
        }

        switch (kind)
        {
            case Kind.Auto:
                if (value != 0)
                {
                    throw new ArgumentException(
                        "An automatic length cannot carry a numeric value.",
                        nameof(value));
                }

                break;
            case Kind.Cells:
                if (value < 0 || value != Math.Truncate(value))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "A fixed length must be a non-negative whole cell count.");
                }

                break;
            case Kind.Percent:
                if (value is < 0 or > 100)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "A percentage must be between zero and one hundred.");
                }

                break;
            case Kind.Star:
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "A proportional weight must be positive.");
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "The length kind is unknown.");
        }

        Kind = kind;
        Value = value;
    }

    /// <summary>Gets the intrinsic automatic length.</summary>
    public static Length Auto => default;

    /// <summary>Creates a fixed terminal-cell length.</summary>
    /// <param name="value">The non-negative cell count.</param>
    /// <returns>The fixed length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
    public static Length Cells(int value) => new(Kind.Cells, value);

    /// <summary>Creates a percentage of the final containing content extent.</summary>
    /// <param name="value">The finite percentage from zero through one hundred.</param>
    /// <returns>The percentage length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The percentage is invalid.</exception>
    public static Length Percent(double value) => new(Kind.Percent, value);

    /// <summary>Creates a weighted share of remaining bounded space.</summary>
    /// <param name="value">The finite positive weight.</param>
    /// <returns>The proportional length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The weight is not positive and finite.</exception>
    public static Length Star(double value) => new(Kind.Star, value);

    /// <summary>Gets the resolution strategy.</summary>
    public Kind Kind { get; }

    /// <summary>Gets the validated numeric payload for the strategy.</summary>
    public double Value { get; }
}
