// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>An <see cref="InputBase"/> derivative that enables only segment editing, with a single
/// editable hour-like segment backed by a plain integer, proving the capability composes without
/// press activation or a popup - mirroring how <see cref="TimeInput"/> uses it
/// today.</summary>
internal sealed class SegmentOnlyInputProbe: InputBase
{
    private readonly SegmentFieldBehavior _segments;

    /// <summary>Initializes a probe with only segment editing enabled.</summary>
    internal SegmentOnlyInputProbe() =>
        _segments = EnableSegmentEditing(BuildSegments, ApplyDigit, Increment, Clear);

    /// <summary>Gets the current backing value, clamped to 0 through 23.</summary>
    internal int Value { get; private set; }

    /// <summary>Attempts to enable segment editing a second time.</summary>
    internal void EnableSegmentEditingAgain() =>
        _ = EnableSegmentEditing(BuildSegments, ApplyDigit, Increment, Clear);

    /// <summary>Moves the active segment through the protected seam.</summary>
    internal bool MoveSegment(int direction, bool wrap) => _segments.MoveSegment(direction, wrap);

    /// <summary>Types one digit through the protected seam.</summary>
    internal bool TypeDigit(int digit) => _segments.TypeDigit(digit);

    /// <summary>Increments the active segment through the protected seam.</summary>
    internal bool IncrementSegment(int delta) => _segments.Increment(delta);

    private SegmentDescriptor[] BuildSegments() =>
        [new SegmentDescriptor(Value.ToString("D2", CultureInfo.InvariantCulture), TemporalSegmentKind.Hour, 2, 23)];

    private bool ApplyDigit(TemporalSegmentKind kind, int digit)
    {
        _ = kind;
        var clamped = Math.Clamp(digit, 0, 23);

        if (clamped == Value)
        {
            return false;
        }

        Value = clamped;
        return true;
    }

    private bool Increment(TemporalSegmentKind kind, int delta)
    {
        _ = kind;
        var clamped = Math.Clamp(Value + delta, 0, 23);

        if (clamped == Value)
        {
            return false;
        }

        Value = clamped;
        return true;
    }

    private bool Clear(TemporalSegmentKind kind)
    {
        _ = kind;

        if (Value == 0)
        {
            return false;
        }

        Value = 0;
        return true;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(2, 1);
    }
}
