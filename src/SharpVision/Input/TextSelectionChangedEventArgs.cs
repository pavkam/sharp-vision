// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using TextSelection = Text.Selection;

/// <summary>Describes one committed directional semantic-text selection transition.</summary>
[PublicAPI]
public sealed class TextSelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes one committed semantic-text selection transition.</summary>
    /// <param name="previousSelection">The directional selection before the transition.</param>
    /// <param name="selection">The committed directional selection after the transition.</param>
    public TextSelectionChangedEventArgs(TextSelection previousSelection, TextSelection selection)
    {
        PreviousSelection = previousSelection;
        Selection = selection;
    }

    /// <summary>Gets the directional selection before the transition.</summary>
    public TextSelection PreviousSelection { get; }

    /// <summary>Gets the committed directional selection after the transition.</summary>
    public TextSelection Selection { get; }
}
