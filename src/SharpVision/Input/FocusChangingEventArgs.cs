using SharpVision.Controls;

namespace SharpVision.Input;

/// <summary>Provides cancellable state before one focus transaction commits.</summary>
public sealed class FocusChangingEventArgs(Control? previous, Control? next): EventArgs
{
    /// <summary>Gets the control focused before the request.</summary>
    public Control? Previous { get; } = previous;

    /// <summary>Gets the requested next control, or null for release.</summary>
    public Control? Next { get; } = next;

    /// <summary>Gets or sets whether an explicit request should be cancelled.</summary>
    public bool Cancel { get; set; }
}
