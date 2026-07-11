namespace SharpVision.Input;

/// <summary>Reports the immutable cause of one completed semantic activation.</summary>
/// <param name="cause">The defined activation cause.</param>
public sealed class ActivationEventArgs(ActivationCause cause): EventArgs
{
    /// <summary>Gets the activation input path.</summary>
    public ActivationCause Cause { get; } = Validate(cause);

    private static ActivationCause Validate(ActivationCause value) => Enum.IsDefined(value)
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), value, "The activation cause is unknown.");
}
