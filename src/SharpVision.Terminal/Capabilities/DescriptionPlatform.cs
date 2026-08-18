// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies the platform description family requested by console hosting.</summary>
internal enum DescriptionPlatform
{
    /// <summary>A Unix host using an ncurses database provider.</summary>
    Unix,

    /// <summary>A Windows console host which must not load ncurses.</summary>
    Windows
}
