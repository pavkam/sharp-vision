// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one global semantic role's normal appearance and state contributions.</summary>
public sealed class ThemeProfile: IEquatable<ThemeProfile>
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

    /// <summary>Determines whether this profile and another resolve the same complete appearance
    /// across every visual state.</summary>
    /// <param name="other">The other profile to compare, or null.</param>
    /// <returns><see langword="true"/> when every normal and state-contribution slot is equal.</returns>
    public bool Equals(ThemeProfile? other) =>
        other is not null &&
        (ReferenceEquals(this, other) ||
         (Normal.Equals(other.Normal) &&
          PointerOver.Equals(other.PointerOver) &&
          FocusWithin.Equals(other.FocusWithin) &&
          Focused.Equals(other.Focused) &&
          Current.Equals(other.Current) &&
          Selected.Equals(other.Selected) &&
          Checked.Equals(other.Checked) &&
          Indeterminate.Equals(other.Indeterminate) &&
          Pressed.Equals(other.Pressed) &&
          Disabled.Equals(other.Disabled)));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ThemeProfile other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Normal);
        hash.Add(PointerOver);
        hash.Add(FocusWithin);
        hash.Add(Focused);
        hash.Add(Current);
        hash.Add(Selected);
        hash.Add(Checked);
        hash.Add(Indeterminate);
        hash.Add(Pressed);
        hash.Add(Disabled);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two profiles resolve the same complete appearance.</summary>
    public static bool operator ==(ThemeProfile? left, ThemeProfile? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Determines whether two profiles resolve different complete appearances.</summary>
    public static bool operator !=(ThemeProfile? left, ThemeProfile? right) => !(left == right);

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

        // Folding one overlay at a time and validating the resulting Face on every intermediate
        // step (via appearance.Apply -> new Face(...)) can reject a partial combination that a
        // later overlay in OrderedOverlays would have gone on to resolve cleanly, even though the
        // final fold of every active state is itself entirely valid. Overlaying the contributions
        // as plain, unvalidated data first and constructing the Face only once, from the complete
        // fold, makes acceptance depend on the actual final appearance rather than on
        // OrderedOverlays' internal sequencing.
        var combined = AppearanceSet.Empty;

        foreach (var overlay in VisualStateOrder.OrderedOverlays)
        {
            if ((state & overlay) != 0)
            {
                combined = combined.Overlay(GetSet(overlay));
            }
        }

        return appearance.Apply(combined);
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
