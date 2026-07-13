// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Selects compact track-only or full button-and-track scrollbar chrome.</summary>
public enum ScrollBarStyle
{
    /// <summary>Uses the entire arranged axis as a draggable track.</summary>
    Thin,

    /// <summary>Reserves decrement and increment button cells around the track.</summary>
    Full,
}
