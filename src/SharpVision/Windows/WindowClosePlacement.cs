// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Windows;

/// <summary>Selects the title-bar edge that hosts a Window close control.</summary>
[PublicAPI]
public enum WindowClosePlacement
{
    /// <summary>Places the close control after the top-left frame corner.</summary>
    Left,

    /// <summary>Places the close control before the top-right frame corner.</summary>
    Right
}
