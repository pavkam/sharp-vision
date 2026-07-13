// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Selects how an unwrapped logical line handles horizontal overflow.</summary>
public enum Trimming
{
    /// <summary>Preserve complete content even when it exceeds the finite width.</summary>
    None,

    /// <summary>Clip overflow at the last complete grapheme boundary.</summary>
    Clip,

    /// <summary>Reserve one cell for an ellipsis after the last fitting grapheme.</summary>
    GraphemeEllipsis,

    /// <summary>Reserve one cell for an ellipsis after the last complete word.</summary>
    WordEllipsis,
}
