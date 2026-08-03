// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using Terminal.Graphics;

/// <summary>Proves graphics diagnostic events own the completed frame snapshot they publish.</summary>
public sealed class GraphicsDiagnosticEventArgsTests
{
    /// <summary>Verifies later caller mutation cannot rewrite a previously constructed event.</summary>
    [Fact]
    public void Constructor_WhenPlacementsMutate_PreservesSnapshot()
    {
        var diagnostic = new GraphicsPlacementDiagnostic(
            42,
            GraphicsPlacementSkipReason.FormatNotEncodable);
        var placements = new List<GraphicsPlacementDiagnostic> { diagnostic };
        var eventArgs = new GraphicsDiagnosticEventArgs(placements);

        placements.Clear();

        eventArgs.Placements.ShouldHaveSingleItem().ShouldBe(diagnostic);
    }
}
