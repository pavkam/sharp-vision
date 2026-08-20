// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TreeViewChildContext's constructor validation and immutable property exposure.</summary>
public sealed class TreeViewChildContextTests
{
    /// <summary>Verifies a null header is rejected.</summary>
    [Fact]
    public void Constructor_WhenHeaderIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(() => new TreeViewChildContext("key", null!));

    /// <summary>Verifies a null key is accepted and reported, matching a caller-authored root.</summary>
    [Fact]
    public void Key_WhenConstructedWithNullKey_IsNull()
    {
        var context = new TreeViewChildContext(null, "Root");

        context.Key.ShouldBeNull();
        context.Header.ShouldBe("Root");
    }

    /// <summary>Verifies a non-null key and header round-trip through the properties.</summary>
    [Fact]
    public void Properties_WhenConstructed_RoundTrip()
    {
        var context = new TreeViewChildContext("node-1", "Node One");

        context.Key.ShouldBe("node-1");
        context.Header.ShouldBe("Node One");
    }
}
