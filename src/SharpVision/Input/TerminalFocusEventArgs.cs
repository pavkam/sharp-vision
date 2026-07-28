// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Provides one decoded terminal focus transition for routed input.</summary>
[PublicAPI]
public sealed class TerminalFocusEventArgs: RoutedEventArgs
{
    /// <summary>Initializes routed terminal focus input.</summary>
    /// <param name="focus">The decoded terminal focus transition.</param>
    public TerminalFocusEventArgs(Focus focus) => Focus = focus;

    /// <summary>Gets the decoded terminal focus transition.</summary>
    public Focus Focus { get; }
}
