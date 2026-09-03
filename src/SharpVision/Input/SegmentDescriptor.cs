// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Describes one position in a segmented temporal field's rendered layout: either a fixed
/// literal (a separator or other pattern text) or an editable segment identified by
/// <see cref="TemporalSegmentKind"/>.</summary>
/// <remarks>
/// A control builds an ordered array of descriptors on every layout-affecting access (mirroring
/// how <see cref="Controls.Input.DateInput"/> already rebuilds its display segments per render),
/// typically from a culture- or format-driven pattern via <see cref="TemporalPatternSegmenter"/>.
/// <see cref="SegmentFieldBehavior"/> reads only <see cref="Text"/>, <see cref="Kind"/>, and
/// <see cref="DigitCapacity"/> to drive navigation, digit buffering, and pointer hit-testing; it
/// never reaches into a control's underlying value.
/// </remarks>
internal readonly struct SegmentDescriptor: IEquatable<SegmentDescriptor>
{
    /// <summary>Initializes a literal (non-editable) segment.</summary>
    /// <param name="text">The literal text occupying this position, such as a date or time separator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public SegmentDescriptor(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Kind = null;
        DigitCapacity = 0;
        MaxValue = 0;
    }

    /// <summary>Initializes an editable segment.</summary>
    /// <param name="text">The currently rendered text for the segment, such as a zero-padded number or a designator.</param>
    /// <param name="kind">The calendar or clock component this segment edits.</param>
    /// <param name="digitCapacity">
    /// The number of digits the segment accepts before a typed value auto-commits and advances,
    /// or zero for a segment that is not directly digit-typable (for example an AM/PM designator,
    /// which is only reachable via <see cref="SegmentFieldBehavior.Increment"/>).
    /// </param>
    /// <param name="maxValue">
    /// The largest numeric value the segment can hold, used only to compute the single-digit
    /// entry that should auto-commit immediately instead of buffering for a second digit. Ignored
    /// when <paramref name="digitCapacity"/> is zero.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="digitCapacity"/> is outside 0-7.</exception>
    public SegmentDescriptor(string text, TemporalSegmentKind kind, int digitCapacity, int maxValue)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (digitCapacity is < 0 or > 7)
        {
            throw new ArgumentOutOfRangeException(
                nameof(digitCapacity),
                digitCapacity,
                "An editable segment's digit capacity must be between 0 (not digit-typable) and 7.");
        }

        Text = text;
        Kind = kind;
        DigitCapacity = digitCapacity;
        MaxValue = maxValue;
    }

    /// <summary>Gets the currently rendered text for this segment.</summary>
    public string Text { get; }

    /// <summary>Gets the segment's semantic kind, or null when this is a fixed literal.</summary>
    public TemporalSegmentKind? Kind { get; }

    /// <summary>Gets whether this segment can receive focus and edits.</summary>
    public bool IsEditable => Kind.HasValue;

    /// <summary>Gets whether typed digits are accepted directly by this segment.</summary>
    public bool IsDigitTypable => DigitCapacity > 0;

    /// <summary>Gets the number of digits accepted before the segment auto-commits, or zero.</summary>
    public int DigitCapacity { get; }

    /// <summary>Gets the largest numeric value the segment can hold. Meaningful only when <see cref="IsDigitTypable"/> is true.</summary>
    public int MaxValue { get; }

    /// <summary>Gets the first-keystroke digit value above which a typed digit auto-commits
    /// immediately instead of waiting for a second digit (for example typing "5" for a two-digit
    /// minute field commits at once, since no valid minute starts with a digit above 5).</summary>
    public int FirstDigitOverflowThreshold => DigitCapacity switch
    {
        > 2 => 9,
        2 => MaxValue / 10,
        _ => -1
    };

    /// <inheritdoc/>
    public bool Equals(SegmentDescriptor other) =>
        Text == other.Text && Kind == other.Kind && DigitCapacity == other.DigitCapacity && MaxValue == other.MaxValue;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SegmentDescriptor other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Text, Kind, DigitCapacity, MaxValue);

    /// <summary>Determines whether two descriptors are equal.</summary>
    public static bool operator ==(SegmentDescriptor left, SegmentDescriptor right) => left.Equals(right);

    /// <summary>Determines whether two descriptors are not equal.</summary>
    public static bool operator !=(SegmentDescriptor left, SegmentDescriptor right) => !left.Equals(right);
}
