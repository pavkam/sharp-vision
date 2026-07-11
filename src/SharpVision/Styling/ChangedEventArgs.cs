namespace SharpVision.Styling;

/// <summary>Describes one committed state appearance change.</summary>
public sealed class ChangedEventArgs(State state, Impact impact): EventArgs
{
    /// <summary>Gets the single state definition that changed.</summary>
    public State State { get; } = state;

    /// <summary>Gets the earliest affected control phase.</summary>
    public Impact Impact { get; } = impact;
}
