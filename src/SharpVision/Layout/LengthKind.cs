// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Identifies how one layout axis resolves its requested extent.</summary>
[PublicAPI]
public enum LengthKind
{
    /// <summary>Uses the content's intrinsic desired extent.</summary>
    Auto,

    /// <summary>Uses an exact integer terminal-cell extent.</summary>
    Cells,

    /// <summary>Uses a percentage of the final containing content extent.</summary>
    Percent,

    /// <summary>Uses a weighted share of remaining bounded space.</summary>
    Star
}
