// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Clipboard;

using SharpVision.Terminal.Clipboard;

/// <summary>
/// Verifies the finite clipboard transfer limit contract.
/// </summary>
public sealed class TransferLimitsTests
{
    /// <summary>
    /// Verifies that every integer limit rejects zero.
    /// </summary>
    [Fact]
    public void Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new TransferLimits { MaxClipboardBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new TransferLimits { MaxMetadataBytes = 0 });
    }

    /// <summary>
    /// Verifies that the default profile is bounded and suitable for an
    /// interactive terminal session.
    /// </summary>
    [Fact]
    public void Default_WhenRead_HasFiniteInteractiveBounds()
    {
        var limits = TransferLimits.Default;

        limits.MaxClipboardBytes.ShouldBeInRange(1, 67_108_864);
        limits.MaxMetadataBytes.ShouldBeInRange(1, 65_536);
    }
}
