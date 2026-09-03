// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Describes one focused currency field's composed text and the editable magnitude's
/// position within it.</summary>
internal readonly record struct CurrencyInputFocusedDisplay
{
    /// <summary>Initializes one immutable focused currency presentation.</summary>
    /// <param name="text">The complete culture-pattern-composed display text.</param>
    /// <param name="coreStart">The UTF-16 offset where the editable magnitude begins.</param>
    /// <param name="magnitude">The editable magnitude without a leading sign.</param>
    /// <param name="signLength">The leading sign token length retained by the edit buffer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or
    /// <paramref name="magnitude"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="coreStart"/> or
    /// <paramref name="signLength"/> is negative.</exception>
    internal CurrencyInputFocusedDisplay(string text, int coreStart, string magnitude, int signLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(magnitude);
        ArgumentOutOfRangeException.ThrowIfNegative(coreStart);
        ArgumentOutOfRangeException.ThrowIfNegative(signLength);

        Text = text;
        CoreStart = coreStart;
        Magnitude = magnitude;
        SignLength = signLength;
    }

    /// <summary>Gets the complete culture-pattern-composed display text.</summary>
    internal string Text { get; }

    /// <summary>Gets the UTF-16 offset where the editable magnitude begins.</summary>
    internal int CoreStart { get; }

    /// <summary>Gets the editable magnitude without a leading sign.</summary>
    internal string Magnitude { get; }

    /// <summary>Gets the leading sign token length retained by the edit buffer.</summary>
    internal int SignLength { get; }
}
