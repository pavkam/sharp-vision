// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Defines how a tab header strip handles content wider than its viewport.</summary>
public enum TabHeaderOverflowPolicy
{
    /// <summary>Clip headers outside the available width.</summary>
    Clip,

    /// <summary>Scroll the header strip to keep the selected header reachable.</summary>
    Scroll,
}
