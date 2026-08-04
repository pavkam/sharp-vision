// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Identifies which calendar or clock component an editable segment of a segmented
/// temporal field (<see cref="Controls.Input.DateInput"/>, <see cref="Controls.Input.TimeInput"/>,
/// <see cref="Controls.Input.DateTimeInput"/>) edits.</summary>
/// <remarks>
/// The engine (<see cref="SegmentFieldBehavior"/>) uses this value only to route a navigation,
/// digit-entry, increment, or clear request back to the owning control's own value arithmetic; it
/// carries no formatting or calendar semantics of its own.
/// </remarks>
internal enum TemporalSegmentKind
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

    /// <summary>The AM/PM designator of a 12-hour time.</summary>
    AmPmDesignator
}
