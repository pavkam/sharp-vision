// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics;

using SharpVision.Terminal.Graphics;

/// <summary>Proves graphics placement diagnostics preserve their published identity invariants.</summary>
public sealed class GraphicsPlacementDiagnosticTests
{
    /// <summary>Verifies the empty image sentinel cannot be published as a skipped placement.</summary>
    [Fact]
    public void Constructor_WhenImageIdentityIsZero_Throws()
    {
        var action = () =>
        {
            _ = new GraphicsPlacementDiagnostic(
                0,
                GraphicsPlacementSkipReason.FormatNotEncodable);
        };

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("ImageIdentity");
    }

    /// <summary>Verifies an unknown fallback classification cannot enter a diagnostic snapshot.</summary>
    [Fact]
    public void Constructor_WhenReasonIsUndefined_Throws()
    {
        var action = () =>
        {
            _ = new GraphicsPlacementDiagnostic(
                42,
                (GraphicsPlacementSkipReason) 999);
        };

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("Reason");
    }
}
