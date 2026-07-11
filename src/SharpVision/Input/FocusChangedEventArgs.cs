using SharpVision.Controls;

namespace SharpVision.Input;

/// <summary>Describes one committed focus transition.</summary>
public sealed class FocusChangedEventArgs(Control? previous, Control? current): EventArgs
{
    /// <summary>Gets the control focused before the commit.</summary>
    public Control? Previous { get; } = previous;

    /// <summary>Gets the control focused after the commit.</summary>
    public Control? Current { get; } = current;
}
