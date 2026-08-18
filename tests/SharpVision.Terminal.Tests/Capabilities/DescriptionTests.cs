// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>Verifies immutable terminal-description metadata and validation.</summary>
public sealed class DescriptionTests
{
    /// <summary>Verifies validated metadata is exposed without reinterpretation.</summary>
    [Fact]
    public void Constructor_WhenValuesAreValid_ExposesMetadata()
    {
        // Arrange / Act
        var description = new Description(
            "xterm-direct",
            DescriptionOrigin.Database,
            Suitability.Usable,
            columns: 132,
            lines: 43,
            colors: 16_777_216,
            automaticMargins: true,
            backColorErase: true,
            eatNewlineGlitch: true);

        // Assert
        description.Name.ShouldBe("xterm-direct");
        description.Origin.ShouldBe(DescriptionOrigin.Database);
        description.Suitability.ShouldBe(Suitability.Usable);
        description.Columns.ShouldBe(132);
        description.Lines.ShouldBe(43);
        description.Colors.ShouldBe(16_777_216);
        description.AutomaticMargins.ShouldBeTrue();
        description.BackColorErase.ShouldBeTrue();
        description.EatNewlineGlitch.ShouldBeTrue();

        var unsuitable = description.WithSuitability(Suitability.Generic);
        unsuitable.EatNewlineGlitch.ShouldBeTrue();
    }

    /// <summary>Verifies a missing or blank terminal name is rejected.</summary>
    [Fact]
    public void Constructor_WhenNameIsMissingOrBlank_Throws()
    {
        // Arrange / Act / Assert
        _ = Should.Throw<ArgumentNullException>(() =>
            new Description(null!, DescriptionOrigin.BuiltIn, Suitability.Usable));
        _ = Should.Throw<ArgumentException>(() =>
            new Description(" \t", DescriptionOrigin.BuiltIn, Suitability.Usable));
    }

    /// <summary>Verifies unknown origin and suitability values are rejected.</summary>
    [Fact]
    public void Constructor_WhenEnumValueIsUndefined_Throws()
    {
        // Arrange / Act / Assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Description("ansi", (DescriptionOrigin) int.MaxValue, Suitability.Usable));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Description("ansi", DescriptionOrigin.BuiltIn, (Suitability) int.MaxValue));
    }

    /// <summary>Verifies present dimensions and color counts must be positive.</summary>
    /// <param name="value">The invalid present numeric value.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenNumericValueIsNotPositive_Throws(int value)
    {
        // Arrange / Act / Assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Description(
                "ansi",
                DescriptionOrigin.BuiltIn,
                Suitability.Usable,
                columns: value));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Description(
                "ansi",
                DescriptionOrigin.BuiltIn,
                Suitability.Usable,
                lines: value));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Description(
                "ansi",
                DescriptionOrigin.BuiltIn,
                Suitability.Usable,
                colors: value));
    }
}
