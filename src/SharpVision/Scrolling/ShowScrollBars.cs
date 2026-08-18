// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Scrolling;

/// <summary>Selects when enabled overflow axes reserve and render scrollbar chrome.</summary>
[PublicAPI]
public enum ShowScrollBars
{
    /// <summary>Never renders scrollbar chrome while retaining enabled-axis scrolling.</summary>
    Never,

    /// <summary>Renders chrome only when content exceeds the viewport.</summary>
    WhenNeeded,

    /// <summary>Always reserves and renders chrome for enabled axes.</summary>
    Always
}
