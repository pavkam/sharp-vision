// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies immutable axis constraint values.</summary>
public sealed class ConstraintTests
{
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
}
