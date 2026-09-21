// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

/// <summary>
/// Packs and unpacks the printf-style flag and precision bits one compiled
/// <see cref="TerminfoOperation.FormatNumber"/> or <see cref="TerminfoOperation.FormatCharacter"/>
/// instruction carries in its tertiary operand.
/// </summary>
/// <remarks>
/// The compiler, the interpreter, and the compile-time non-empty-expansion proof must all agree on
/// this exact bit layout: the compiler packs flags and precision once, the interpreter unpacks them
/// on every execution, and the proof unpacks them to decide whether a numeric conversion can ever
/// produce zero output bytes. A single shared layout keeps those three readers from drifting apart.
/// </remarks>
internal static class TerminfoFormatLayout
{
    /// <summary>Identifies the printf <c>#</c> alternate-form flag.</summary>
    public const int Alternate = 1 << 0;

    /// <summary>Identifies the printf <c>0</c> zero-pad flag.</summary>
    public const int ZeroPad = 1 << 1;

    /// <summary>Identifies the printf <c>-</c> left-alignment flag.</summary>
    public const int LeftAligned = 1 << 2;

    /// <summary>Identifies the printf <c>+</c> explicit-sign flag.</summary>
    public const int ShowSign = 1 << 3;

    /// <summary>Identifies the printf leading-space flag.</summary>
    public const int LeadingSpace = 1 << 4;

    /// <summary>Packs printf flags and an optional precision into one tertiary operand.</summary>
    /// <param name="flags">The bitwise-combined printf flags.</param>
    /// <param name="precision">The parsed precision, or -1 when the directive specified none.</param>
    /// <returns>The packed tertiary operand for a formatted numeric or character instruction.</returns>
    public static int Pack(int flags, int precision) => flags | ((precision + 1) << 8);

    /// <summary>Unpacks the precision from one packed tertiary operand.</summary>
    /// <param name="packed">A tertiary operand produced by <see cref="Pack"/>.</param>
    /// <returns>The parsed precision, or -1 when the directive specified none.</returns>
    public static int UnpackPrecision(int packed) => (packed >> 8) - 1;

    /// <summary>Unpacks the printf flags from one packed tertiary operand.</summary>
    /// <param name="packed">A tertiary operand produced by <see cref="Pack"/>.</param>
    /// <returns>The bitwise-combined printf flags.</returns>
    public static int UnpackFlags(int packed) => packed & 0xff;
}
