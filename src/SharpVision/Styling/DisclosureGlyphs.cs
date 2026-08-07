// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines collapsed, expanded, and drop-down indicators.</summary>
internal readonly record struct DisclosureGlyphs
{
    /// <summary>Initializes the complete disclosure indicator family.</summary>
    /// <param name="collapsed">The collapsed indicator.</param>
    /// <param name="expanded">The expanded indicator.</param>
    /// <param name="dropDown">The drop-down indicator.</param>
    public DisclosureGlyphs(ControlGlyph collapsed, ControlGlyph expanded, ControlGlyph dropDown)
    {
        Collapsed = collapsed;
        Expanded = expanded;
        DropDown = dropDown;
    }

    /// <summary>Gets the collapsed indicator.</summary>
    public ControlGlyph Collapsed { get; }

    /// <summary>Gets the expanded indicator.</summary>
    public ControlGlyph Expanded { get; }

    /// <summary>Gets the drop-down indicator.</summary>
    public ControlGlyph DropDown { get; }
}
