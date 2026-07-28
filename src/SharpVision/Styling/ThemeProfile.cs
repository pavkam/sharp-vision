// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one global semantic role's normal appearance and state contributions.</summary>
public sealed class ThemeProfile
{
    private const VisualState _allStates =
        VisualState.PointerOver |
        VisualState.FocusWithin |
        VisualState.Focused |
        VisualState.Current |
        VisualState.Selected |
        VisualState.Checked |
        VisualState.Indeterminate |
        VisualState.Pressed |
        VisualState.Disabled;

    /// <summary>Initializes a complete semantic role profile.</summary>
    public ThemeProfile(
        ThemeAppearance normal,
        AppearanceSet pointerOver = default,
        AppearanceSet focusWithin = default,
        AppearanceSet focused = default,
        AppearanceSet current = default,
        AppearanceSet selected = default,
        AppearanceSet @checked = default,
        AppearanceSet indeterminate = default,
        AppearanceSet pressed = default,
        AppearanceSet disabled = default)
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

    /// <summary>Gets the complete normal appearance.</summary>
    public ThemeAppearance Normal { get; }

    /// <summary>Gets the pointer-over contribution.</summary>
    public AppearanceSet PointerOver { get; }

    /// <summary>Gets the descendant-focus contribution.</summary>
    public AppearanceSet FocusWithin { get; }

    /// <summary>Gets the direct-focus contribution.</summary>
    public AppearanceSet Focused { get; }

    /// <summary>Gets the current-item contribution.</summary>
    public AppearanceSet Current { get; }

    /// <summary>Gets the selected-item contribution.</summary>
    public AppearanceSet Selected { get; }

    /// <summary>Gets the checked contribution.</summary>
    public AppearanceSet Checked { get; }

    /// <summary>Gets the indeterminate contribution.</summary>
    public AppearanceSet Indeterminate { get; }

    /// <summary>Gets the pressed contribution.</summary>
    public AppearanceSet Pressed { get; }

    /// <summary>Gets the disabled contribution.</summary>
    public AppearanceSet Disabled { get; }

    /// <summary>Gets whether any state contribution can alter intrinsic chrome geometry.</summary>
    internal bool StateCanChangeChromeGeometry =>
        ChangesChromeGeometry(PointerOver) ||
        ChangesChromeGeometry(FocusWithin) ||
        ChangesChromeGeometry(Focused) ||
        ChangesChromeGeometry(Current) ||
        ChangesChromeGeometry(Selected) ||
        ChangesChromeGeometry(Checked) ||
        ChangesChromeGeometry(Indeterminate) ||
        ChangesChromeGeometry(Pressed) ||
        ChangesChromeGeometry(Disabled);

    /// <summary>Resolves the complete appearance for an exact set of visual-state flags.</summary>
    /// <param name="state">The exact local visual-state flags.</param>
    /// <returns>The complete composed semantic appearance.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> contains unknown flags.</exception>
    public ThemeAppearance Resolve(VisualState state)
        => ApplyStates(Normal, state);

    /// <summary>Applies this profile's state contributions to one earlier complete appearance.</summary>
    internal ThemeAppearance ApplyStates(ThemeAppearance appearance, VisualState state)
    {
        if ((state & ~_allStates) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The visual state contains unknown flags.");
        }

        var result = appearance;
        foreach (var overlay in VisualStateOrder.OrderedOverlays)
        {
            if ((state & overlay) != 0)
            {
                result = result.Apply(GetSet(overlay));
            }
        }

        return result;
    }

    private AppearanceSet GetSet(VisualState state) => state switch
    {
        VisualState.Normal => throw new UnreachableException(),
        VisualState.PointerOver => PointerOver,
        VisualState.FocusWithin => FocusWithin,
        VisualState.Focused => Focused,
        VisualState.Current => Current,
        VisualState.Selected => Selected,
        VisualState.Checked => Checked,
        VisualState.Indeterminate => Indeterminate,
        VisualState.Pressed => Pressed,
        VisualState.Disabled => Disabled,
        _ => throw new UnreachableException()
    };

    private static bool ChangesChromeGeometry(AppearanceSet set) =>
        set.Border?.Sides.HasValue == true ||
        set.Shadow?.IsVisible.HasValue == true ||
        set.Shadow?.Offset.HasValue == true;
}
