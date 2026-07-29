// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

/// <summary>Wraps a specimen in a rounded bordered card.</summary>
internal sealed class DocCard: CompositeControl
{
    /// <summary>Initializes a rounded card around one specimen.</summary>
    /// <param name="child">The specimen to frame.</param>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    internal DocCard(Control child)
    {
        ArgumentNullException.ThrowIfNull(child);
        InitializeContent(new Dock
        {
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { child }
        });
    }
}
