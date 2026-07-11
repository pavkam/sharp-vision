namespace SharpVision.Input;

/// <summary>Reports the immutable cause of one completed semantic activation.</summary>
public sealed class ActivationEventArgs: EventArgs
{
    /// <summary>Initializes a validated semantic activation.</summary>
    /// <param name="cause">The defined activation cause.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    public ActivationEventArgs(ActivationCause cause) => Cause = Validate(cause);

    /// <summary>Gets the activation input path.</summary>
    public ActivationCause Cause { get; }

    private static ActivationCause Validate(ActivationCause value) => Enum.IsDefined(value)
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), value, "The activation cause is unknown.");
}
