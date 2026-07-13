// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Provides helpers for theme and control-style test setup.</summary>
internal static class ThemeTestSupport
{
    /// <summary>Creates one mutable control style with optional configuration.</summary>
    /// <param name="configure">Optional style mutation invoked before return.</param>
    /// <returns>The configured style instance.</returns>
    internal static ControlStyle<Control> CreateControlStyle(Action<ControlStyle<Control>>? configure = null)
    {
        ControlStyle<Control> style = new();
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
        ControlStyle<TControl> style = new();
        configure?.Invoke(style);
        return style;
    }

    /// <summary>Copies one overlay layer into a typed control style.</summary>
    /// <typeparam name="TControl">The targeted control type.</typeparam>
    /// <param name="style">The style receiving the values.</param>
    /// <param name="state">The visual state being configured.</param>
    /// <param name="overlay">The optional overlay fields to copy.</param>
    internal static void ApplyOverlay<TControl>(
        ControlStyle<TControl> style,
        State state,
        ThemeOverlay overlay)
        where TControl : Control
    {
        ArgumentNullException.ThrowIfNull(style);

        if (overlay.Foreground.HasValue)
        {
            style.Set(Control.ForegroundProperty, state, overlay.Foreground);
        }

        if (overlay.Background.HasValue)
        {
            style.Set(Control.BackgroundProperty, state, overlay.Background);
        }

        if (overlay.Attributes.HasValue)
        {
            style.Set(Control.AttributesProperty, state, overlay.Attributes);
        }

        if (overlay.Underline.HasValue)
        {
            style.Set(Control.UnderlineProperty, state, overlay.Underline);
        }

        if (overlay.UnderlineColor.HasValue)
        {
            style.Set(Control.UnderlineColorProperty, state, overlay.UnderlineColor);
        }

        if (overlay.Padding.HasValue)
        {
            style.Set(Control.PaddingProperty, state, overlay.Padding.Value);
        }

        if (overlay.BorderColor.HasValue)
        {
            style.Set(Control.BorderColorProperty, state, overlay.BorderColor);
        }
    }

    /// <summary>Creates one per-instance overlay style from themed overlay layers.</summary>
    /// <typeparam name="TControl">The targeted control type.</typeparam>
    /// <param name="layers">Ordered state and overlay pairs.</param>
    /// <returns>The configured overlay style.</returns>
    internal static ControlStyle<TControl> OverlayStyle<TControl>(
        params (State State, ThemeOverlay Overlay)[] layers)
        where TControl : Control
    {
        ControlStyle<TControl> style = new();

        foreach ((State state, ThemeOverlay overlay) in layers)
        {
            ApplyOverlay(style, state, overlay);
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

        ThemeContext context = ThemeContext.Create(theme);
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
