using SharpVision.Controls;

namespace SharpVision.Styling;

/// <summary>Resolves effective style-property values through the theme cascade.</summary>
public static class ThemeResolver
{
    private static readonly State[] _overlayOrder =
    [
        State.Hovered,
        State.Focused,
        State.Checked,
        State.Pressed,
        State.Disabled,
    ];

    private static readonly List<Control> _noScopes = [];

    /// <summary>Resolves one property for a control using theme, style, and local values.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="control">The control receiving the value.</param>
    /// <param name="property">The registered style property.</param>
    /// <param name="visualState">The active visual-state flags.</param>
    /// <returns>The effective value.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// The property does not apply to the control's runtime type.
    /// </exception>
    public static T Resolve<T>(Control control, StyleProperty<T> property, State visualState)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(property);
        EnsureApplies(control, property);

        if (control.TryGetLocalValue(property, out var local))
        {
            return local;
        }

        var value = property.DefaultValue;

        if (property.TryGetClassDefault(control.GetType(), out var classDefault))
        {
            value = (T) classDefault!;
        }

        var context = control.ThemeContext;
        var scopes = CollectStyleScopes(control);

        ApplyState(control, property, State.Normal, context, scopes, ref value);

        foreach (var overlay in _overlayOrder)
        {
            if ((visualState & overlay) != 0)
            {
                ApplyState(control, property, overlay, context, scopes, ref value);
            }
        }

        return value;
    }

    /// <summary>Resolves one property for a control type under a theme without a live control.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="theme">The theme supplying styles.</param>
    /// <param name="controlType">The concrete control type being evaluated.</param>
    /// <param name="property">The registered style property.</param>
    /// <param name="visualState">The active visual-state flags.</param>
    /// <returns>The value from defaults, class defaults, and the theme cascade for the type.</returns>
    /// <remarks>
    /// Per-instance styles, explicit local values, and ancestor style scopes require a live control
    /// and are not applied here; this overload is intended for design-time and tooling queries.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The property does not apply to <paramref name="controlType"/>.</exception>
    public static T Resolve<T>(Theme theme, Type controlType, StyleProperty<T> property, State visualState)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(controlType);
        ArgumentNullException.ThrowIfNull(property);

        if (!property.AppliesTo(controlType))
        {
            throw new ArgumentException(
                $"The property '{property.Name}' does not apply to {controlType.Name}.",
                nameof(property));
        }

        var value = property.DefaultValue;

        if (property.TryGetClassDefault(controlType, out var classDefault))
        {
            value = (T) classDefault!;
        }

        ApplyChain(theme.GetStyleChain(controlType), property, State.Normal, ref value);

        foreach (var overlay in _overlayOrder)
        {
            if ((visualState & overlay) != 0)
            {
                ApplyChain(theme.GetStyleChain(controlType), property, overlay, ref value);
            }
        }

        return value;
    }

    private static void ApplyState<T>(
        Control control,
        StyleProperty<T> property,
        State state,
        ThemeContext? context,
        List<Control> scopes,
        ref T value)
    {
        if (context is not null)
        {
            ApplyChain(context.GetStyleChain(control.GetType()), property, state, ref value);

            // Farthest scope first so a nearer scope overrides a farther one.
            for (var index = scopes.Count - 1; index >= 0; index--)
            {
                ApplyChain(context.GetStyleChain(scopes[index].GetType()), property, state, ref value);
            }
        }

        if (control.InstanceStyle is { } own && TryGetSnapshotValue(own, property, state, out var ownValue))
        {
            value = (T) ownValue!;
        }

        for (var index = scopes.Count - 1; index >= 0; index--)
        {
            if (scopes[index].InstanceStyle is { } scopeStyle &&
                TryGetSnapshotValue(scopeStyle, property, state, out var scopeValue))
            {
                value = (T) scopeValue!;
            }
        }
    }

    private static void ApplyChain<T>(
        IReadOnlyList<IControlStyle> chain,
        StyleProperty<T> property,
        State state,
        ref T value)
    {
        foreach (var style in chain)
        {
            if (TryGetSnapshotValue(style, property, state, out var themed))
            {
                value = (T) themed!;
            }
        }
    }

    private static List<Control> CollectStyleScopes(Control control)
    {
        List<Control>? scopes = null;

        for (var current = control.Parent; current is not null; current = current.Parent)
        {
            if (current is IStyleScope)
            {
                (scopes ??= []).Add(current);
            }
        }

        return scopes ?? _noScopes;
    }

    private static void EnsureApplies<T>(Control control, StyleProperty<T> property)
    {
        if (!property.AppliesTo(control.GetType()))
        {
            throw new ArgumentException(
                $"The property '{property.Name}' does not apply to {control.GetType().Name}.",
                nameof(property));
        }
    }

    private static bool TryGetSnapshotValue(
        IControlStyle style,
        IStyleProperty property,
        State state,
        out object? value) =>
        style.TryGetValue(property, state, out value);
}
