// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Describes one command item accepted by its owning command bar.</summary>
[PublicAPI]
public sealed class CommandBarItemInvokedEventArgs: EventArgs
{
    /// <summary>Initializes a validated command-bar activation payload.</summary>
    /// <param name="item">The non-null semantic item that accepted activation.</param>
    /// <param name="cause">The defined input path that completed activation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is undefined.</exception>
    public CommandBarItemInvokedEventArgs(CommandBarItem item, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause);
        Item = item;
        Cause = cause;
    }

    /// <summary>Gets the semantic command item that accepted activation.</summary>
    public CommandBarItem Item { get; }

    /// <summary>Gets the keyboard, pointer, or programmatic activation path.</summary>
    public ActivationCause Cause { get; }
}
