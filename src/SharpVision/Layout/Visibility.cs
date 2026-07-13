// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Defines whether a control participates in layout and rendering.</summary>
public enum Visibility
{
    /// <summary>Participates in layout and rendering.</summary>
    Visible,

    /// <summary>Participates in layout but not rendering or input.</summary>
    Hidden,

    /// <summary>Participates in neither layout, rendering, nor input.</summary>
    Collapsed,
}
