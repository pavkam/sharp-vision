// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>Verifies changed-run construction and diagnostic formatting.</summary>
public sealed class DamageSpanTests
{
    /// <summary>Verifies the formatted run uses invariant digits.</summary>
    [Fact]
    public void ToString_WhenDamageSpanIsValid_UsesInvariantDigits() =>
        // Act and assert
        new DamageSpan(1, 2, 3).ToString().ShouldBe("DamageSpan { Row=1, Start=2, Length=3 }");
}
