// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

/// <summary>Controls whether a <see cref="Popup"/> self-manages its modal scope on open.</summary>
[PublicAPI]
public enum PopupModalBehavior
{
    /// <summary>The Popup enters a dismissing modal scope automatically when opened.</summary>
    Auto,

    /// <summary>The Popup does not manage modality; the owning control is responsible.</summary>
    None
}
