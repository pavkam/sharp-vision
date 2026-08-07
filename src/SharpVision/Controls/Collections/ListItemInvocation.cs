// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Selects which pointer gesture raises ListView's ItemInvoked event.</summary>
[PublicAPI]
public enum ListItemInvocation
{
    /// <summary>Every pointer activation raises ItemInvoked, in addition to applying selection.</summary>
    SingleClick,

    /// <summary>A pointer activation raises ItemInvoked only on a plain multi-click; a single
    /// pointer activation applies selection without invoking. A multi-click held with Control or
    /// Shift is a selection gesture, not a commit, so it only toggles or extends selection and
    /// never invokes even once the click count reaches a multi-click. Enter always raises
    /// ItemInvoked regardless of this setting.</summary>
    DoubleClick
}
