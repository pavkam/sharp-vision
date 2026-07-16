// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines the content-relative terminal-cell axis used by a <see cref="Prism"/> color cycle.</summary>
public enum PrismDirection
{
    /// <summary>Advances color from left to right using each stored owner's horizontal offset.</summary>
    Horizontal,

    /// <summary>Advances color from top to bottom using each stored owner's vertical offset.</summary>
    Vertical,

    /// <summary>Advances color using the sum of each stored owner's horizontal and vertical offsets.</summary>
    Diagonal,
}
