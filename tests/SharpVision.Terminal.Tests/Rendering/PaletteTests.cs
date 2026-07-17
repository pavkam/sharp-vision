// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;



/// <summary>Verifies deterministic semantic-color projection to terminal tiers.</summary>
public sealed class PaletteTests
{
    /// <summary>Provides exact reference-palette and degradation cases.</summary>
    public static TheoryData<Color, ColorDepth, Color> ProjectionCases => new()
    {
        { Color.Default, ColorDepth.Monochrome, Color.Default },
        { Color.Rgb(1, 2, 3), ColorDepth.Monochrome, Color.Default },
        { Color.Rgb(1, 2, 3), ColorDepth.TrueColor, Color.Rgb(1, 2, 3) },
        { Color.Indexed(255), ColorDepth.TrueColor, Color.Indexed(255) },
        { Color.Indexed(67), ColorDepth.Indexed256, Color.Indexed(67) },
        { Color.Rgb(95, 135, 175), ColorDepth.Indexed256, Color.Indexed(67) },
        { Color.Rgb(128, 128, 128), ColorDepth.Indexed256, Color.Indexed(244) },
        { Color.Rgb(0, 0, 0), ColorDepth.Indexed256, Color.Indexed(0) },
        { Color.Indexed(9), ColorDepth.Basic16, Color.Indexed(9) },
        { Color.Rgb(255, 0, 0), ColorDepth.Basic16, Color.Indexed(9) },
        { Color.Indexed(231), ColorDepth.Basic16, Color.Indexed(15) },
        { Color.Indexed(232), ColorDepth.Basic16, Color.Indexed(0) },
        { Color.Indexed(255), ColorDepth.Basic16, Color.Indexed(7) },
    };

    /// <summary>Verifies exact palette points and tier degradation.</summary>
    /// <param name="source">The semantic input color.</param>
    /// <param name="depth">The active terminal color tier.</param>
    /// <param name="expected">The exact projected representation.</param>
    [Theory]
    [MemberData(nameof(ProjectionCases))]
    public void Project_WhenDepthIsSelected_ReturnsExpectedRepresentation(
        Color source,
        ColorDepth depth,
        Color expected) => Palette.Project(source, depth).ShouldBe(expected);

    /// <summary>Verifies every indexed source remains inside the target range.</summary>
    [Fact]
    public void Project_WhenEveryIndexIsDegraded_RemainsInsideTier()
    {
        for (var index = 0; index <= byte.MaxValue; index++)
        {
            var source = Color.Indexed(index);
            var basic = Palette.Project(source, ColorDepth.Basic16);
            var indexed = Palette.Project(source, ColorDepth.Indexed256);

            basic.Kind.ShouldBe(ColorKind.Indexed);
            basic.Red.ShouldBeLessThan((byte) 16);
            indexed.ShouldBe(source);
        }
    }

    /// <summary>Verifies one indexed reference entry resolves to its exact RGB components.</summary>
    [Fact]
    public void Resolve_WhenIndexedColorIsSupplied_ReturnsReferenceRgb() =>
        Palette.Resolve(Color.Indexed(67)).ShouldBe(Color.Rgb(95, 135, 175));

    /// <summary>Verifies concrete RGB and terminal-default colors need no indexed resolution.</summary>
    [Fact]
    public void Resolve_WhenColorIsNotIndexed_PreservesRepresentation()
    {
        var rgb = Color.Rgb(12, 34, 56);

        Palette.Resolve(rgb).ShouldBe(rgb);
        Palette.Resolve(Color.Default).ShouldBe(Color.Default);
    }

    /// <summary>Verifies projection is deterministic and idempotent for random RGB colors.</summary>
    [Fact]
    public void Project_WhenRandomColorsAreRepeated_IsDeterministicAndIdempotent()
    {
        var random = new Random(0x00C01012);

        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var source = Color.Rgb(random.Next(256), random.Next(256), random.Next(256));

            foreach (var depth in Enum.GetValues<ColorDepth>())
            {
                var first = Palette.Project(source, depth);
                var second = Palette.Project(source, depth);

                second.ShouldBe(first);
                Palette.Project(first, depth).ShouldBe(first);
            }
        }
    }
}
