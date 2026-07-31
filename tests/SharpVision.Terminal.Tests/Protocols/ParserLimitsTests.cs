// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies the finite control-sequence parsing limit contract.
/// </summary>
public sealed class ParserLimitsTests
{
    /// <summary>
    /// Verifies that every integer limit rejects zero.
    /// </summary>
    [Fact]
    public void Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new ParserLimits { MaxParameterBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new ParserLimits { MaxIntermediateBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new ParserLimits { MaxStringBytes = 0 });
    }

    /// <summary>
    /// Verifies that the default profile is bounded and suitable for an
    /// interactive terminal session.
    /// </summary>
    [Fact]
    public void Default_WhenRead_HasFiniteInteractiveBounds()
    {
        var limits = ParserLimits.Default;

        limits.MaxParameterBytes.ShouldBeInRange(1, 4_096);
        limits.MaxIntermediateBytes.ShouldBeInRange(1, 256);
        limits.MaxStringBytes.ShouldBeInRange(1, 16_777_216);
    }
}
