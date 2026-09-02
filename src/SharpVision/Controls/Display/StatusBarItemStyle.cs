// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable status-bar entry presentation. This style declares no
/// theme section of its own: it falls back to <see cref="ControlStyle"/>'s "control" role section
/// for its passive chrome, resolves its own separator glyphs from code-owned defaults, and is
/// themeable only through that fallback and a locally assigned
/// <see cref="StatusBarItem.Style"/>.</summary>
/// <remarks>
/// Named for the item rather than the bar because the separators are the item's own chrome:
/// <see cref="StatusBar"/> itself draws none. Keying it "statusBar" would have put members under a
/// section whose control never reads them, which is the blessed-but-unread shape themes.md says the
/// section-granularity rules exist to prevent.
/// </remarks>
[PublicAPI]
public sealed record StatusBarItemStyle: ControlStyle
{
    /// <summary>Gets the primary status-bar-entry style definition. Falls back to
    /// <see cref="ControlStyle"/>'s "control" role section; both separator glyphs are code-owned,
    /// shared by every item.</summary>
    internal static StyleDefinition<StatusBarItemStyle> Definition { get; } = StyleDefinitions.ControlWithThemeOwnedStateDefaults(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, _, current, _) =>
            previous != current ? InvalidationImpact.Render : InvalidationImpact.None);

    private static StatusBarItemStyle Complete(ControlStyle control, VisualState state, Theme theme)
    {
        var states = theme.GetStyleSet(ControlStyle.Default);
        return new StatusBarItemStyle(
            BarAppearance.CompleteFace(control, state, states),
            control.Border,
            control.Shadow,
            StatusBarSeparatorGlyphs.Bar,
            StatusBarSeparatorGlyphs.Bar);
    }

    /// <summary>Initializes a complete status-bar entry presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="leftSeparatorGlyph">The printable one-cell glyph drawn before the content.</param>
    /// <param name="rightSeparatorGlyph">The printable one-cell glyph drawn after the content.</param>
    /// <exception cref="ArgumentException">A separator is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public StatusBarItemStyle(
        Face face,
        Border border,
        Shadow shadow,
        Rune leftSeparatorGlyph,
        Rune rightSeparatorGlyph) : base(face, border, shadow)
    {
        LeftSeparatorGlyph = leftSeparatorGlyph;
        RightSeparatorGlyph = rightSeparatorGlyph;
    }

    /// <summary>Gets the standard status-bar entry presentation.</summary>
    public static new StatusBarItemStyle Default => Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the glyph drawn before the retained content when a leading separator is shown.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune LeftSeparatorGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the glyph drawn after the retained content when a trailing separator is shown.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune RightSeparatorGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }
}
