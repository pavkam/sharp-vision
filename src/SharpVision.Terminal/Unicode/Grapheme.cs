// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Unicode;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Identifies one extended grapheme cluster inside a borrowed UTF-16 span.</summary>
[DebuggerDisplay("{ToString()}")]
[PublicAPI]
public readonly record struct Grapheme
{
    /// <summary>Initializes a validated borrowed-span segment.</summary>
    /// <param name="offset">The non-negative UTF-16 code-unit offset.</param>
    /// <param name="length">The positive UTF-16 code-unit length.</param>
    /// <param name="hasInvalidData">Whether invalid input was replaced.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offset"/> is negative or <paramref name="length"/> is not positive.
    /// </exception>
    public Grapheme(
        [NonNegativeValue] int offset,
        [ValueRange(1, int.MaxValue)] int length,
        bool hasInvalidData)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        Offset = offset;
        Length = length;
        HasInvalidData = hasInvalidData;
    }

    /// <summary>Gets the zero-based UTF-16 code-unit offset.</summary>
    [NonNegativeValue]
    public int Offset { get; }

    /// <summary>Gets the positive UTF-16 code-unit length.</summary>
    [ValueRange(1, int.MaxValue)]
    public int Length { get; }

    /// <summary>Gets whether the segment contains invalid data represented as U+FFFD.</summary>
    public bool HasInvalidData { get; }

    /// <inheritdoc/>
    public override string ToString() => $"Grapheme {{ Offset={Offset}, Length={Length}{(HasInvalidData ? ", Invalid" : "")} }}";

    /// <summary>Deconstructs the borrowed-span segment.</summary>
    /// <param name="offset">Receives the UTF-16 offset.</param>
    /// <param name="length">Receives the UTF-16 length.</param>
    /// <param name="hasInvalidData">Receives whether invalid input was replaced.</param>
    public void Deconstruct(out int offset, out int length, out bool hasInvalidData)
    {
        offset = Offset;
        length = Length;
        hasInvalidData = HasInvalidData;
    }
}
