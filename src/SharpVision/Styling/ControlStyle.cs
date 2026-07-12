using SharpVision.Controls;

namespace SharpVision.Styling;

/// <summary>Stores typed style values for one specific control type.</summary>
/// <typeparam name="TControl">The targeted control type.</typeparam>
public sealed class ControlStyle<TControl> : IControlStyle
    where TControl : Control
{
    private const State _overlayStates =
        State.Hovered | State.Focused | State.Checked | State.Pressed | State.Disabled;

    private readonly Lock _gate = new();
    private readonly Dictionary<(IStyleProperty Property, State State), object> _values = [];
    private Snapshot _snapshot;
    private bool _isFrozen;

    /// <summary>Initializes an empty style for <typeparamref name="TControl"/>.</summary>
    public ControlStyle()
    {
        _snapshot = Snapshot.Empty;
    }

    private ControlStyle(Snapshot snapshot, bool isFrozen)
    {
        _snapshot = snapshot;
        _isFrozen = isFrozen;
    }

    /// <inheritdoc/>
    public event EventHandler<ThemeChangedEventArgs>? Changed;

    /// <inheritdoc/>
    public Type TargetType => typeof(TControl);

    /// <inheritdoc/>
    public bool IsFrozen => _isFrozen;

    /// <inheritdoc/>
    public Impact AggregateImpact => _snapshot.AggregateImpact;

    /// <summary>Adds or replaces one property value for a visual state.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The registered style property.</param>
    /// <param name="state">Normal or exactly one overlay flag.</param>
    /// <param name="value">The validated value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The property is outside the target hierarchy or a measure-impact property is used in an
    /// overlay state.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The state contains unknown or combined flags.</exception>
    /// <exception cref="InvalidOperationException">The style is frozen.</exception>
    public void Set<T>(StyleProperty<T> property, State state, T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        EnsureMutable();
        ValidateState(state);
        EnsureProperty(property);
        EnsureStateImpact(property, state);
        property.ValidateValue(value);

        var key = (property, state);
        var impact = property.Impact;

        lock (_gate)
        {
            if (_values.TryGetValue(key, out var existing) && EqualityComparer<T>.Default.Equals((T) existing, value))
            {
                return;
            }

            _values[key] = value!;
            Publish(impact);
        }
    }

    /// <summary>Removes one property value for a visual state.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The registered style property.</param>
    /// <param name="state">Normal or exactly one overlay flag.</param>
    /// <returns>Whether a value was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentException">The property is outside the target hierarchy.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The state contains unknown or combined flags.</exception>
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

            Publish(property.Impact);
            return true;
        }
    }

    /// <summary>Gets one stored property value for a visual state.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The registered style property.</param>
    /// <param name="state">Normal or exactly one overlay flag.</param>
    /// <param name="value">The stored value when present.</param>
    /// <returns>Whether the value exists in the current snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The state contains unknown or combined flags.</exception>
    public bool TryGet<T>(StyleProperty<T> property, State state, out T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        ValidateState(state);

        if (_snapshot.TryGet(property, state, out var stored))
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
            var clone = new ControlStyle<TControl>();

            foreach (var entry in _values)
            {
                clone._values[entry.Key] = entry.Value;
            }

            clone._snapshot = clone.BuildSnapshot();
            return clone;
        }
    }

    internal Snapshot CurrentSnapshot => _snapshot;

    internal ControlStyle<TControl> FreezeCopy()
    {
        lock (_gate)
        {
            return new ControlStyle<TControl>(BuildSnapshot(), isFrozen: true);
        }
    }

    /// <inheritdoc/>
    IControlStyle IControlStyle.CloneForTheme() => Clone();

    /// <inheritdoc/>
    IControlStyle IControlStyle.FreezeForTheme() => FreezeCopy();

    /// <inheritdoc/>
    bool IControlStyle.TryGetSnapshotValue(IStyleProperty property, State state, out object? value) =>
        TryGetSnapshotValue(property, state, out value);

    internal bool TryGetSnapshotValue(IStyleProperty property, State state, out object? value) =>
        _snapshot.TryGet(property, state, out value);

    private void EnsureMutable()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("A frozen control style cannot be changed.");
        }
    }

    private void EnsureProperty<T>(StyleProperty<T> property)
    {
        if (!property.DeclaringType.IsAssignableFrom(typeof(TControl)))
        {
            throw new ArgumentException(
                $"The property '{property.Name}' cannot be set on a style for {typeof(TControl).Name}.",
                nameof(property));
        }
    }

    private static void EnsureStateImpact<T>(StyleProperty<T> property, State state)
    {
        if (state != State.Normal && property.Impact == Impact.Measure)
        {
            throw new ArgumentException(
                $"The measure-impact property '{property.Name}' cannot be set for overlay state {state}.",
                nameof(property));
        }
    }

    private static void ValidateState(State state)
    {
        if ((state & ~_overlayStates) != 0 && state != State.Normal)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The state contains unknown flags.");
        }

        var value = (int) state;

        if (state != State.Normal && value != 0 && (value & (value - 1)) != 0)
        {
            throw new ArgumentException(
                "A style definition must target Normal or exactly one overlay state.",
                nameof(state));
        }
    }

    private void Publish(Impact impact)
    {
        _snapshot = BuildSnapshot();
        Changed?.Invoke(this, new ThemeChangedEventArgs(typeof(TControl), impact));
    }

    private Snapshot BuildSnapshot()
    {
        var copy = new Dictionary<(IStyleProperty Property, State State), object>(_values);
        var aggregate = Impact.Render;

        foreach (var entry in copy.Keys)
        {
            if (entry.Property.Impact == Impact.Measure)
            {
                aggregate = Impact.Measure;
                break;
            }
        }

        return new Snapshot(copy, aggregate);
    }

    /// <summary>Immutable style contents read by theme resolution.</summary>
    internal sealed class Snapshot
    {
        internal static Snapshot Empty { get; } = new([], Impact.Render);

        private readonly Dictionary<(IStyleProperty Property, State State), object> _values;

        internal Snapshot(
            Dictionary<(IStyleProperty Property, State State), object> values,
            Impact aggregateImpact)
        {
            _values = values;
            AggregateImpact = aggregateImpact;
        }

        internal Impact AggregateImpact { get; }

        internal bool TryGet(IStyleProperty property, State state, out object? value) =>
            _values.TryGetValue((property, state), out value);

        internal bool TryGet<T>(StyleProperty<T> property, State state, out object? value) =>
            _values.TryGetValue((property, state), out value);
    }
}
