// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Text;

/// <summary>
/// Describes what a focused numeric field paints: the rendered text plus the edit buffer's
/// selection and caret projected into that text.
/// </summary>
/// <remarks>
/// A numeric field's edit buffer holds only the bare digits the user types, but a derived control
/// may present those digits inside a richer composition while focused - a currency field wraps them
/// in a sign and a symbol, for example. This value carries that composed presentation together with
/// the selection and caret mapped into it, so the shared rendering and cursor-replay paths on
/// <see cref="Controls.Input.NumericInputBase"/> never need to know how the composition was built.
/// The default value describes an empty display with no selection and the caret at column zero.
/// </remarks>
[PublicAPI]
public readonly struct NumericFocusedDisplay
{
    /// <summary>Initializes one focused display projection.</summary>
    /// <param name="text">The non-null text the focused field renders.</param>
    /// <param name="selection">The buffer selection projected into <paramref name="text"/>.</param>
    /// <param name="caret">The buffer caret projected into <paramref name="text"/>, as a
    /// non-negative UTF-16 index no greater than the text length.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="caret"/> is negative or
    /// exceeds the length of <paramref name="text"/>.</exception>
    public NumericFocusedDisplay(string text, Selection selection, int caret)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(caret);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(caret, text.Length);
        Text = text;
        Selection = selection;
        Caret = caret;
    }

    /// <summary>Gets the text the focused field renders; empty for the default value.</summary>
    public string Text
    {
        get => field ?? string.Empty;
        private init;
    }

    /// <summary>Gets the buffer selection projected into <see cref="Text"/>.</summary>
    public Selection Selection { get; }

    /// <summary>Gets the buffer caret projected into <see cref="Text"/>.</summary>
    public int Caret { get; }
}
