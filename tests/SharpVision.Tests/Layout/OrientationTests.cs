// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies the shared panel orientation enum's defined values.</summary>
public sealed class OrientationTests
{
    /// <summary>Verifies the shared panel orientation has exactly two defined values.</summary>
    [Fact]
    public void Orientation_WhenEnumerated_ContainsVerticalAndHorizontal() =>
        Enum.GetValues<Orientation>().ShouldBe([Orientation.Vertical, Orientation.Horizontal]);
}
