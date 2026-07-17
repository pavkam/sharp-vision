// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies one built-in one-cell Spinner frame sequence.</summary>
public enum SpinnerPattern
{
    /// <summary>Uses a light ten-frame Braille orbit.</summary>
    Braille,

    /// <summary>Uses an eight-frame dense Braille rotation.</summary>
    DenseBraille,

    /// <summary>Uses the portable ASCII sequence vertical, slash, horizontal, and backslash.</summary>
    Ascii,
}
