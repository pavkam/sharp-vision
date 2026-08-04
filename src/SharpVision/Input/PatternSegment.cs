// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Describes one token run or literal run parsed from a .NET custom date/time format
/// pattern by <see cref="TemporalPatternSegmenter"/>, before any control-specific numeric range or
/// digit-capacity policy is attached.</summary>
/// <remarks>
/// This is an intermediate representation: an owning control combines each entry with its own
/// per-kind maximum value to produce the final <see cref="SegmentDescriptor"/> layout used by
/// <see cref="SegmentFieldBehavior"/>.
/// </remarks>
internal readonly struct PatternSegment: IEquatable<PatternSegment>
{
    /// <summary>Initializes a literal run, such as a separator or other fixed pattern text.</summary>
    /// <param name="literalText">The literal text, already culture-resolved for date and time separators.</param>
    /// <exception cref="ArgumentNullException"><paramref name="literalText"/> is null.</exception>
    public PatternSegment(string literalText)
    {
        ArgumentNullException.ThrowIfNull(literalText);
        LiteralText = literalText;
        Kind = null;
        RunLength = 0;
    }

    /// <summary>Initializes an editable token run, such as "yyyy" or "HH".</summary>
    /// <param name="kind">The calendar or clock component the run represents.</param>
    /// <param name="runLength">The number of repeated pattern letters in the run.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="runLength"/> is not positive.</exception>
    public PatternSegment(TemporalSegmentKind kind, int runLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runLength);
        LiteralText = string.Empty;
        Kind = kind;
        RunLength = runLength;
    }

    /// <summary>Gets the literal text for a non-editable run, or the empty string for an editable run.</summary>
    public string LiteralText { get; }

    /// <summary>Gets the run's semantic kind, or null for a literal run.</summary>
    public TemporalSegmentKind? Kind { get; }

    /// <summary>Gets the number of repeated pattern letters in an editable run, or zero for a literal run.</summary>
    public int RunLength { get; }

    /// <inheritdoc/>
    public bool Equals(PatternSegment other) =>
        LiteralText == other.LiteralText && Kind == other.Kind && RunLength == other.RunLength;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PatternSegment other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(LiteralText, Kind, RunLength);

    /// <summary>Determines whether two pattern segments are equal.</summary>
    public static bool operator ==(PatternSegment left, PatternSegment right) => left.Equals(right);

    /// <summary>Determines whether two pattern segments are not equal.</summary>
    public static bool operator !=(PatternSegment left, PatternSegment right) => !left.Equals(right);
}
