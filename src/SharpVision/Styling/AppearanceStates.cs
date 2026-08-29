// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one resolved style's complete normal appearance plus its nine partial
/// per-visual-state contributions - the render-facing shape every style type is converted into,
/// whichever of them produced it (see <see cref="StyleStates{TStyle}"/>). The asymmetry is
/// deliberate: Normal is complete, while each state slot is a sparse overlay that changes only what
/// it names, so simultaneously active states fold onto one another without any of them having to
/// restate the whole appearance.</summary>
public sealed class AppearanceStates: IEquatable<AppearanceStates>
{
    private const VisualState _allStates =
        VisualState.IsPointerOver |
        VisualState.FocusWithin |
        VisualState.Focused |
        VisualState.Current |
        VisualState.Selected |
        VisualState.Checked |
        VisualState.Indeterminate |
        VisualState.Pressed |
        VisualState.Disabled;

    /// <summary>Initializes a complete normal appearance and its per-state contributions.</summary>
    public AppearanceStates(
        ControlAppearance normal,
        AppearanceOverlay pointerOver = default,
        AppearanceOverlay focusWithin = default,
        AppearanceOverlay focused = default,
        AppearanceOverlay current = default,
        AppearanceOverlay selected = default,
        AppearanceOverlay @checked = default,
        AppearanceOverlay indeterminate = default,
        AppearanceOverlay pressed = default,
        AppearanceOverlay disabled = default)
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

    /// <summary>Gets the complete normal appearance.</summary>
    public ControlAppearance Normal { get; }

    /// <summary>Gets the pointer-over contribution.</summary>
    public AppearanceOverlay IsPointerOver { get; }

    /// <summary>Gets the descendant-focus contribution.</summary>
    public AppearanceOverlay FocusWithin { get; }

    /// <summary>Gets the direct-focus contribution.</summary>
    public AppearanceOverlay Focused { get; }

    /// <summary>Gets the current-item contribution.</summary>
    public AppearanceOverlay Current { get; }

    /// <summary>Gets the selected-item contribution.</summary>
    public AppearanceOverlay Selected { get; }

    /// <summary>Gets the checked contribution.</summary>
    public AppearanceOverlay Checked { get; }

    /// <summary>Gets the indeterminate contribution.</summary>
    public AppearanceOverlay Indeterminate { get; }

    /// <summary>Gets the pressed contribution.</summary>
    public AppearanceOverlay Pressed { get; }

    /// <summary>Gets the disabled contribution.</summary>
    public AppearanceOverlay Disabled { get; }

