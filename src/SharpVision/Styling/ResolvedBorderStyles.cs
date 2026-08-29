// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Holds the concrete terminal style for every physical border edge.</summary>
internal readonly record struct ResolvedBorderStyles
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

    /// <summary>Creates edge styles from one fully literal resolved border and the active relief palette.</summary>
    internal static ResolvedBorderStyles Create(Border border, Theme? theme)
    {
        var background = border.Background.Literal;
        var attributes = border.Attributes.Literal;
        var foreground = border.Foreground.Literal;
        var highlight = new ControlColor(SemanticColor.ReliefHighlight).Resolve(theme);
        var shade = new ControlColor(SemanticColor.ReliefShade).Resolve(theme);
        var (top, right, bottom, left) = border.Relief switch
        {
            BorderRelief.Flat => (foreground, foreground, foreground, foreground),
            BorderRelief.Raised => (highlight, shade, shade, highlight),
            BorderRelief.Sunken => (shade, highlight, highlight, shade),
            _ => throw new UnreachableException("Border validates relief before resolution.")
        };
        return new ResolvedBorderStyles(
            new TerminalStyle(top, background, attributes),
            new TerminalStyle(right, background, attributes),
            new TerminalStyle(bottom, background, attributes),
            new TerminalStyle(left, background, attributes));
    }
}
