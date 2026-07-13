namespace SharpVision.Controls;

using SharpVision.Input;

/// <summary>Reports a menu item and input cause after a completed activation.</summary>
public sealed class MenuItemInvokedEventArgs: EventArgs
{
    /// <summary>Initializes event data for one non-null item and activation cause.</summary>
    /// <param name="item">The invoked menu item.</param>
    /// <param name="cause">The completed activation path.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public MenuItemInvokedEventArgs(MenuItem item, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(item);
        Item = item;
        Cause = cause;
    }

    /// <summary>Gets the invoked item.</summary>
    public MenuItem Item { get; }

    /// <summary>Gets the input path that completed activation.</summary>
    public ActivationCause Cause { get; }
}
