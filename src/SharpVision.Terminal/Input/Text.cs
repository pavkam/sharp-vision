// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

/// <summary>Represents one decoded Unicode scalar intended for text input.</summary>
[PublicAPI]
public readonly record struct Text
{
    /// <summary>Initializes decoded text from one valid Unicode scalar.</summary>
    /// <param name="value">The valid Unicode scalar value.</param>
    public Text(Rune value) => Value = value;

    /// <summary>Gets the decoded Unicode scalar.</summary>
    public Rune Value { get; }

    /// <summary>Deconstructs the decoded text.</summary>
    /// <param name="value">Receives the Unicode scalar.</param>
    public void Deconstruct(out Rune value) => value = Value;
}
