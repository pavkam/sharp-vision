// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Identifies xterm mouse event tracking levels.</summary>
public enum MouseTracking
{
    /// <summary>Report X10 button presses through mode 9.</summary>
    X10 = 9,

    /// <summary>Report VT200 button transitions through mode 1000.</summary>
    Press = 1000,

    /// <summary>Report button transitions and drag motion through mode 1002.</summary>
    Drag = 1002,

    /// <summary>Report all pointer motion through mode 1003.</summary>
    Any = 1003,
}
