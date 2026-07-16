// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Numerics;


/// <summary>Resolves effective style-property values through the theme cascade.</summary>
public static class ThemeResolver
{
    private static readonly List<Control> _noScopes = [];

    #region Public resolution

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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="visualState"/> contains an unknown flag.
    /// </exception>
    public static T Resolve<T>(Control control, StyleProperty<T> property, State visualState)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(property);
        ValidateVisualState(visualState);
        EnsureApplies(control, property);

        T? value;

        if (control.TryGetLocalValue(property, out var local))
        {
            value = local;
        }
        else
        {
            value = property.DefaultValue;

            if (property.TryGetClassDefault(control.GetType(), out var classDefault))
            {
                value = (T) classDefault!;
            }

            var themeContext = control.ThemeContext;
            var scopes = CollectStyleScopes(control);
            var states = ResolutionOrder(visualState);
            ApplyCascade(control, property, states, themeContext, scopes, ref value);
        }

        // Collapse semantic colors only after local and cascade precedence is
        // complete, so no consumer observes a deferred role value.
        if (value is Color { Kind: ColorKind.Role } role)
        {
            var context = control.ThemeContext;
            value = (T) (object) SemanticColor.Resolve(
                role,
                r => context is not null && context.TryGetColor(r, out var c) ? c : null);
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="visualState"/> contains an unknown flag.
    /// </exception>
    public static T Resolve<T>(Theme theme, Type controlType, StyleProperty<T> property, State visualState)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(controlType);
        ArgumentNullException.ThrowIfNull(property);
        ValidateVisualState(visualState);

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

        var states = ResolutionOrder(visualState);
        ApplyChain(theme.GetStyleChain(controlType), property, states, ref value);

        if (value is Color { Kind: ColorKind.Role } role)
        {
            value = (T) (object) SemanticColor.Resolve(
                role,
                r => theme.TryGetColor(r, out var c) ? c : null);
        }

        return value;
    }

    #endregion

    #region Visual-state ordering

