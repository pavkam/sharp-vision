// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Layout;

/// <summary>Stores weakly attached optional Canvas offsets.</summary>
internal sealed class Position
{
    /// <summary>Gets or sets the leading horizontal offset.</summary>
    internal Length? Left { get; set; }

    /// <summary>Gets or sets the leading vertical offset.</summary>
    internal Length? Top { get; set; }

    /// <summary>Gets or sets the trailing horizontal offset.</summary>
    internal Length? Right { get; set; }

    /// <summary>Gets or sets the trailing vertical offset.</summary>
    internal Length? Bottom { get; set; }
}
