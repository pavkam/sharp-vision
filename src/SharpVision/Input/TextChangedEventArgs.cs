// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Reports one committed TextInput text transition.</summary>
public sealed class TextChangedEventArgs: EventArgs
{
    /// <summary>Initializes immutable previous and committed text.</summary>
    /// <param name="previousText">The non-null previous text.</param>
    /// <param name="text">The non-null committed text.</param>
    /// <exception cref="ArgumentNullException">A text value is null.</exception>
    public TextChangedEventArgs(string previousText, string text)
    {
        ArgumentNullException.ThrowIfNull(previousText);
        ArgumentNullException.ThrowIfNull(text);
        PreviousText = previousText;
        Text = text;
    }

    /// <summary>Gets the text before the transition.</summary>
    public string PreviousText { get; }

    /// <summary>Gets the committed text.</summary>
    public string Text { get; }
}
