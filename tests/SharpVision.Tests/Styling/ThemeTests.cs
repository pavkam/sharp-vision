// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies public Theme construction preserves valid immutable metadata.</summary>
public sealed class ThemeTests
{
    /// <summary>Verifies an undefined color-scheme value is rejected before publication.</summary>
    [Fact]
    public void Constructor_WhenColorSchemeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Theme(colorScheme: (ColorScheme) int.MaxValue));
    }
}
