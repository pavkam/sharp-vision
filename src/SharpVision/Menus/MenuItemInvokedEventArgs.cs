// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

/// <summary>Reports a menu item and input cause after a completed activation.</summary>
[PublicAPI]
public sealed class MenuItemInvokedEventArgs: EventArgs
{
    /// <summary>Initializes event data for one non-null item and activation cause.</summary>
    /// <param name="item">The invoked menu item.</param>
    /// <param name="cause">The completed activation path.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is undefined.</exception>
    public MenuItemInvokedEventArgs(MenuItem item, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(item);

        ArgumentOutOfRangeException.ThrowIfNotDefined(cause, nameof(cause), "The activation cause is unknown.");

        Item = item;
        Cause = cause;
    }

    /// <summary>Gets the MenuItem that was activated by pointer click, keyboard shortcut, or access key.</summary>
    public MenuItem Item { get; }

    /// <summary>Gets the input path that completed activation.</summary>
    public ActivationCause Cause { get; }
}
