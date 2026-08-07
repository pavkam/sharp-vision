// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies the visual layer occupied by controls in an ownership slot.</summary>
internal enum OwnedControlLayer
{
    /// <summary>Places controls in the owner's ordinary visual and input layer.</summary>
    Normal,

    /// <summary>Places controls in the elevated popup visual and input layer.</summary>
    Popup
}
