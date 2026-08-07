// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Kitty;

using SharpVision.Terminal.Kitty.Graphics;

/// <summary>Proves finite nonzero Kitty identifier ownership and reuse.</summary>
public sealed class IdentifierAllocatorTests
{
    /// <summary>Verifies active identifiers are unique and released identifiers are reusable.</summary>
    [Fact]
    public void Rent_WhenIdentifiersAreReleased_ReusesOnlyInactiveValues()
    {
        var allocator = new KittyGraphicsIdentifierAllocator(2);

        var first = allocator.Rent();
        var second = allocator.Rent();
        _ = Should.Throw<InvalidOperationException>(() => _ = allocator.Rent());
        allocator.Return(first);
        var reused = allocator.Rent();

        first.ShouldBe(1U);
        second.ShouldBe(2U);
        reused.ShouldBe(first);
    }

    /// <summary>Verifies zero, out-of-range, and duplicate returns cannot corrupt ownership.</summary>
    [Theory]
    [InlineData(0U)]
    [InlineData(3U)]
    public void Return_WhenIdentifierIsNotActive_RejectsIt(uint value)
    {
        var allocator = new KittyGraphicsIdentifierAllocator(2);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => allocator.Return(value));
    }
}
