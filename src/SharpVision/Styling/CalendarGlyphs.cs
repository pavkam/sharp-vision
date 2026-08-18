// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines previous- and next-month navigation arrow glyphs.</summary>
internal readonly record struct CalendarGlyphs
{
    /// <summary>Initializes the complete month-navigation glyph family.</summary>
    /// <param name="previousMonth">The previous-month arrow.</param>
    /// <param name="nextMonth">The next-month arrow.</param>
    public CalendarGlyphs(ControlGlyph previousMonth, ControlGlyph nextMonth)
    {
        PreviousMonth = previousMonth;
        NextMonth = nextMonth;
    }

    /// <summary>Gets the previous-month arrow.</summary>
    public ControlGlyph PreviousMonth { get; }

    /// <summary>Gets the next-month arrow.</summary>
    public ControlGlyph NextMonth { get; }
}
