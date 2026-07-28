// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Reports semantic activation of one realized ListView item.</summary>
[PublicAPI]
public sealed class ItemInvokedEventArgs: EventArgs
{
    /// <summary>Initializes a validated item index, borrowed value, and cause.</summary>
    /// <param name="index">The non-negative item index.</param>
    /// <param name="item">The borrowed item value, which may be null.</param>
    /// <param name="cause">The defined activation path.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative or cause is unknown.</exception>
    public ItemInvokedEventArgs(int index, object? item, ActivationCause cause)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause), cause, "The activation cause is unknown.");
        }

        Index = index;
        Item = item;
        Cause = cause;
    }

    /// <summary>Gets the invoked item index.</summary>
    public int Index { get; }

    /// <summary>Gets the borrowed invoked item value.</summary>
    public object? Item { get; }

    /// <summary>Gets the semantic activation path.</summary>
    public ActivationCause Cause { get; }
}
