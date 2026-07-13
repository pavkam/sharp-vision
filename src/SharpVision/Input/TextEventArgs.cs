// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using TerminalText = Terminal.Input.Text;

/// <summary>Provides one immutable Unicode text scalar.</summary>
public sealed class TextEventArgs: RoutedEventArgs
{
    /// <summary>Initializes routed Unicode text input.</summary>
    /// <param name="text">The decoded text scalar.</param>
    public TextEventArgs(TerminalText text) => Text = text;

    /// <summary>Gets the decoded text scalar.</summary>
    public TerminalText Text { get; }
}
