using SharpVision.Controls;
using SharpVision.Styling;

namespace SharpVision.Tests.Support;

/// <summary>Provides helpers for theme and control-style test setup.</summary>
internal static class ThemeTestSupport
{
    /// <summary>Creates one mutable control style with optional configuration.</summary>
    /// <param name="configure">Optional style mutation invoked before return.</param>
    /// <returns>The configured style instance.</returns>
    internal static ControlStyle<Control> CreateControlStyle(Action<ControlStyle<Control>>? configure = null)
    {
        var style = new ControlStyle<Control>();
        configure?.Invoke(style);
        return style;
    }

    /// <summary>Creates one mutable style for a concrete control type.</summary>
    /// <typeparam name="TControl">The targeted control type.</typeparam>
    /// <param name="configure">Optional style mutation invoked before return.</param>
    /// <returns>The configured style instance.</returns>
    internal static ControlStyle<TControl> CreateStyle<TControl>(Action<ControlStyle<TControl>>? configure = null)
        where TControl : Control
    {
        var style = new ControlStyle<TControl>();
        configure?.Invoke(style);
        return style;
    }

    /// <summary>Copies one legacy appearance overlay into a typed control style.</summary>
    /// <typeparam name="TControl">The targeted control type.</typeparam>
    /// <param name="style">The style receiving the values.</param>
    /// <param name="state">The visual state being configured.</param>
    /// <param name="appearance">The optional appearance fields to copy.</param>
    internal static void ApplyAppearance<TControl>(
        ControlStyle<TControl> style,
        State state,
        Appearance appearance)
        where TControl : Control
    {
        ArgumentNullException.ThrowIfNull(style);

        if (appearance.Foreground.HasValue)
        {
            style.Set(Control.ForegroundProperty, state, appearance.Foreground);
        }

        if (appearance.Background.HasValue)
        {
            style.Set(Control.BackgroundProperty, state, appearance.Background);
        }

        if (appearance.Attributes.HasValue)
        {
            style.Set(Control.AttributesProperty, state, appearance.Attributes);
        }

        if (appearance.Underline.HasValue)
        {
            style.Set(Control.UnderlineProperty, state, appearance.Underline);
        }

        if (appearance.UnderlineColor.HasValue)
        {
            style.Set(Control.UnderlineColorProperty, state, appearance.UnderlineColor);
        }

        if (appearance.Padding.HasValue)
        {
            style.Set(Control.PaddingProperty, state, appearance.Padding.Value);
        }

        if (appearance.BorderColor.HasValue)
        {
            style.Set(Control.BorderColorProperty, state, appearance.BorderColor);
        }
    }

    /// <summary>Creates one per-instance overlay style from legacy appearance layers.</summary>
    /// <typeparam name="TControl">The targeted control type.</typeparam>
    /// <param name="layers">Ordered state and appearance pairs.</param>
    /// <returns>The configured overlay style.</returns>
    internal static ControlStyle<TControl> OverlayStyle<TControl>(
        params (State State, Appearance Appearance)[] layers)
        where TControl : Control
    {
        var style = new ControlStyle<TControl>();

        foreach (var (state, appearance) in layers)
        {
            ApplyAppearance(style, state, appearance);
        }

        return style;
    }

    /// <summary>Applies one theme snapshot to a control subtree.</summary>
    /// <param name="root">The non-null root control.</param>
    /// <param name="theme">The theme whose snapshot is published.</param>
    internal static void ApplyTheme(Control root, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(theme);

        var context = ThemeContext.Create(theme);
        ApplyThemeContext(root, context);
    }

    /// <summary>Republishes the latest theme snapshot to a control subtree.</summary>
    /// <param name="root">The non-null root control.</param>
    /// <param name="theme">The theme whose snapshot is republished.</param>
    internal static void RefreshTheme(Control root, Theme theme) => ApplyTheme(root, theme);

    /// <summary>Resolves one property for an explicit visual-state flag set.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="control">The non-null control.</param>
    /// <param name="property">The registered style property.</param>
    /// <param name="visualState">The active visual-state flags.</param>
    /// <returns>The effective value.</returns>
    internal static T Resolve<T>(Control control, StyleProperty<T> property, State visualState) =>
        ThemeResolver.Resolve(control, property, visualState);

    private static void ApplyThemeContext(Control control, ThemeContext context)
    {
        control.SetThemeContext(context);
        control.VisitChildren(child => ApplyThemeContext(child, context));
    }
}
