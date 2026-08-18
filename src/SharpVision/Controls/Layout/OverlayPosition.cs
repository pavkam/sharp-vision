// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Stores weakly attached Overlay position offsets.</summary>
internal sealed class OverlayPosition
{
    /// <summary>Gets or sets the leading horizontal offset.</summary>
    public Length? Left { get; set; }

    /// <summary>Gets or sets the leading vertical offset.</summary>
    public Length? Top { get; set; }

    /// <summary>Gets or sets the trailing horizontal offset.</summary>
    public Length? Right { get; set; }

    /// <summary>Gets or sets the trailing vertical offset.</summary>
    public Length? Bottom { get; set; }
}
