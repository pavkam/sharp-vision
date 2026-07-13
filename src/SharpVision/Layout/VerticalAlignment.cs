// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Defines vertical placement inside an arranged slot.</summary>
public enum VerticalAlignment
{
    /// <summary>Places content at the top edge.</summary>
    Top,

    /// <summary>Centers content vertically.</summary>
    Center,

    /// <summary>Places content at the bottom edge.</summary>
    Bottom,

    /// <summary>Uses the available vertical extent when no fixed size overrides it.</summary>
    Stretch,
}
