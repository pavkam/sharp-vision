namespace SharpVision.Styling;

/// <summary>Identifies whether one resource change affects render or measurement.</summary>
public enum Impact
{
    /// <summary>Only semantic cell appearance changed.</summary>
    Render,

    /// <summary>Content or box geometry may have changed.</summary>
    Measure,
}

/// <summary>Describes one committed state appearance change.</summary>
public sealed class ChangedEventArgs(State state, Impact impact): EventArgs
{
    /// <summary>Gets the single state definition that changed.</summary>
    public State State { get; } = state;

    /// <summary>Gets the earliest affected control phase.</summary>
    public Impact Impact { get; } = impact;
}

/// <summary>Stores mutable appearance overlays for individual visual states.</summary>
public sealed class Style
{
    private const State _allStates =
        State.Hovered | State.Focused | State.Checked | State.Pressed | State.Disabled;
    private readonly Dictionary<State, Appearance> _values = [];

    /// <summary>Raised after one state definition commits a changed value.</summary>
    public event EventHandler<ChangedEventArgs>? Changed;

    /// <summary>Adds or replaces one base or single-state appearance.</summary>
    /// <param name="state">Normal or exactly one defined overlay flag.</param>
    /// <param name="appearance">The complete optional overlay definition.</param>
    /// <exception cref="ArgumentException">Multiple state flags are combined.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The state contains an unknown flag.</exception>
    public void Set(State state, Appearance appearance)
    {
        Validate(state);

        if (_values.TryGetValue(state, out var previous) && previous == appearance)
        {
            return;
        }

        _values[state] = appearance;
        Changed?.Invoke(this, new ChangedEventArgs(state, GetImpact(previous, appearance)));
    }

    /// <summary>Removes one base or single-state appearance.</summary>
    /// <param name="state">Normal or exactly one defined overlay flag.</param>
    /// <returns>True when a definition was removed.</returns>
    /// <exception cref="ArgumentException">Multiple state flags are combined.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The state contains an unknown flag.</exception>
    public bool Remove(State state)
    {
        Validate(state);

        if (!_values.Remove(state, out var previous))
        {
            return false;
        }

        Changed?.Invoke(this, new ChangedEventArgs(state, GetImpact(previous, default)));
        return true;
    }

    /// <summary>Gets one exact base or single-state appearance when defined.</summary>
    /// <param name="state">Normal or exactly one defined overlay flag.</param>
    /// <param name="appearance">The stored appearance when present.</param>
    /// <returns>Whether the definition exists.</returns>
    /// <exception cref="ArgumentException">Multiple state flags are combined.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The state contains an unknown flag.</exception>
    public bool TryGet(State state, out Appearance appearance)
    {
        Validate(state);
        return _values.TryGetValue(state, out appearance);
    }

    private static Impact GetImpact(Appearance previous, Appearance current) =>
        previous.Padding != current.Padding ? Impact.Measure : Impact.Render;

    private static void Validate(State state)
    {
        if ((state & ~_allStates) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The state contains unknown flags.");
        }

        var value = (int) state;

        if (value != 0 && (value & (value - 1)) != 0)
        {
            throw new ArgumentException(
                "A style definition must target Normal or exactly one overlay state.",
                nameof(state));
        }
    }
}