    /// <summary>Builds the ordered list of state keys to apply for one active visual state.</summary>
    /// <param name="visualState">The active visual-state flags.</param>
    /// <returns>
    /// Normal first, then every non-empty subset of the active overlays ordered by ascending
    /// specificity so a more specific (multi-flag) definition wins, with single-flag ties broken by
    /// <see cref="VisualStates.PrecedenceOrder"/>.
    /// </returns>
    private static List<State> ResolutionOrder(State visualState)
    {
        Debug.Assert((visualState & ~VisualStates.Overlays) == 0, "Resolution receives only known visual-state flags.");

        var order = new List<State> { State.Normal };
        var active = new List<State>();

        foreach (var overlay in VisualStates.PrecedenceOrder)
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

        // Seven known overlays cap this power set at 127 entries. Enumerating
        // every active subset lets increasingly specific combinations override
        // their component states deterministically.
        var combos = new List<State>();

        for (var mask = 1; mask < 1 << active.Count; mask++)
        {
            var combo = State.Normal;

            for (var index = 0; index < active.Count; index++)
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
        Debug.Assert(left != State.Normal && right != State.Normal, "Only overlay combinations are specificity-sorted.");

        var byCount = BitOperations.PopCount((uint) left).CompareTo(BitOperations.PopCount((uint) right));
        return byCount != 0 ? byCount : MaxRank(left).CompareTo(MaxRank(right));
    }

    private static int MaxRank(State state)
    {
        Debug.Assert((state & ~VisualStates.Overlays) == 0, "State ranking receives only known overlays.");

        var rank = -1;

        foreach (var overlay in VisualStates.PrecedenceOrder)
        {
            if ((state & overlay) != 0)
            {
                rank = VisualStates.RankOf(overlay);
            }
        }

        return rank;
    }

    #endregion

    #region Cascade application

    private static void ApplyCascade<T>(
        Control control,
        StyleProperty<T> property,
        IReadOnlyList<State> states,
        ThemeContext? context,
        List<Control> scopes,
        ref T value)
    {
        Debug.Assert(control is not null, "Cascade application requires a live control.");
        Debug.Assert(property is not null, "Cascade application requires registered property metadata.");
        Debug.Assert(states is not null, "Cascade application requires visual-state precedence.");
        Debug.Assert(scopes is not null, "Cascade application requires a scope collection.");

        if (context is not null)
        {
            // Farthest scope first so a nearer scope overrides a farther one.
            for (var index = scopes.Count - 1; index >= 0; index--)
            {
                ApplyChain(context.GetStyleChain(scopes[index].GetType()), property, states, ref value);
            }

            ApplyChain(context.GetStyleChain(control.GetType()), property, states, ref value);
        }

        // Scope instance styles are also resources. Apply them farthest to
        // nearest before the descendant's own, highest-priority style.
        for (var index = scopes.Count - 1; index >= 0; index--)
        {
            if (scopes[index].InstanceStyle is { } scopeStyle)
            {
                ApplyStyle(scopeStyle, property, states, ref value);
            }
        }

        if (control.InstanceStyle is { } own)
        {
            ApplyStyle(own, property, states, ref value);
        }
    }

    private static void ApplyChain<T>(
        IReadOnlyList<IControlStyle> chain,
        StyleProperty<T> property,
        IReadOnlyList<State> states,
        ref T value)
    {
        Debug.Assert(chain is not null, "Theme cascade application requires a style chain.");
        Debug.Assert(property is not null, "Theme cascade application requires property metadata.");
        Debug.Assert(states is not null, "Theme cascade application requires visual-state precedence.");

        foreach (var style in chain)
        {
            ApplyStyle(style, property, states, ref value);
        }
    }

    private static void ApplyStyle<T>(
        IControlStyle style,
        StyleProperty<T> property,
        IReadOnlyList<State> states,
        ref T value)
    {
        Debug.Assert(style is not null, "Style cascade application requires a style layer.");
        Debug.Assert(property is not null, "Style cascade application requires property metadata.");
        Debug.Assert(states is not null, "Style cascade application requires visual-state precedence.");

        // Resolve the best matching state inside this layer before advancing
        // to the next layer. A higher layer's Normal value must therefore beat
        // every state-specific value from a lower layer.
        foreach (var state in states)
        {
            if (TryGetSnapshotValue(style, property, state, out var styled))
            {
                value = (T) styled!;
            }
        }
    }

    private static List<Control> CollectStyleScopes(Control control)
    {
        Debug.Assert(control is not null, "Scope collection requires a live control.");

        List<Control>? scopes = null;

        for (var current = control.Parent; current is not null; current = current.Parent)
        {
            // Popups create a visually separate surface; their content must not
            // inherit style scopes (like selection colors) from above the popup.
            if (current is Popup)
            {
                break;
            }

            if (current is IStyleScope)
            {
                (scopes ??= []).Add(current);
            }
        }

        return scopes ?? _noScopes;
    }

    #endregion

    #region Validation

    private static void EnsureApplies<T>(Control control, StyleProperty<T> property)
    {
        Debug.Assert(control is not null, "Property applicability requires a live control.");
        Debug.Assert(property is not null, "Property applicability requires registered metadata.");

        if (!property.AppliesTo(control.GetType()))
        {
            throw new ArgumentException(
                $"The property '{property.Name}' does not apply to {control.GetType().Name}.",
                nameof(property));
        }
    }

    private static void ValidateVisualState(State visualState)
    {
        if ((visualState & ~VisualStates.Overlays) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(visualState),
                visualState,
                "The visual state contains an unknown flag.");
        }
    }

    private static bool TryGetSnapshotValue(
        IControlStyle style,
        IStyleProperty property,
        State state,
        out object? value) =>
        style.TryGetValue(property, state, out value);

    #endregion
}
