// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Wrap's validated public panel contract.</summary>
public sealed class WrapTests
{
    /// <summary>Verifies defaults and invalid setters preserve prior state.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasValidatedDefaults()
    {
        var wrap = new Wrap();

        wrap.Orientation.ShouldBe(Orientation.Horizontal);
        wrap.Spacing.ShouldBe(0);
        wrap.LineSpacing.ShouldBe(0);

        wrap.Orientation = Orientation.Vertical;
        wrap.Spacing = 1;
        wrap.LineSpacing = 2;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => wrap.Orientation = (Orientation) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => wrap.Spacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => wrap.LineSpacing = -1);

        wrap.Orientation.ShouldBe(Orientation.Vertical);
        wrap.Spacing.ShouldBe(1);
        wrap.LineSpacing.ShouldBe(2);
    }
}
