// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

using SharpVision.Input;

/// <summary>Provides a collectible routed-handler delegate target.</summary>
internal sealed class Listener
{
    /// <summary>Consumes one key event to create a real delegate target.</summary>
    /// <param name="sender">The current route control.</param>
    /// <param name="eventArgs">The key payload.</param>
    internal void Handle(object? sender, KeyEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(eventArgs);
    }
}
