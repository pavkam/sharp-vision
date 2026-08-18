// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Groups optional appearance contributions for normal and interactive visual states.</summary>
public readonly record struct AppearanceStatesOverlay
{
    /// <summary>Initializes a partial appearance overlay.</summary>
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
    public AppearanceStatesOverlay(
        AppearanceOverlay? normal = null,
        AppearanceOverlay? pointerOver = null,
        AppearanceOverlay? focusWithin = null,
        AppearanceOverlay? focused = null,
        AppearanceOverlay? current = null,
        AppearanceOverlay? selected = null,
        AppearanceOverlay? @checked = null,
        AppearanceOverlay? indeterminate = null,
        AppearanceOverlay? pressed = null,
        AppearanceOverlay? disabled = null)
    {
        Normal = normal;
        IsPointerOver = pointerOver;
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
    public AppearanceOverlay? Normal { get; }

    /// <summary>Gets the optional pointer-over contribution.</summary>
    public AppearanceOverlay? IsPointerOver { get; }

    /// <summary>Gets the optional descendant-focus contribution.</summary>
    public AppearanceOverlay? FocusWithin { get; }

    /// <summary>Gets the optional direct-focus contribution.</summary>
    public AppearanceOverlay? Focused { get; }

    /// <summary>Gets the optional current-item contribution.</summary>
    public AppearanceOverlay? Current { get; }

    /// <summary>Gets the optional selected-item contribution.</summary>
    public AppearanceOverlay? Selected { get; }

    /// <summary>Gets the optional checked contribution.</summary>
    public AppearanceOverlay? Checked { get; }

    /// <summary>Gets the optional indeterminate contribution.</summary>
    public AppearanceOverlay? Indeterminate { get; }

    /// <summary>Gets the optional pressed contribution.</summary>
    public AppearanceOverlay? Pressed { get; }

    /// <summary>Gets the optional disabled contribution.</summary>
    public AppearanceOverlay? Disabled { get; }

    /// <summary>Combines a later partial overlay onto this one.</summary>
    /// <param name="later">The later overlay whose supplied members win.</param>
    /// <returns>The combined partial overlay.</returns>
    public AppearanceStatesOverlay Overlay(AppearanceStatesOverlay later) => new(
        Overlay(Normal, later.Normal),
        Overlay(IsPointerOver, later.IsPointerOver),
        Overlay(FocusWithin, later.FocusWithin),
        Overlay(Focused, later.Focused),
        Overlay(Current, later.Current),
        Overlay(Selected, later.Selected),
        Overlay(Checked, later.Checked),
        Overlay(Indeterminate, later.Indeterminate),
        Overlay(Pressed, later.Pressed),
        Overlay(Disabled, later.Disabled));

    private static AppearanceOverlay? Overlay(AppearanceOverlay? earlier, AppearanceOverlay? later) =>
        later is null ? earlier : earlier?.Overlay(later.Value) ?? later;
}
