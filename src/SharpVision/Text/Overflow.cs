// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Selects how text handles horizontal overflow within a finite cell width.</summary>
public enum Overflow
{
    /// <summary>Break onto new lines at whitespace, falling back to grapheme boundaries.</summary>
    Wrap,

    /// <summary>Break onto new lines between complete grapheme clusters.</summary>
    WrapAnywhere,

    /// <summary>Keep one line and clip at the last complete grapheme that fits.</summary>
    Clip,

    /// <summary>Keep one line with a trailing ellipsis, preferring a word boundary.</summary>
    Ellipsis,

    /// <summary>Keep one complete line and report its full cell width.</summary>
    Visible,
}
