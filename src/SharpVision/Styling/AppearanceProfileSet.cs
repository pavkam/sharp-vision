// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Groups optional appearance contributions for normal and interactive visual states.</summary>
public readonly record struct AppearanceProfileSet
{
    /// <summary>Initializes a partial appearance profile.</summary>
    /// <param name="normal">The optional normal contribution.</param>
    /// <param name="pointerOver">The optional pointer-over contribution.</param>
    /// <param name="focusWithin">The optional descendant-focus contribution.</param>
    /// <param name="focused">The optional direct-focus contribution.</param>
    /// <param name="current">The optional current-item contribution.</param>
    /// <param name="selected">The optional selected-item contribution.</param>
    /// <param name="checked">The optional checked contribution.</param>
    /// <param name="indeterminate">The optional indeterminate contribution.</param>
    /// <param name="pressed">The optional pressed contribution.</param>
    /// <param name="disabled">The optional disabled contribution.</param>
    public AppearanceProfileSet(
        AppearanceSet? normal = null,
        AppearanceSet? pointerOver = null,
        AppearanceSet? focusWithin = null,
        AppearanceSet? focused = null,
        AppearanceSet? current = null,
        AppearanceSet? selected = null,
        AppearanceSet? @checked = null,
        AppearanceSet? indeterminate = null,
        AppearanceSet? pressed = null,
        AppearanceSet? disabled = null)
    {
        Normal = normal;
        PointerOver = pointerOver;
        FocusWithin = focusWithin;
        Focused = focused;
        Current = current;
        Selected = selected;
        Checked = @checked;
        Indeterminate = indeterminate;
        Pressed = pressed;
        Disabled = disabled;
    }

    /// <summary>Gets the optional normal contribution.</summary>
    public AppearanceSet? Normal { get; }

    /// <summary>Gets the optional pointer-over contribution.</summary>
    public AppearanceSet? PointerOver { get; }

    /// <summary>Gets the optional descendant-focus contribution.</summary>
    public AppearanceSet? FocusWithin { get; }

    /// <summary>Gets the optional direct-focus contribution.</summary>
    public AppearanceSet? Focused { get; }

    /// <summary>Gets the optional current-item contribution.</summary>
    public AppearanceSet? Current { get; }

    /// <summary>Gets the optional selected-item contribution.</summary>
    public AppearanceSet? Selected { get; }

    /// <summary>Gets the optional checked contribution.</summary>
    public AppearanceSet? Checked { get; }

    /// <summary>Gets the optional indeterminate contribution.</summary>
    public AppearanceSet? Indeterminate { get; }

    /// <summary>Gets the optional pressed contribution.</summary>
    public AppearanceSet? Pressed { get; }

    /// <summary>Gets the optional disabled contribution.</summary>
    public AppearanceSet? Disabled { get; }

    /// <summary>Overlays a later partial profile over this profile.</summary>
    /// <param name="later">The later profile whose supplied members win.</param>
    /// <returns>The combined partial profile.</returns>
    public AppearanceProfileSet Overlay(AppearanceProfileSet later) => new(
        Overlay(Normal, later.Normal),
        Overlay(PointerOver, later.PointerOver),
        Overlay(FocusWithin, later.FocusWithin),
        Overlay(Focused, later.Focused),
        Overlay(Current, later.Current),
        Overlay(Selected, later.Selected),
        Overlay(Checked, later.Checked),
        Overlay(Indeterminate, later.Indeterminate),
        Overlay(Pressed, later.Pressed),
        Overlay(Disabled, later.Disabled));

    private static AppearanceSet? Overlay(AppearanceSet? earlier, AppearanceSet? later) =>
        later is null ? earlier : earlier?.Overlay(later.Value) ?? later;
}
