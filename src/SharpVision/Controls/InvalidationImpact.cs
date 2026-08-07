// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies the earliest UI phase affected by a committed value change.</summary>
/// <remarks>
/// Values are ordered from no observable phase work through the earliest and
/// therefore strongest invalidation. Later phases are included transitively.
/// </remarks>
[PublicAPI]
public enum InvalidationImpact
{
    /// <summary>No layout or rendering phase is affected.</summary>
    None,

    /// <summary>Semantic cell output must be regenerated.</summary>
    Render,

    /// <summary>Committed bounds and semantic cell output must be regenerated.</summary>
    Arrange,

    /// <summary>Intrinsic size, committed bounds, and semantic cell output must be regenerated.</summary>
    Measure
}
