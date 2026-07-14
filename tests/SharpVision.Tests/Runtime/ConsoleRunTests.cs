// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Runtime;


using CapabilityOrigin = Terminal.Capabilities.Origin;
using CapabilitySupport = Terminal.Capabilities.Support;

/// <summary>Verifies console host defaults and interactive run helpers.</summary>
public sealed class ConsoleRunTests
{
    /// <summary>Verifies default console startup negotiates cell mouse and SGR any-event input.</summary>
    [Fact]
    public void CreateTerminalOptions_WhenCalled_EnablesNegotiatedCellMouse()
    {
        // Act
        Options terminal = ConsoleRun.CreateTerminalOptions();

        // Assert
        NegotiationOptions negotiation = terminal.Negotiation.ShouldNotBeNull();
        negotiation.Overrides.ShouldNotBeNull().CellMouse.ShouldBe(true);
        terminal.Capabilities.CellMouse.ShouldBe(
            new Feature(CapabilitySupport.Supported, CapabilityOrigin.Override));
        terminal.Tracking.ShouldBe(MouseTracking.Any);
        terminal.Coordinates.ShouldBe(MouseCoordinates.Sgr);
    }
}
