// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies Face construction preserves its invariants.</summary>
public sealed class FaceTests
{
    /// <summary>Verifies transparent foregrounds are rejected before a face is constructed.</summary>
    [Fact]
    public void Constructor_WhenForegroundIsTransparent_ThrowsArgumentException()
    {
        _ = Should.Throw<ArgumentException>(() => new Face(
            Color.Transparent,
            Color.Default,
            TerminalAttributes.None,
            Underline.None,
            Color.Default));
    }
}
