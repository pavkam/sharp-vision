// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Holds the concrete terminal style for every physical border edge.</summary>
internal readonly struct ResolvedBorderStyles
{
    /// <summary>Initializes concrete styles for every physical edge.</summary>
    internal ResolvedBorderStyles(
        TerminalStyle top,
        TerminalStyle right,
        TerminalStyle bottom,
        TerminalStyle left)
    {
        Top = top;
        Right = right;
        Bottom = bottom;
        Left = left;
    }

    /// <summary>Gets the top-edge style, which also owns both top corners.</summary>
    internal TerminalStyle Top { get; }

    /// <summary>Gets the right-edge style.</summary>
    internal TerminalStyle Right { get; }

    /// <summary>Gets the bottom-edge style, which also owns both bottom corners.</summary>
    internal TerminalStyle Bottom { get; }

    /// <summary>Gets the left-edge style.</summary>
    internal TerminalStyle Left { get; }

    /// <summary>Creates edge styles from one fully literal resolved border.</summary>
    internal static ResolvedBorderStyles Create(Border border)
    {
        var background = border.Background.Literal;
        var attributes = border.Attributes.Literal;
        var edges = border.EdgeColors;
        return new ResolvedBorderStyles(
            new TerminalStyle(edges.ResolveTop(border.Foreground).Literal, background, attributes),
            new TerminalStyle(edges.ResolveRight(border.Foreground).Literal, background, attributes),
            new TerminalStyle(edges.ResolveBottom(border.Foreground).Literal, background, attributes),
            new TerminalStyle(edges.ResolveLeft(border.Foreground).Literal, background, attributes));
    }
}
