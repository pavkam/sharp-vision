using SharpVision.Controls;

namespace SharpVision.Input;

/// <summary>Describes one implicit capture or press cancellation.</summary>
public sealed class CaptureCancelledEventArgs(Control control, ReleaseReason reason): EventArgs
{
    /// <summary>Gets the captured or pressed control.</summary>
    public Control Control { get; } = control;

    /// <summary>Gets why interaction was cancelled.</summary>
    public ReleaseReason Reason { get; } = reason;
}
