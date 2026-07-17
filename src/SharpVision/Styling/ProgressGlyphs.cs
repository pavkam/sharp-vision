// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines immutable progress-track, fill, and ordered fractional glyphs.</summary>
public sealed class ProgressGlyphs
{
    private const int _levelCount = 9;
    private readonly ThemedGlyph[] _horizontalFractions;
    private readonly ThemedGlyph[] _verticalFractions;

    /// <summary>Initializes a complete progress glyph family.</summary>
    /// <param name="empty">The empty-track glyph.</param>
    /// <param name="full">The fully filled glyph.</param>
    /// <param name="indeterminate">The indeterminate-state glyph.</param>
    /// <param name="horizontalFractions">Nine horizontal levels from empty through full.</param>
    /// <param name="verticalFractions">Nine vertical levels from empty through full.</param>
    /// <exception cref="ArgumentException">
    /// A fractional sequence does not contain nine entries or its endpoints do not match
    /// <paramref name="empty"/> and <paramref name="full"/>.
    /// </exception>
    public ProgressGlyphs(
        ThemedGlyph empty,
        ThemedGlyph full,
        ThemedGlyph indeterminate,
        ReadOnlySpan<ThemedGlyph> horizontalFractions,
        ReadOnlySpan<ThemedGlyph> verticalFractions)
    {
        Validate(horizontalFractions, empty, full, nameof(horizontalFractions));
        Validate(verticalFractions, empty, full, nameof(verticalFractions));
        Empty = empty;
        Full = full;
        Indeterminate = indeterminate;
        _horizontalFractions = horizontalFractions.ToArray();
        _verticalFractions = verticalFractions.ToArray();
    }

    /// <summary>Gets the empty-track glyph.</summary>
    public ThemedGlyph Empty { get; }
    /// <summary>Gets the fully filled glyph.</summary>
    public ThemedGlyph Full { get; }
    /// <summary>Gets the indeterminate-state glyph.</summary>
    public ThemedGlyph Indeterminate { get; }
    /// <summary>Gets nine horizontal levels from empty through full.</summary>
    public ReadOnlyMemory<ThemedGlyph> HorizontalFractions => _horizontalFractions;
    /// <summary>Gets nine vertical levels from empty through full.</summary>
    public ReadOnlyMemory<ThemedGlyph> VerticalFractions => _verticalFractions;

    private static void Validate(
        ReadOnlySpan<ThemedGlyph> values,
        ThemedGlyph empty,
        ThemedGlyph full,
        string name)
    {
        if (values.Length != _levelCount || values[0] != empty || values[^1] != full)
        {
            throw new ArgumentException(
                "Progress fractions must contain nine levels whose endpoints match empty and full.",
                name);
        }
    }
}
