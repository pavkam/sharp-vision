namespace SharpVision.Styling;

/// <summary>Describes one committed state appearance change.</summary>
public sealed class ChangedEventArgs: EventArgs
{
    /// <summary>Initializes one validated appearance change.</summary>
    /// <param name="state">The valid state combination that changed.</param>
    /// <param name="impact">The earliest affected control phase.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="state"/> contains unknown flags or <paramref name="impact"/> is unknown.
    /// </exception>
    public ChangedEventArgs(State state, Impact impact)
    {
        const State known = State.Hovered | State.Focused | State.Checked | State.Pressed | State.Disabled;

        if ((state & ~known) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The visual state contains unknown flags.");
        }

        if (!Enum.IsDefined(impact))
        {
            throw new ArgumentOutOfRangeException(nameof(impact), impact, "The style impact is unknown.");
        }

        State = state;
        Impact = impact;
    }

    /// <summary>Gets the single state definition that changed.</summary>
    public State State { get; }

    /// <summary>Gets the earliest affected control phase.</summary>
    public Impact Impact { get; }
}
