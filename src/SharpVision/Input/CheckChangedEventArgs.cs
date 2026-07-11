namespace SharpVision.Input;

/// <summary>Reports one committed CheckBox state transition.</summary>
public sealed class CheckChangedEventArgs: EventArgs
{
    /// <summary>Initializes immutable transition values.</summary>
    /// <param name="previous">The previous nullable state.</param>
    /// <param name="current">The committed nullable state.</param>
    /// <param name="cause">The defined transition cause.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    public CheckChangedEventArgs(bool? previous, bool? current, ActivationCause cause)
    {
        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause), cause, "The activation cause is unknown.");
        }

        Previous = previous;
        Current = current;
        Cause = cause;
    }

    /// <summary>Gets the previous nullable state.</summary>
    public bool? Previous { get; }

    /// <summary>Gets the committed nullable state.</summary>
    public bool? Current { get; }

    /// <summary>Gets the transition input path.</summary>
    public ActivationCause Cause { get; }
}
