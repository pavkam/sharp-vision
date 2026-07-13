// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Unicode;

/// <summary>
/// Reports the pinned Unicode specification used by terminal cell geometry.
/// </summary>
public static class Info
{
    /// <summary>Gets the Unicode Character Database version.</summary>
    public const string Version = "17.0.0";

    /// <summary>Gets the UAX 29 revision used for grapheme segmentation.</summary>
    public const int GraphemeRevision = 47;

    /// <summary>Gets the UAX 11 revision used for East Asian width.</summary>
    public const int WidthRevision = 44;
}
