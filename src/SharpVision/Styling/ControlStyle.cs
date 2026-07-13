// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using SharpVision.Controls;

/// <summary>Stores typed style values for one specific control type.</summary>
/// <typeparam name="TControl">The targeted control type.</typeparam>
public sealed class ControlStyle<TControl>: IControlStyle, IStyleLifecycle
    where TControl : Control
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(IStyleProperty Property, State State), object> _values = [];

    /// <summary>Initializes an empty style for <typeparamref name="TControl"/>.</summary>
    public ControlStyle() => CurrentSnapshot = ControlStyleSnapshot.Empty;

    private ControlStyle(ControlStyleSnapshot snapshot, bool isFrozen)
    {
        CurrentSnapshot = snapshot;
        IsFrozen = isFrozen;
    }

    /// <inheritdoc/>
    public event EventHandler<ThemeChangedEventArgs>? Changed;

    /// <inheritdoc/>
    public Type TargetType => typeof(TControl);

    /// <inheritdoc/>
    public bool IsFrozen { get; }

    /// <inheritdoc/>
    public Impact AggregateImpact => CurrentSnapshot.AggregateImpact;

    /// <summary>Adds or replaces one property value for a visual state.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The registered style property.</param>
    /// <param name="state">Normal or any combination of overlay flags; more specific combinations win.</param>
    /// <param name="value">The validated value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentException">The property is outside the target hierarchy.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The state contains unknown flags.</exception>
    /// <exception cref="InvalidOperationException">The style is frozen.</exception>
    public void Set<T>(StyleProperty<T> property, State state, T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        EnsureMutable();
        ValidateState(state);
        EnsureProperty(property);
        property.ValidateValue(value);

        (StyleProperty<T> property, State state) key = (property, state);

        lock (_gate)
        {
            if (_values.TryGetValue(key, out var existing) && EqualityComparer<T>.Default.Equals((T) existing, value))
            {
                return;
            }

            _values[key] = value!;
            CurrentSnapshot = BuildSnapshot();
        }

        RaiseChanged(property.Impact);
    }

    /// <summary>Removes one property value for a visual state.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The registered style property.</param>
    /// <param name="state">Normal or any combination of overlay flags.</param>
    /// <returns>Whether a value was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentException">The property is outside the target hierarchy.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The state contains unknown flags.</exception>
    /// <exception cref="InvalidOperationException">The style is frozen.</exception>
    public bool Remove<T>(StyleProperty<T> property, State state)
    {
        ArgumentNullException.ThrowIfNull(property);
        EnsureMutable();
        ValidateState(state);
        EnsureProperty(property);

        lock (_gate)
        {
            if (!_values.Remove((property, state), out _))
            {
                return false;
            }

            CurrentSnapshot = BuildSnapshot();
        }

        RaiseChanged(property.Impact);
        return true;
    }

    /// <summary>Gets one stored property value for a visual state.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The registered style property.</param>
    /// <param name="state">Normal or any combination of overlay flags.</param>
    /// <param name="value">The stored value when present.</param>
    /// <returns>Whether the value exists in the current snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The state contains unknown flags.</exception>
    public bool TryGet<T>(StyleProperty<T> property, State state, out T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        ValidateState(state);

        if (CurrentSnapshot.TryGet(property, state, out var stored))
        {
            value = (T) stored!;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Creates an independent unfrozen copy of this style.</summary>
    /// <returns>A mutable clone containing the same values.</returns>
    public ControlStyle<TControl> Clone()
    {
        lock (_gate)
        {
            ControlStyle<TControl> clone = new ControlStyle<TControl>();

            foreach (KeyValuePair<(IStyleProperty Property, State State), object> entry in _values)
            {
                clone._values[entry.Key] = entry.Value;
            }

            clone.CurrentSnapshot = clone.BuildSnapshot();
            return clone;
        }
    }

    internal ControlStyleSnapshot CurrentSnapshot { get; private set; }

    internal ControlStyle<TControl> FreezeCopy()
    {
        lock (_gate)
        {
            return new ControlStyle<TControl>(BuildSnapshot(), isFrozen: true);
        }
    }

    /// <inheritdoc/>
    public bool TryGetValue(IStyleProperty styleProperty, State state, out object? value) =>
        CurrentSnapshot.TryGet(styleProperty, state, out value);

    /// <inheritdoc/>
    IControlStyle IStyleLifecycle.CloneForTheme() => Clone();

    /// <inheritdoc/>
    IControlStyle IStyleLifecycle.FreezeForTheme() => FreezeCopy();

    private void EnsureMutable()
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("A frozen control style cannot be changed.");
        }
    }

    private static void EnsureProperty<T>(StyleProperty<T> property)
    {
        if (!property.DeclaringType.IsAssignableFrom(typeof(TControl)))
        {
            throw new ArgumentException(
                $"The property '{property.Name}' cannot be set on a style for {typeof(TControl).Name}.",
                nameof(property));
        }
    }

    private static void ValidateState(State state)
    {
        if ((state & ~VisualStates.Overlays) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The state contains unknown flags.");
        }
    }

    private void RaiseChanged(Impact impact) =>
        Changed?.Invoke(this, new ThemeChangedEventArgs(typeof(TControl), impact));

    private ControlStyleSnapshot BuildSnapshot()
    {
        Dictionary<(IStyleProperty Property, State State), object> copy = new Dictionary<(IStyleProperty Property, State State), object>(_values);
        Impact aggregate = Impact.Render;

        foreach ((IStyleProperty Property, State State) entry in copy.Keys)
        {
            if (entry.Property.Impact == Impact.Measure)
            {
                aggregate = Impact.Measure;
                break;
            }
        }

        return new ControlStyleSnapshot(copy, aggregate);
    }
}
