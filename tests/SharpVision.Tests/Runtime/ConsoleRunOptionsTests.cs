// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;


using CapabilityOrigin = Origin;
using CapabilitySupport = Terminal.Capabilities.Support;

/// <summary>Verifies <see cref="ConsoleRunOptions"/> defaults and terminal/host mapping.</summary>
public sealed class ConsoleRunOptionsTests
{
    /// <summary>Verifies default construction reproduces today's interactive-console policy.</summary>
    [Fact]
    public void Defaults_WhenConstructed_ReproduceInteractiveConsolePolicy()
    {
        var options = new ConsoleRunOptions();

        options.AlternateScreen.ShouldBeTrue();
        options.ShowCursor.ShouldBeFalse();
        options.MouseTracking.ShouldBe(MouseTracking.Any);
        options.MouseCoordinates.ShouldBe(MouseCoordinates.Sgr);
        options.BracketedPaste.ShouldBeTrue();
        options.FocusReporting.ShouldBeTrue();
        options.TreatControlCAsInput.ShouldBeFalse();
    }

    /// <summary>Verifies default startup negotiates cell mouse and SGR any-event input.</summary>
    [Fact]
    public void ToTerminalOptions_WhenDefault_EnablesNegotiatedCellMouse()
    {
        // Act
        var terminal = new ConsoleRunOptions().ToTerminalOptions();

        // Assert
        var negotiation = terminal.Negotiation.ShouldNotBeNull();
        negotiation.Overrides.ShouldNotBeNull().CellMouse.ShouldBe(true);
        terminal.Capabilities.CellMouse.ShouldBe(
            new Feature(CapabilitySupport.Supported, CapabilityOrigin.Override));
        terminal.Tracking.ShouldBe(MouseTracking.Any);
        terminal.Coordinates.ShouldBe(MouseCoordinates.Sgr);
    }

    /// <summary>Verifies disabling mouse tracking leaves the terminal tracking mode null.</summary>
    [Fact]
    public void ToTerminalOptions_WhenMouseDisabled_LeavesTrackingNull()
    {
        var options = new ConsoleRunOptions() { MouseTracking = null };

        var terminal = options.ToTerminalOptions();

        terminal.Tracking.ShouldBeNull();
    }

    /// <summary>Verifies opting into Ctrl+C as input maps to host control-key capture.</summary>
    [Fact]
    public void ToHostOptions_WhenControlCAsInput_CapturesControlKeys()
    {
        var options = new ConsoleRunOptions() { TreatControlCAsInput = true };

        options.ToHostOptions().CaptureControlKeys.ShouldBeTrue();
    }

    /// <summary>Verifies a null theme resolves to the standard dark theme.</summary>
    [Fact]
    public void ResolveTheme_WhenThemeNull_ReturnsDark() =>
        new ConsoleRunOptions().ResolveTheme().ShouldBe(Themes.Dark);

    /// <summary>Verifies a forced color depth is threaded into negotiation overrides so it survives renegotiation.</summary>
    [Fact]
    public void ToTerminalOptions_WhenColorDepthForced_ThreadsItIntoNegotiationOverrides()
    {
        var options = new ConsoleRunOptions() { ColorDepth = ColorDepth.Monochrome };

        var terminal = options.ToTerminalOptions();

        terminal.Negotiation.ShouldNotBeNull().Overrides.ShouldNotBeNull().ColorDepth.ShouldBe(ColorDepth.Monochrome);
    }

    /// <summary>Verifies the default (unforced) color depth leaves the negotiation override null.</summary>
    [Fact]
    public void ToTerminalOptions_WhenColorDepthDefault_LeavesNegotiationOverrideNull()
    {
        var terminal = new ConsoleRunOptions().ToTerminalOptions();

        terminal.Negotiation.ShouldNotBeNull().Overrides.ShouldNotBeNull().ColorDepth.ShouldBeNull();
    }
}
