// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Identifies which calendar or clock component an editable segment of a segmented
/// temporal field (<see cref="Controls.Input.DateInput"/>, <see cref="Controls.Input.TimeInput"/>,
/// <see cref="Controls.Input.DateTimeInput"/>, or a third-party
/// <see cref="Controls.Input.TemporalInputBase{TValue}"/> derivative) edits.</summary>
/// <remarks>
/// The engine (<see cref="SegmentFieldBehavior"/>) uses this value only to route a navigation,
/// digit-entry, increment, or clear request back to the owning control's own value arithmetic; it
/// carries no formatting or calendar semantics of its own. Public so a derivative outside this
/// assembly can implement <see cref="Controls.Input.TemporalInputBase{TValue}"/>'s abstract
/// per-segment arithmetic seams, which classify and dispatch on this value, without needing
/// friend access to the internal segmented-field engine itself.
/// </remarks>
[PublicAPI]
public enum TemporalSegmentKind
{
    /// <summary>The calendar month component of a date.</summary>
    Month,

    /// <summary>The day-of-month component of a date.</summary>
    Day,

    /// <summary>The year component of a date.</summary>
    Year,

    /// <summary>The hour component of a time, in whatever 12- or 24-hour range the owning control applies.</summary>
    Hour,

    /// <summary>The minute component of a time.</summary>
    Minute,

    /// <summary>The second component of a time.</summary>
    Second,

    /// <summary>The fractional-second component of a time, at the precision declared by its format run.</summary>
    FractionalSecond,

    /// <summary>The AM/PM designator of a 12-hour time.</summary>
    AmPmDesignator
}