    /// <summary>Determines whether these states and another resolve the same complete appearance
    /// across every visual state.</summary>
    /// <param name="other">The other appearance states to compare, or null.</param>
    /// <returns><see langword="true"/> when every normal and state-contribution slot is equal.</returns>
    public bool Equals(AppearanceStates? other) =>
        other is not null &&
        (ReferenceEquals(this, other) ||
         (Normal.Equals(other.Normal) &&
          IsPointerOver.Equals(other.IsPointerOver) &&
          FocusWithin.Equals(other.FocusWithin) &&
          Focused.Equals(other.Focused) &&
          Current.Equals(other.Current) &&
          Selected.Equals(other.Selected) &&
          Checked.Equals(other.Checked) &&
          Indeterminate.Equals(other.Indeterminate) &&
          Pressed.Equals(other.Pressed) &&
          Disabled.Equals(other.Disabled)));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AppearanceStates other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Normal);
        hash.Add(IsPointerOver);
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

    /// <summary>Determines whether two appearance-states values resolve the same complete appearance.</summary>
    public static bool operator ==(AppearanceStates? left, AppearanceStates? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Determines whether two appearance-states values resolve different complete appearances.</summary>
    public static bool operator !=(AppearanceStates? left, AppearanceStates? right) => !(left == right);

    /// <summary>Gets whether any state contribution can alter intrinsic chrome geometry.</summary>
    internal bool StateCanChangeChromeGeometry =>
        ChangesChromeGeometry(IsPointerOver) ||
        ChangesChromeGeometry(FocusWithin) ||
        ChangesChromeGeometry(Focused) ||
        ChangesChromeGeometry(Current) ||
        ChangesChromeGeometry(Selected) ||
        ChangesChromeGeometry(Checked) ||
        ChangesChromeGeometry(Indeterminate) ||
        ChangesChromeGeometry(Pressed) ||
        ChangesChromeGeometry(Disabled);

    // Button is - today - the sole style whose own per-state completion swaps Border.Relief
    // (Raised/Sunken) as its chosen state-feedback mechanism; no bundled theme JSON authors a
    // per-state "relief" delta at all (only ever "normal"). A style that owns its state feedback
    // this way must not ALSO have a same-state Border.Foreground difference it never asked for -
    // inherited unchanged from a borrowed fallback's own per-state color, see ButtonStyle.Complete
    // - reinterpreted downstream as deliberate per-state authorship and painted flat, discarding
    // the very Raised/Sunken bezel this Relief switch exists to show. Scanning every slot, not just
    // the currently active one, is required: Button's own Relief only actually changes for
    // Pressed, but the same opt-out must cover Focused/IsPointerOver too, since their inherited
    // Foreground still differs from Normal even though their own Relief does not.
    internal bool StateAuthorsOwnRelief =>
        IsPointerOver.Border?.Relief.HasValue == true ||
        FocusWithin.Border?.Relief.HasValue == true ||
        Focused.Border?.Relief.HasValue == true ||
        Current.Border?.Relief.HasValue == true ||
        Selected.Border?.Relief.HasValue == true ||
        Checked.Border?.Relief.HasValue == true ||
        Indeterminate.Border?.Relief.HasValue == true ||
        Pressed.Border?.Relief.HasValue == true ||
        Disabled.Border?.Relief.HasValue == true;

    /// <summary>Resolves the complete appearance for an exact set of visual-state flags.</summary>
    /// <param name="state">The exact local visual-state flags.</param>
    /// <returns>The complete composed semantic appearance.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> contains unknown flags.</exception>
    [Pure]
    public ControlAppearance Resolve(VisualState state)
        => ApplyStates(Normal, state);

    /// <summary>Composes a partial overlay onto this complete set, member by member: every member
    /// the overlay supplies wins, and every member it omits keeps this set's value. A control that
    /// layers its own reusable appearance on top of whatever its theme resolved - a hyperlink's
    /// underline, a table presenter's grid - uses this rather than rebuilding a whole set.</summary>
    /// <param name="overlay">The partial contributions whose supplied members win.</param>
    /// <returns>A complete composed set.</returns>
    [Pure]
    public AppearanceStates Compose(AppearanceStatesOverlay overlay) =>
        new(
            overlay.Normal is null ? Normal : Normal.Apply(overlay.Normal.Value),
            Compose(IsPointerOver, overlay.IsPointerOver),
            Compose(FocusWithin, overlay.FocusWithin),
            Compose(Focused, overlay.Focused),
            Compose(Current, overlay.Current),
            Compose(Selected, overlay.Selected),
            Compose(Checked, overlay.Checked),
            Compose(Indeterminate, overlay.Indeterminate),
            Compose(Pressed, overlay.Pressed),
            Compose(Disabled, overlay.Disabled));

    private static AppearanceOverlay Compose(AppearanceOverlay baseline, AppearanceOverlay? overlay) =>
        overlay is null ? baseline : baseline.Overlay(overlay.Value);

    /// <summary>Applies these states' contributions to one earlier complete appearance.</summary>
    [Pure]
    internal ControlAppearance ApplyStates(ControlAppearance appearance, VisualState state)
    {
        // Deliberately NOT ArgumentOutOfRangeException.ThrowIfUndefinedFlags here: this runs on
        // every appearance resolution, and the generic helper's Convert.ToUInt64 boxes both
        // operands on every call, which is invisible in a functional test but pushed
        // GetActualFace's first-resolution allocation budget over its gate (see
        // ResolvedAppearancePerformanceTests). The direct bitwise check below costs nothing since
        // VisualState's operators never box.
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
        var combined = AppearanceOverlay.Empty;

        foreach (var overlay in VisualStateOrder.OrderedOverlays)
        {
            if ((state & overlay) != 0)
            {
                combined = combined.Overlay(GetSet(overlay));
            }
        }

        return appearance.Apply(combined);
    }

    private AppearanceOverlay GetSet(VisualState state) => state switch
    {
        VisualState.Normal => throw new UnreachableException(),
        VisualState.IsPointerOver => IsPointerOver,
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

    /// <summary>Reports whether one partial contribution can move the control's visual footprint,
    /// and so needs Measure rather than Render.</summary>
    /// <remarks>
    /// The single definition of that question. It used to be answered in three places that
    /// disagreed: the two theme-side predicates listed Sides/IsVisible/Offset, and the two
    /// local-overlay checks listed Sides alone, so the same footprint change classified as Measure
    /// when a theme expressed it and Render when application code did.
    ///
    /// <para><c>Shadow.Mode</c> belongs here and was in none of them. ControlChrome's
    /// ExpandVisualBounds picks half-row arithmetic for FractionalBlock and a whole-cell shift
    /// otherwise, so mode alone resizes the visual-overflow rectangle - the identical-magnitude
    /// change spelled as an offset was already classified Measure.</para>
    /// </remarks>
    /// <param name="set">The partial contribution to classify.</param>
    /// <returns>Whether applying it can change the footprint.</returns>
    [Pure]
    internal static bool ChangesChromeGeometry(AppearanceOverlay set) =>
        set.Border?.Sides.HasValue == true ||
        set.Shadow?.IsVisible.HasValue == true ||
        set.Shadow?.Offset.HasValue == true ||
        set.Shadow?.Mode.HasValue == true;
}
