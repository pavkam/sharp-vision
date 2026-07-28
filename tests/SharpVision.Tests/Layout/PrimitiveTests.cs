// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies immutable layout units and saturated box-model geometry.</summary>
public sealed class PrimitiveTests
{
    /// <summary>Verifies the shared panel orientation has exactly two defined values.</summary>
    [Fact]
    public void Orientation_WhenEnumerated_ContainsVerticalAndHorizontal() =>
        Enum.GetValues<Orientation>().ShouldBe([Orientation.Vertical, Orientation.Horizontal]);

    /// <summary>Verifies every supported length factory preserves its exact value.</summary>
    [Fact]
    public void Factory_WhenLengthIsValid_PreservesKindAndValue()
    {
        Length.Auto.ShouldBe(default);
        Length.Cells(0).ShouldBe(new Length(Kind.Cells, 0));
        Length.Cells(14).ShouldBe(new Length(Kind.Cells, 14));
        Length.Percent(37.5).ShouldBe(new Length(Kind.Percent, 37.5));
        Length.Star(2.5).ShouldBe(new Length(Kind.Star, 2.5));
    }

    /// <summary>Verifies invalid fixed, percentage, and proportional values are rejected.</summary>
    [Theory]
    [InlineData(Kind.Cells, -1)]
    [InlineData(Kind.Cells, 1.5)]
    [InlineData(Kind.Percent, -0.1)]
    [InlineData(Kind.Percent, 100.1)]
    [InlineData(Kind.Percent, double.NaN)]
    [InlineData(Kind.Star, 0)]
    [InlineData(Kind.Star, double.PositiveInfinity)]
    public void Constructor_WhenLengthIsInvalid_ThrowsArgumentOutOfRangeException(
        Kind kind,
        double value) =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Length(kind, value));

    /// <summary>Verifies automatic lengths reject a meaningless numeric payload.</summary>
    [Fact]
    public void Constructor_WhenAutomaticLengthHasValue_ThrowsArgumentException()
    {
        _ = Should.Throw<ArgumentException>(() => new Length(Kind.Auto, 1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Length((Kind) int.MaxValue, 0));
    }

    /// <summary>Verifies nullable axes distinguish unbounded from bounded zero.</summary>
    [Fact]
    public void Constructor_WhenConstraintIsValid_PreservesAxisBounds()
    {
        var value = new Constraint(width: null, height: 0);

        value.IsWidthBounded.ShouldBeFalse();
        value.IsHeightBounded.ShouldBeTrue();
        value.Width.ShouldBeNull();
        value.Height.ShouldBe(0);
    }

    /// <summary>Verifies negative bounded constraints are rejected.</summary>
    [Fact]
    public void Constructor_WhenConstraintIsNegative_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Constraint(-1, null));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Constraint(null, -1));
    }

    /// <summary>Verifies uniform and axis constructors map to physical edges.</summary>
    [Fact]
    public void Constructor_WhenThicknessIsValid_PreservesEdgesAndSums()
    {
        new Thickness(2).ShouldBe(new Thickness(2, 2, 2, 2));
        new Thickness(horizontal: 2, vertical: 3)
            .ShouldBe(new Thickness(2, 3, 2, 3));
        var value = new Thickness(1, 2, 3, 4);

        value.Horizontal.ShouldBe(4);
        value.Vertical.ShouldBe(6);
    }

    /// <summary>Verifies invalid edges and overflowing sums fail during construction.</summary>
    [Fact]
    public void Constructor_WhenThicknessIsInvalid_ThrowsBeforeConstruction()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Thickness(-1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Thickness(0, -1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Thickness(0, 0, -1, 0));
        _ = Should.Throw<OverflowException>(() => new Thickness(int.MaxValue, 0, 1, 0));
    }

    /// <summary>Verifies box deflation saturates without creating negative extents.</summary>
    [Fact]
    public void Deflate_WhenThicknessExceedsGeometry_SaturatesAtZero()
    {
        var value = new Thickness(1, 2, 3, 4);

        value.Deflate(new Size(2, 3)).ShouldBe(default);
        value.Deflate(new Rect(10, 20, 2, 3)).ShouldBe(new Rect(11, 22, 0, 0));
    }

    /// <summary>Verifies alignment and visibility values remain stable public vocabulary.</summary>
    [Fact]
    public void Values_WhenEnumerated_ContainDocumentedMembers()
    {
        Enum.GetValues<HorizontalAlignment>()
            .ShouldBe([
                HorizontalAlignment.Left,
                HorizontalAlignment.Center,
                HorizontalAlignment.Right,
                HorizontalAlignment.Stretch
            ]);
        Enum.GetValues<VerticalAlignment>()
            .ShouldBe([
                VerticalAlignment.Top,
                VerticalAlignment.Center,
                VerticalAlignment.Bottom,
                VerticalAlignment.Stretch
            ]);
        Enum.GetValues<Visibility>()
            .ShouldBe([Visibility.Visible, Visibility.Hidden, Visibility.Collapsed]);
    }
}
