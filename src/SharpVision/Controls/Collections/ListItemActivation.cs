// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Selects which pointer gesture raises ListView's ItemInvoked event.</summary>
[PublicAPI]
public enum ListItemActivation
{
    /// <summary>Every pointer activation raises ItemInvoked, in addition to applying selection. The default.</summary>
    SingleClick,

    /// <summary>A pointer activation raises ItemInvoked only on a multi-click; a single pointer
    /// activation applies selection without invoking. Keyboard activation always raises
    /// ItemInvoked regardless of this setting.</summary>
    DoubleClick
}
