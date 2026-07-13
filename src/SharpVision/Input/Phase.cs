// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Identifies the direction of one routed-event pass.</summary>
public enum Phase
{
    /// <summary>Travels from the root toward the original target.</summary>
    Preview,

    /// <summary>Travels from the original target toward the root.</summary>
    Bubble,
}
