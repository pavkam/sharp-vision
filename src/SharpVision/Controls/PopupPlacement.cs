// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Selects the preferred side of an anchor used by a Popup.</summary>
public enum PopupPlacement
{
    /// <summary>Places the popup immediately below its anchor when space permits.</summary>
    Below,

    /// <summary>Places the popup immediately above its anchor when space permits.</summary>
    Above,

    /// <summary>Places the popup immediately to the right of its anchor.</summary>
    Right,

    /// <summary>Places the popup immediately to the left of its anchor.</summary>
    Left,
}
