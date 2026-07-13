// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Scrolling;


/// <summary>Identifies the source of one committed scroll change.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Pointer is the conventional scrolling input path name.")]
public enum Cause
{
    /// <summary>A direct API requested the change.</summary>
    Programmatic,

    /// <summary>A keyboard command requested the change.</summary>
    Keyboard,

    /// <summary>A pointer button, track, or drag requested the change.</summary>
    Pointer,

    /// <summary>A cell or pixel wheel delta requested the change.</summary>
    Wheel,

    /// <summary>Focus or an explicit request brought content into view.</summary>
    BringIntoView,

    /// <summary>Content extent changed and clamped the value.</summary>
    Content,

    /// <summary>Viewport resize changed and clamped the value.</summary>
    Resize,
}
