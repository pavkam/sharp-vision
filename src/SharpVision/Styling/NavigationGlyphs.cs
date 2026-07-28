// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines navigation-item, group-disclosure, and separator glyphs.</summary>
internal readonly record struct NavigationGlyphs
{
    /// <summary>Initializes the complete navigation glyph family.</summary>
    /// <param name="itemIdle">The idle item marker.</param>
    /// <param name="itemCurrent">The current or hovered item marker.</param>
    /// <param name="groupCollapsed">The collapsed-group marker.</param>
    /// <param name="groupExpanded">The expanded-group marker.</param>
    /// <param name="separator">The navigation separator glyph.</param>
    public NavigationGlyphs(
        ControlGlyph itemIdle,
        ControlGlyph itemCurrent,
        ControlGlyph groupCollapsed,
        ControlGlyph groupExpanded,
        ControlGlyph separator)
    {
        ItemIdle = itemIdle;
        ItemCurrent = itemCurrent;
        GroupCollapsed = groupCollapsed;
        GroupExpanded = groupExpanded;
        Separator = separator;
    }

    /// <summary>Gets the idle item marker.</summary>
    public ControlGlyph ItemIdle { get; }

    /// <summary>Gets the current or hovered item marker.</summary>
    public ControlGlyph ItemCurrent { get; }

    /// <summary>Gets the collapsed-group marker.</summary>
    public ControlGlyph GroupCollapsed { get; }

    /// <summary>Gets the expanded-group marker.</summary>
    public ControlGlyph GroupExpanded { get; }

    /// <summary>Gets the navigation separator glyph.</summary>
    public ControlGlyph Separator { get; }
}
