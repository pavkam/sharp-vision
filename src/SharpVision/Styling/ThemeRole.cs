// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Identifies one global semantic appearance profile selected by library controls.</summary>
public enum ThemeRole
{
    /// <summary>The passive base-control profile.</summary>
    Control,
    /// <summary>The editable or selectable input profile.</summary>
    Input,
    /// <summary>The framed grouping or collection profile.</summary>
    Container,
    /// <summary>The top-level window profile.</summary>
    Window,
    /// <summary>The transient popup profile.</summary>
    Popup
}
