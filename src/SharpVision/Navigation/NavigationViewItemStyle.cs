// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable navigation-entry presentation. This style's own
/// "navigationViewItem" theme key falls back to <see cref="ControlStyle"/>'s "control" key for
/// anything it does not author itself.</summary>
[PublicAPI]
public sealed record NavigationViewItemStyle: ControlStyle
{
    /// <summary>Gets the primary navigation-item style definition.</summary>
    internal static StyleDefinition<NavigationViewItemStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        Compare);

    private static NavigationViewItemStyle Complete(ControlStyle control, VisualState state) =>
        new(
            control.Face,
            control.Border,
            control.Shadow,
            ControlGlyphs.Navigation.ItemIdle.Value,
            ControlGlyphs.Navigation.ItemCurrent.Value);

    /// <summary>Initializes a complete navigation-entry presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="idleMarker">The printable one-cell marker for a non-current entry.</param>
    /// <param name="currentMarker">The printable one-cell marker for the current entry.</param>
    /// <exception cref="ArgumentException">A marker is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public NavigationViewItemStyle(Face face, Border border, Shadow shadow, Rune idleMarker, Rune currentMarker)
        : base(face, border, shadow)
    {
        IdleMarker = idleMarker;
        CurrentMarker = currentMarker;
    }

    /// <summary>Gets the standard navigation-entry presentation.</summary>
    public static new NavigationViewItemStyle Default => Complete(ControlStyle.Default, VisualState.Normal);

    /// <summary>Gets the marker drawn for a non-current entry.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune IdleMarker
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the marker drawn for the current entry.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune CurrentMarker
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    private static InvalidationImpact Compare(
        NavigationViewItemStyle previous,
        Theme? previousTheme,
        NavigationViewItemStyle current,
        Theme? currentTheme) =>
        previous != current
            ? InvalidationImpact.Render
            : InvalidationImpact.None;
}
