// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines one complete immutable Button presentation.</summary>
[PublicAPI]
public readonly struct ButtonStyle: IEquatable<ButtonStyle>
{
    private static readonly ThemeProfile _standardAppearance = ControlStyleProfiles.Control;
    private readonly ThemeProfile? _appearance;
    private readonly Thickness? _padding;

    /// <summary>Initializes a complete Button presentation.</summary>
    /// <param name="padding">The non-negative internal content padding in cells.</param>
    /// <param name="appearance">The complete normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A reachable visual state has both a visible shadow and an enabled border side.</exception>
    public ButtonStyle(Thickness padding, ThemeProfile appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var invalidState = FindInvalidState(appearance);
        if (invalidState is { } state)
        {
            throw new ArgumentException(
                $"The appearance for visual state '{state}' has both a visible shadow and enabled border sides.",
                nameof(appearance));
        }

        _padding = padding;
        _appearance = appearance;
    }

    /// <summary>Gets the standard bordered Button presentation.</summary>
    public static ButtonStyle Standard => default;

    /// <summary>Gets the compact filled Button presentation with a fractional lower-right shadow.</summary>
    public static ButtonStyle Filled { get; } = CreateFilled();

    /// <summary>Gets the internal content padding in terminal cells.</summary>
    public Thickness Padding => _padding ?? new Thickness(horizontal: 1, vertical: 0);

    /// <summary>Gets the complete normal and visual-state appearance profile.</summary>
    public ThemeProfile Appearance => ResolveAppearance();

    /// <summary>Determines whether this value and another Button style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when the resolved padding and complete appearance profiles are equal.</returns>
    public bool Equals(ButtonStyle other) =>
        Padding == other.Padding && Appearance.Equals(other.Appearance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ButtonStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Padding, Appearance);

    /// <summary>Determines whether two Button styles resolve to the same presentation.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the resolved styles are equal.</returns>
    public static bool operator ==(ButtonStyle left, ButtonStyle right) => left.Equals(right);

    /// <summary>Determines whether two Button styles resolve to different presentations.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the resolved styles are different.</returns>
    public static bool operator !=(ButtonStyle left, ButtonStyle right) => !left.Equals(right);

    private static ButtonStyle CreateFilled()
    {
        var normal = new ThemeAppearance(
            _standardAppearance.Normal.Face,
            new Border(
                BorderSide.None,
                BorderGlyphStyle.Default,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            new Shadow(
                true,
                ShadowMode.FractionalBlock,
                new Point(1, 1),
                ControlGlyphs.Chrome.Shadow.Value,
                ThemeColor.ControlShadow,
                Color.Transparent,
                ThemeDecoration.Shadow));
        var appearance = new ThemeProfile(
            normal,
            _standardAppearance.PointerOver,
            _standardAppearance.FocusWithin,
            _standardAppearance.Focused,
            _standardAppearance.Current,
            _standardAppearance.Selected,
            _standardAppearance.Checked,
            _standardAppearance.Indeterminate,
            _standardAppearance.Pressed,
            _standardAppearance.Disabled);

        return new ButtonStyle(new Thickness(horizontal: 2, vertical: 0), appearance);
    }

    private ThemeProfile ResolveAppearance() => _appearance ?? _standardAppearance;

    /// <summary>Finds the first reachable state whose resolved chrome violates the Button invariant.</summary>
    /// <param name="appearance">The complete appearance profile to inspect.</param>
    /// <returns>The first invalid exact state, or <see langword="null"/> when every reachable state is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    internal static VisualState? FindInvalidState(ThemeProfile appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var combinations = 1 << VisualStateOrder.OrderedOverlays.Length;

        for (var flags = 0; flags < combinations; flags++)
        {
            var state = VisualState.Normal;

            for (var index = 0; index < VisualStateOrder.OrderedOverlays.Length; index++)
            {
                if ((flags & (1 << index)) != 0)
                {
                    state |= VisualStateOrder.OrderedOverlays[index];
                }
            }

            var resolved = appearance.Resolve(state);
            if (resolved.Shadow.IsVisible && resolved.Border.Sides != BorderSide.None)
            {
                return state;
            }
        }

        return null;
    }
}
