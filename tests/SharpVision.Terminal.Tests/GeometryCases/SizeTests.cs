// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.GeometryCases;

/// <summary>Verifies non-negative extent construction and diagnostic formatting.</summary>
public sealed class SizeTests
{
    /// <summary>Verifies the formatted extents use invariant digits and the multiplication sign
    /// separator.</summary>
    [Fact]
    public void ToString_WhenSizeIsValid_UsesInvariantDigits() =>
        // Act and assert
        new Size(12, 34).ToString().ShouldBe("12×34");
}
