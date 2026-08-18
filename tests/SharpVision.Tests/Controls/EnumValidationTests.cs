// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the shared enum guards controls delegate to.</summary>
public sealed class EnumValidationTests
{
    /// <summary>Verifies a value using only allowed bits passes.</summary>
    [Fact]
    public void ThrowIfUndefinedFlags_WhenValueUsesOnlyAllowedBits_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfUndefinedFlags(ScrollBars.Horizontal, ScrollBars.Both, "value", "message"));

    /// <summary>Verifies a value equal to the allowed mask - every bit set, none outside - passes.</summary>
    [Fact]
    public void ThrowIfUndefinedFlags_WhenValueEqualsAllowedMask_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfUndefinedFlags(ScrollBars.Both, ScrollBars.Both, "value", "message"));

    /// <summary>Verifies a value with a bit outside the allowed mask is rejected, reporting the
    /// caller's parameter name, the offending value, and the supplied message.</summary>
    [Fact]
    public void ThrowIfUndefinedFlags_WhenValueHasBitOutsideAllowedMask_Throws()
    {
        var outOfMask = (ScrollBars) 4;

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentOutOfRangeException.ThrowIfUndefinedFlags(outOfMask, ScrollBars.Both, "value", "message"));

        exception.ParamName.ShouldBe("value");
        exception.ActualValue.ShouldBe(outOfMask);
        exception.Message.ShouldStartWith("message");
    }
}
