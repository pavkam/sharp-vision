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

    /// <summary>Gets whether this event begins a key hold rather than repeating or ending one.</summary>
    public bool IsInitialKeyDown => Stroke.Action == KeyAction.Press;

    /// <summary>Gets whether this event is an initial or repeated key-down command.</summary>
    public bool IsKeyDown => Stroke.Action is KeyAction.Press or KeyAction.Repeat;

    /// <summary>Gets whether this event repeats a key that is already down.</summary>
    public bool IsRepeat => Stroke.Action == KeyAction.Repeat;

    /// <summary>Gets whether this event ends a key hold.</summary>
    public bool IsKeyUp => Stroke.Action == KeyAction.Release;
}
