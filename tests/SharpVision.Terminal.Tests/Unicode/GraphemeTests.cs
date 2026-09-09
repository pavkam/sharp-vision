// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Unicode;

/// <summary>Verifies extended-grapheme segment construction and diagnostic formatting.</summary>
public sealed class GraphemeTests
{
    /// <summary>Verifies the formatted segment uses invariant digits and reports invalid-data
    /// replacement only when present.</summary>
    [Fact]
    public void ToString_WhenGraphemeIsValid_UsesInvariantDigits()
    {
        // Act and assert
        new Grapheme(2, 3, hasInvalidData: false).ToString()
            .ShouldBe("Grapheme { Offset=2, Length=3 }");
        new Grapheme(2, 3, hasInvalidData: true).ToString()
            .ShouldBe("Grapheme { Offset=2, Length=3, Invalid }");
    }
}
