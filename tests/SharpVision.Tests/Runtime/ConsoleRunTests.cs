using SharpVision.Runtime;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Runtime;

using Shouldly;

using CapabilityOrigin = SharpVision.Terminal.Capabilities.Origin;
using CapabilitySupport = SharpVision.Terminal.Capabilities.Support;

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies console host defaults and interactive run helpers.</summary>
public sealed class ConsoleRunTests
{
    /// <summary>Verifies default console startup negotiates cell mouse and SGR any-event input.</summary>
    [Fact]
    public void CreateTerminalOptions_WhenCalled_EnablesNegotiatedCellMouse()
    {
        // Act
        var terminal = ConsoleRun.CreateTerminalOptions();

        // Assert
        var negotiation = terminal.Negotiation.ShouldNotBeNull();
        negotiation.Overrides.ShouldNotBeNull().CellMouse.ShouldBe(true);
        terminal.Capabilities.CellMouse.ShouldBe(
            new Feature(CapabilitySupport.Supported, CapabilityOrigin.Override));
        terminal.Tracking.ShouldBe(MouseTracking.Any);
        terminal.Coordinates.ShouldBe(MouseCoordinates.Sgr);
    }

    /// <summary>Verifies unsupported hosts receive a no-op raw-input lease.</summary>
    [Fact]
    public void Enter_WhenHostIsUnsupported_DisposesWithoutThrowing()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return;
        }

        using var mode = ConsoleInputMode.Enter();
        _ = mode.ShouldNotBeNull();
    }
}
