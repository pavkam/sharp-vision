// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Graphics;

/// <summary>Verifies completed render metrics retain immutable diagnostic snapshots.</summary>
public sealed class RenderingMetricsTests
{
    /// <summary>Verifies later caller-list mutation cannot rewrite completed-frame diagnostics.</summary>
    [Fact]
    public void Constructor_WhenDiagnosticsListMutates_PreservesSnapshot()
    {
        var diagnostic = new GraphicsPlacementDiagnostic(42, GraphicsPlacementSkipReason.OutputLimitExceeded);
        List<GraphicsPlacementDiagnostic> diagnostics = [diagnostic];
        var metrics = new RenderMetrics(
            bytes: 0,
            writes: 0,
            spans: 0,
            full: false,
            elapsed: TimeSpan.Zero,
            graphicsDiagnostics: diagnostics);

        diagnostics.Clear();

        metrics.GraphicsDiagnostics.ShouldHaveSingleItem().ShouldBe(diagnostic);
    }
}
