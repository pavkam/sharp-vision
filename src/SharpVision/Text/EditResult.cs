// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Owns one immutable text-and-selection edit snapshot.</summary>
[PublicAPI]
public readonly record struct EditResult
{
    /// <summary>Initializes a validated caller-owned editing snapshot.</summary>
    /// <param name="text">The non-null UTF-16 source.</param>
    /// <param name="selection">The grapheme-aligned directional selection.</param>
    /// <param name="changed">Whether the operation changed text or selection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the text.</exception>
    public EditResult(string text, Selection selection, bool changed)
    {
        ArgumentNullException.ThrowIfNull(text);
        Edit.Validate(text, selection);
        Text = text;
        Selection = selection;
        IsChanged = changed;
    }

    /// <summary>Gets the owned immutable UTF-16 text.</summary>
    public string Text { get; }

    /// <summary>Gets the directional grapheme-aligned selection.</summary>
    public Selection Selection { get; }

    /// <summary>Gets whether the producing operation changed text or selection.</summary>
    public bool IsChanged { get; }
}
