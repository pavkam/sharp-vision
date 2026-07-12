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

        if (context is not null)
        {
            foreach (var style in context.GetStyleChain(control.GetType()))
            {
                if (TryGetSnapshotValue(style, property, State.Normal, out var themed))
                {
                    value = (T) themed!;
                }
            }
        }

        if (control.InstanceStyle is { } instanceStyle)
        {
            if (TryGetSnapshotValue(instanceStyle, property, State.Normal, out var instance))
            {
                value = (T) instance!;
            }
        }

        foreach (var overlay in _overlayOrder)
        {
            if ((visualState & overlay) == 0)
            {
                continue;
            }

            if (context is not null)
            {
                foreach (var style in context.GetStyleChain(control.GetType()))
                {
                    if (TryGetSnapshotValue(style, property, overlay, out var themed))
                    {
                        value = (T) themed!;
                    }
                }
            }

            if (control.InstanceStyle is { } instanceStyle &&
                TryGetSnapshotValue(instanceStyle, property, overlay, out var instance))
            {
                value = (T) instance!;
            }
        }

        return value;
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
        style.TryGetSnapshotValue(property, state, out value);
}
