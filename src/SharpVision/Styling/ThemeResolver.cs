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

            ApplyOwnerListTheme(context, control, property, State.Normal, ref value);
        }

        if (control.InstanceStyle is { } instanceStyle)
        {
            if (TryGetSnapshotValue(instanceStyle, property, State.Normal, out var instance))
            {
                value = (T) instance!;
            }
        }

        ApplyOwnerListInstanceStyle(control, property, State.Normal, ref value);

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

                ApplyOwnerListTheme(context, control, property, overlay, ref value);
            }

            if (control.InstanceStyle is { } overlayStyle &&
                TryGetSnapshotValue(overlayStyle, property, overlay, out var overlayValue))
            {
                value = (T) overlayValue!;
            }

            ApplyOwnerListInstanceStyle(control, property, overlay, ref value);
        }

        return value;
    }

    private static void ApplyOwnerListTheme<T>(
        ThemeContext context,
        Control control,
        StyleProperty<T> property,
        State state,
        ref T value)
    {
        if (TryFindOwningList(control) is null)
        {
            return;
        }

        foreach (var style in context.GetStyleChain(typeof(List)))
        {
            if (TryGetSnapshotValue(style, property, state, out var themed))
            {
                value = (T) themed!;
            }
        }
    }

    private static void ApplyOwnerListInstanceStyle<T>(
        Control control,
        StyleProperty<T> property,
        State state,
        ref T value)
    {
        if (TryFindOwningList(control)?.InstanceStyle is not { } ownerStyle)
        {
            return;
        }

        if (TryGetSnapshotValue(ownerStyle, property, state, out var owner))
        {
            value = (T) owner!;
        }
    }

    private static List? TryFindOwningList(Control control)
    {
        for (var current = control.Parent; current is not null; current = current.Parent)
        {
            if (current is List list)
            {
                return list;
            }
        }

        return null;
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
