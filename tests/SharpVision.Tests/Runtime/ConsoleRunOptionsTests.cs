// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;


using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies <see cref="ConsoleRunOptions"/> defaults and terminal/host mapping.</summary>
public sealed class ConsoleRunOptionsTests
{
    /// <summary>Verifies default construction reproduces today's interactive-console policy.</summary>
    [Fact]
    public void Defaults_WhenConstructed_ReproduceInteractiveConsolePolicy()
    {
        ConsoleRunOptions options = new();

        options.AlternateScreen.ShouldBeTrue();
        options.ShowCursor.ShouldBeFalse();
        options.MouseTracking.ShouldBe(MouseTracking.Any);
        options.MouseCoordinates.ShouldBe(MouseCoordinates.Sgr);
        options.BracketedPaste.ShouldBeTrue();
        options.FocusReporting.ShouldBeTrue();
        options.TreatControlCAsInput.ShouldBeFalse();
    }

    /// <summary>Verifies disabling mouse tracking leaves the terminal tracking mode null.</summary>
    [Fact]
    public void ToTerminalOptions_WhenMouseDisabled_LeavesTrackingNull()
    {
        ConsoleRunOptions options = new() { MouseTracking = null };

        TerminalOptions terminal = options.ToTerminalOptions();

        terminal.Tracking.ShouldBeNull();
    }

    /// <summary>Verifies opting into Ctrl+C as input maps to host control-key capture.</summary>
    [Fact]
    public void ToHostOptions_WhenControlCAsInput_CapturesControlKeys()
    {
        ConsoleRunOptions options = new() { TreatControlCAsInput = true };

        options.ToHostOptions().CaptureControlKeys.ShouldBeTrue();
    }

    /// <summary>Verifies a null theme resolves to the standard dark theme.</summary>
    [Fact]
    public void ResolveTheme_WhenThemeNull_ReturnsDark() =>
        new ConsoleRunOptions().ResolveTheme().ShouldBe(Themes.Dark);
}
