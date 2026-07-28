// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Defines how a typed event traverses the control ancestry.</summary>
[PublicAPI]
public enum Strategy
{
    /// <summary>Invokes preview root-to-target, then bubble target-to-root.</summary>
    TunnelBubble,

    /// <summary>Invokes only bubble target-to-root.</summary>
    Bubble,

    /// <summary>Invokes only the target.</summary>
    Direct
}
