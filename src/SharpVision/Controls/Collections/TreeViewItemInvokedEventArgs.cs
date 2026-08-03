// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Reports a tree view item and input cause after a completed activation.</summary>
[PublicAPI]
public sealed class TreeViewItemInvokedEventArgs: EventArgs
{
    /// <summary>Initializes event data for one non-null tree view item and activation cause.</summary>
    /// <param name="item">The invoked tree view item.</param>
    /// <param name="cause">The completed activation path.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is undefined.</exception>
    public TreeViewItemInvokedEventArgs(TreeViewItem item, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause), cause, "The activation cause is unknown.");
        }

        Item = item;
        Cause = cause;
    }

    /// <summary>Gets the invoked tree view item.</summary>
    public TreeViewItem Item { get; }

    /// <summary>Gets the input path that completed activation.</summary>
    public ActivationCause Cause { get; }
}
