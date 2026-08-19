// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Fonts;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Bounds FIG-font parsing and rendered output resource use.</summary>
[PublicAPI]
public readonly record struct FigletLimits
{
    /// <summary>Initializes finite positive FIG-let limits.</summary>
    /// <param name="maxInputBytes">The largest accepted encoded font.</param>
    /// <param name="maxGlyphs">The largest accepted glyph count.</param>
    /// <param name="maxHeight">The largest accepted glyph height.</param>
    /// <param name="maxRowWidth">The largest accepted encoded glyph row.</param>
    /// <param name="maxComments">The largest accepted comment-line count.</param>
    /// <param name="maxOutputChars">The largest rendered UTF-16 result.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is not positive.</exception>
    public FigletLimits(
        int maxInputBytes = 16 * 1024 * 1024,
        int maxGlyphs = 4096,
        int maxHeight = 256,
        int maxRowWidth = 16 * 1024,
        int maxComments = 4096,
        int maxOutputChars = 16 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxGlyphs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRowWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxComments);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputChars);
        MaxInputBytes = maxInputBytes;
        MaxGlyphs = maxGlyphs;
        MaxHeight = maxHeight;
        MaxRowWidth = maxRowWidth;
        MaxComments = maxComments;
        MaxOutputChars = maxOutputChars;
    }

    /// <summary>Gets the default interactive limits.</summary>
    public static FigletLimits Default { get; } = new(
        16 * 1024 * 1024);

    /// <summary>Gets the largest accepted encoded font.</summary>
    [ValueRange(1, int.MaxValue)]
    public int MaxInputBytes { get; }

    /// <summary>Gets the largest accepted glyph count.</summary>
    [ValueRange(1, int.MaxValue)]
    public int MaxGlyphs { get; }

    /// <summary>Gets the largest accepted glyph height.</summary>
    [ValueRange(1, int.MaxValue)]
    public int MaxHeight { get; }

    /// <summary>Gets the largest accepted encoded glyph row.</summary>
    [ValueRange(1, int.MaxValue)]
    public int MaxRowWidth { get; }

    /// <summary>Gets the largest accepted comment-line count.</summary>
    [ValueRange(1, int.MaxValue)]
    public int MaxComments { get; }

    /// <summary>Gets the largest rendered UTF-16 result.</summary>
    [ValueRange(1, int.MaxValue)]
    public int MaxOutputChars { get; }
}
