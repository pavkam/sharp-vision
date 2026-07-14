// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;

/// <summary>Verifies a role color is rejected before palette projection at every color depth.</summary>
public sealed class PaletteRoleGuardTests
{
    /// <summary>Verifies a role color throws instead of being misread as a palette index.</summary>
    /// <param name="depth">The terminal color tier under test.</param>
    [Theory]
    [InlineData(ColorDepth.Monochrome)]
    [InlineData(ColorDepth.Basic16)]
    [InlineData(ColorDepth.Indexed256)]
    [InlineData(ColorDepth.TrueColor)]
    public void Project_WhenSourceIsRole_Throws(ColorDepth depth) =>
        Should.Throw<InvalidOperationException>(() => Palette.Project(Color.Role(1), depth));

    /// <summary>Verifies a concrete color still projects normally alongside the new guard.</summary>
    [Fact]
    public void Project_WhenSourceIsConcrete_ReturnsProjection() =>
        Palette.Project(Color.Rgb(1, 2, 3), ColorDepth.TrueColor).ShouldBe(Color.Rgb(1, 2, 3));
}
