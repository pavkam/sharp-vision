// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Provides an immutable decoded keyboard transition.</summary>
[PublicAPI]
public sealed class KeyEventArgs: RoutedEventArgs
{
    /// <summary>Initializes routed keyboard input.</summary>
    /// <param name="stroke">The decoded keyboard transition.</param>
    public KeyEventArgs(Stroke stroke) => Stroke = stroke;

    /// <summary>Gets the decoded keyboard transition.</summary>
    public Stroke Stroke { get; }
}
