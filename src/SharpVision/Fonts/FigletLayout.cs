// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Fonts;

/// <summary>Defines FIGfont version 2 horizontal and vertical layout bits.</summary>
[Flags]
[PublicAPI]
public enum FigletLayout
{
    /// <summary>Uses full-width glyphs without fitting or smushing.</summary>
    None = 0,

    /// <summary>Smushes equal characters.</summary>
    Equal = 1,

    /// <summary>Smushes underscores with hierarchy characters.</summary>
    Underscore = 2,

    /// <summary>Smushes characters according to FIGlet hierarchy.</summary>
    Hierarchy = 4,

    /// <summary>Smushes opposite bracket pairs.</summary>
    OppositePair = 8,

    /// <summary>Smushes slash pairs into a larger shape.</summary>
    BigX = 16,

    /// <summary>Smushes two hardblanks.</summary>
    HardBlank = 32,

    /// <summary>Fits glyphs horizontally without non-space collisions.</summary>
    HorizontalFitting = 64,

    /// <summary>Enables horizontal smushing.</summary>
    HorizontalSmushing = 128,

    /// <summary>Smushes equal vertical characters.</summary>
    VerticalEqual = 256,

    /// <summary>Smushes vertical underscore and hierarchy characters.</summary>
    VerticalUnderscore = 512,

    /// <summary>Smushes vertical hierarchy characters.</summary>
    VerticalHierarchy = 1024,

    /// <summary>Smushes horizontal line pairs vertically.</summary>
    VerticalHorizontalLine = 2048,

    /// <summary>Smushes vertical line supersets.</summary>
    VerticalSuperSmush = 4096,

    /// <summary>Fits rendered lines vertically.</summary>
    VerticalFitting = 8192,

    /// <summary>Enables vertical smushing.</summary>
    VerticalSmushing = 16384
}
