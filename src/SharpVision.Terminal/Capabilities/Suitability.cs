// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies whether terminal-description evidence permits full-screen use.</summary>
[PublicAPI]
public enum Suitability
{
    /// <summary>The description provides the validated operations required for full-screen use.</summary>
    Usable,

    /// <summary>No terminal description was available.</summary>
    Missing,

    /// <summary>The description declares itself generic rather than terminal-specific.</summary>
    Generic,

    /// <summary>The description targets a hardcopy device rather than an interactive display.</summary>
    Hardcopy,

    /// <summary>The description omits or invalidates an operation required for full-screen use.</summary>
    Incomplete,

    /// <summary>The description requires terminal padding that SharpVision does not emit.</summary>
    UnsupportedPadding
}
