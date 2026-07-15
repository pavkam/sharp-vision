// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Numerics;

using SharpVision.Terminal.Protocols;

/// <summary>Resolves effective style-property values through the theme cascade.</summary>
public static class ThemeResolver
{
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

        T? value;

        if (control.TryGetLocalValue(property, out T? local))
        {
            value = local;
        }
        else
        {
            value = property.DefaultValue;

            if (property.TryGetClassDefault(control.GetType(), out object? classDefault))
            {
                value = (T) classDefault!;
            }

            ThemeContext? themeContext = control.ThemeContext;
            List<Control> scopes = CollectStyleScopes(control);

            foreach (State state in ResolutionOrder(visualState))
            {
                ApplyState(control, property, state, themeContext, scopes, ref value);
            }
        }

        // Single collapse point: a role color (from a local value OR the theme cascade) becomes concrete,
        // so every consumer of this public overload sees a concrete color, never a deferred role.
        if (value is Color { Kind: ColorKind.Role } role)
        {
            ThemeContext? context = control.ThemeContext;
            value = (T) (object) SemanticColor.Resolve(
                role,
                r => context is not null && context.TryGetColor(r, out Color c) ? c : null);
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

        T? value = property.DefaultValue;

        if (property.TryGetClassDefault(controlType, out object? classDefault))
        {
            value = (T) classDefault!;
        }

        foreach (State state in ResolutionOrder(visualState))
        {
            ApplyChain(theme.GetStyleChain(controlType), property, state, ref value);
        }

        if (value is Color { Kind: ColorKind.Role } role)
        {
            value = (T) (object) SemanticColor.Resolve(
                role,
                r => theme.TryGetColor(r, out Color c) ? c : null);
        }

        return value;
    }

    /// <summary>Builds the ordered list of state keys to apply for one active visual state.</summary>
    /// <param name="visualState">The active visual-state flags.</param>
    /// <returns>
    /// Normal first, then every non-empty subset of the active overlays ordered by ascending
    /// specificity so a more specific (multi-flag) definition wins, with single-flag ties broken by
    /// <see cref="VisualStates.PrecedenceOrder"/>.
    /// </returns>
    private static List<State> ResolutionOrder(State visualState)
    {
        List<State> order = [State.Normal];
        List<State> active = [];

        foreach (State overlay in VisualStates.PrecedenceOrder)
        {
            if ((visualState & overlay) != 0)
            {
                active.Add(overlay);
            }
        }

        if (active.Count == 0)
        {
            return order;
        }

        List<State> combos = [];

        for (int mask = 1; mask < 1 << active.Count; mask++)
        {
            State combo = State.Normal;

            for (int index = 0; index < active.Count; index++)
            {
                if ((mask & (1 << index)) != 0)
                {
                    combo |= active[index];
                }
            }

            combos.Add(combo);
        }

        combos.Sort(CompareSpecificity);
        order.AddRange(combos);
        return order;
    }

    private static int CompareSpecificity(State left, State right)
    {
        int byCount = BitOperations.PopCount((uint) left).CompareTo(BitOperations.PopCount((uint) right));
        return byCount != 0 ? byCount : MaxRank(left).CompareTo(MaxRank(right));
    }

    private static int MaxRank(State state)
    {
        int rank = -1;

        foreach (State overlay in VisualStates.PrecedenceOrder)
        {
            if ((state & overlay) != 0)
            {
                rank = VisualStates.RankOf(overlay);
            }
        }

        return rank;
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
            for (int index = scopes.Count - 1; index >= 0; index--)
            {
                ApplyChain(context.GetStyleChain(scopes[index].GetType()), property, state, ref value);
            }
        }

        if (control.InstanceStyle is { } own && TryGetSnapshotValue(own, property, state, out object? ownValue))
        {
            value = (T) ownValue!;
        }

        for (int index = scopes.Count - 1; index >= 0; index--)
        {
            if (scopes[index].InstanceStyle is { } scopeStyle &&
                TryGetSnapshotValue(scopeStyle, property, state, out object? scopeValue))
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
        foreach (IControlStyle style in chain)
        {
            if (TryGetSnapshotValue(style, property, state, out object? themed))
            {
                value = (T) themed!;
            }
        }
    }

    private static List<Control> CollectStyleScopes(Control control)
    {
        List<Control>? scopes = null;

        for (Container? current = control.Parent; current is not null; current = current.Parent)
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
