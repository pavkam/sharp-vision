// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Selects behavior when a wide cluster cannot fit at the right frame edge.
/// </summary>
public enum Edge
{
    /// <summary>Skip the complete cluster.</summary>
    Clip,

    /// <summary>Move the complete cluster to the next row.</summary>
    Wrap,

    /// <summary>Write one narrow U+FFFD replacement.</summary>
    Replace,
}
