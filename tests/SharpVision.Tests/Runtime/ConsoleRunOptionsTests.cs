// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Terminal.Kitty.Keyboard;

using Terminal.Multiplexing;

using MultiplexerKind = Terminal.Multiplexing.MultiplexerKind;

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
        options.ClipboardPasteEvents.ShouldBeFalse();
        options.ClipboardOperationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.TreatControlCAsInput.ShouldBeFalse();
        options.DiagnosticPromotions.ShouldBe(DiagnosticPromotion.None);
    }

    /// <summary>Verifies selected strict diagnostic families reach terminal session policy unchanged.</summary>
    [Fact]
    public void ToTerminalOptions_WhenDiagnosticFamiliesPromoted_PreservesSelection()
    {
        var promotions = DiagnosticPromotion.MalformedInput | DiagnosticPromotion.CleanupFailure;
        var options = new ConsoleRunOptions { DiagnosticPromotions = promotions };

        var terminal = options.ToTerminalOptions(Ansi());

        terminal.DiagnosticPromotions.ShouldBe(promotions);
    }

    /// <summary>Verifies undefined promotion bits are rejected before a console is opened.</summary>
    [Fact]
    public void DiagnosticPromotions_WhenValueHasUnknownBits_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ConsoleRunOptions { DiagnosticPromotions = (DiagnosticPromotion) 32 });

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies an undefined enhancement bit is rejected at the option boundary instead of
    /// surfacing from Application.StartAsync with a parameter name the caller never wrote.</summary>
    [Fact]
    public void KeyboardEnhancement_WhenValueHasUnknownBits_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ConsoleRunOptions { KeyboardEnhancement = (KittyKeyboardEnhancement) 64 });

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies AssociatedText without AllKeys is rejected at the option boundary.</summary>
    [Fact]
    public void KeyboardEnhancement_WhenAssociatedTextIsSetWithoutAllKeys_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new ConsoleRunOptions { KeyboardEnhancement = KittyKeyboardEnhancement.AssociatedText });

        exception.ShouldNotBeOfType<ArgumentOutOfRangeException>();
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies every defined combination, including AssociatedText paired with AllKeys, is accepted.</summary>
    [Fact]
    public void KeyboardEnhancement_WhenAssociatedTextIsPairedWithAllKeys_DoesNotThrow()
    {
        var options = Should.NotThrow(() =>
            new ConsoleRunOptions { KeyboardEnhancement = KittyKeyboardEnhancement.AllKeys | KittyKeyboardEnhancement.AssociatedText });

        options.KeyboardEnhancement.ShouldBe(KittyKeyboardEnhancement.AllKeys | KittyKeyboardEnhancement.AssociatedText);
    }

    /// <summary>Verifies disabling keyboard enhancement entirely remains accepted.</summary>
    [Fact]
    public void KeyboardEnhancement_WhenNull_DoesNotThrow()
    {
        var options = Should.NotThrow(() => new ConsoleRunOptions { KeyboardEnhancement = null });

        options.KeyboardEnhancement.ShouldBeNull();
    }

    /// <summary>Verifies default startup negotiates cell mouse and SGR any-event input.</summary>
    [Fact]
    public void ToTerminalOptions_WhenDefault_EnablesNegotiatedCellMouse()
    {
        // Act
        var terminal = new ConsoleRunOptions().ToTerminalOptions(Ansi());

        // Assert
        var negotiation = terminal.Negotiation.ShouldNotBeNull();
        negotiation.Overrides.ShouldNotBeNull().CellMouse.ShouldBe(true);
        terminal.Profile.Capabilities.CellMouse.ShouldBe(Feature.Unknown);
        terminal.Tracking.ShouldBe(MouseTracking.Any);
        terminal.Coordinates.ShouldBe(MouseCoordinates.Sgr);
    }

    /// <summary>Verifies disabling mouse tracking leaves the terminal tracking mode null.</summary>
    [Fact]
    public void ToTerminalOptions_WhenMouseDisabled_LeavesTrackingNull()
    {
        var options = new ConsoleRunOptions { MouseTracking = null };

        var terminal = options.ToTerminalOptions(Ansi());

        terminal.Tracking.ShouldBeNull();
    }

    /// <summary>Verifies the explicit Kitty paste-event opt-in reaches session policy unchanged.</summary>
    [Fact]
    public void ToTerminalOptions_WhenClipboardPasteEventsEnabled_EnablesSessionLease()
    {
        var options = new ConsoleRunOptions { ClipboardPasteEvents = true };

        var terminal = options.ToTerminalOptions(Ansi());

        terminal.ClipboardPasteEvents.ShouldBeTrue();
    }

    /// <summary>Verifies the human-interaction deadline reaches terminal clipboard services
    /// independently of startup negotiation limits.</summary>
    [Fact]
    public void ToTerminalOptions_WhenClipboardOperationTimeoutConfigured_PreservesDeadline()
    {
        var timeout = TimeSpan.FromSeconds(45);
        var options = new ConsoleRunOptions { ClipboardOperationTimeout = timeout };

        var terminal = options.ToTerminalOptions(Ansi());

        terminal.ClipboardOperationTimeout.ShouldBe(timeout);
    }

    /// <summary>Verifies an unbounded or non-positive clipboard deadline is rejected before the
    /// console is opened.</summary>
    [Fact]
    public void ClipboardOperationTimeout_WhenValueIsNotPositiveAndFinite_ThrowsArgumentOutOfRangeException()
    {
        var zero = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ConsoleRunOptions { ClipboardOperationTimeout = TimeSpan.Zero });
        var infinite = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ConsoleRunOptions { ClipboardOperationTimeout = Timeout.InfiniteTimeSpan });

        zero.ParamName.ShouldBe("value");
        infinite.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies opting into Ctrl+C as input maps to host control-key capture.</summary>
    [Fact]
    public void ToHostOptions_WhenControlCAsInput_CapturesControlKeys()
    {
        var options = new ConsoleRunOptions { TreatControlCAsInput = true };

        options.ToHostOptions().CaptureControlKeys.ShouldBeTrue();
    }

    /// <summary>Verifies mouse tracking being configured maps to host mouse-input enablement.</summary>
    [Fact]
    public void ToHostOptions_WhenMouseTrackingConfigured_EnablesMouseInput()
    {
        var options = new ConsoleRunOptions { MouseTracking = MouseTracking.Any };

        options.ToHostOptions().EnableMouseInput.ShouldBeTrue();
    }

    /// <summary>Verifies mouse tracking disabled maps to host mouse-input disablement.</summary>
    [Fact]
    public void ToHostOptions_WhenMouseTrackingDisabled_DisablesMouseInput()
    {
        var options = new ConsoleRunOptions { MouseTracking = null };

        options.ToHostOptions().EnableMouseInput.ShouldBeFalse();
    }

    /// <summary>Verifies a null theme resolves to the standard dark theme.</summary>
    [Fact]
    public void ResolveTheme_WhenThemeNull_ReturnsDark() =>
        new ConsoleRunOptions().ResolveTheme().ShouldBe(ThemeCatalog.Dark);

    /// <summary>Verifies a forced color depth is threaded into negotiation overrides so it survives renegotiation.</summary>
    [Fact]
    public void ToTerminalOptions_WhenColorDepthForced_ThreadsItIntoNegotiationOverrides()
    {
        var options = new ConsoleRunOptions { ColorDepth = ColorDepth.Monochrome };

        var terminal = options.ToTerminalOptions(Ansi());

        terminal.Negotiation.ShouldNotBeNull().Overrides.ShouldNotBeNull().ColorDepth.ShouldBe(ColorDepth.Monochrome);
    }

    /// <summary>Verifies the default (unforced) color depth leaves the negotiation override null.</summary>
    [Fact]
    public void ToTerminalOptions_WhenColorDepthDefault_LeavesNegotiationOverrideNull()
    {
        var terminal = new ConsoleRunOptions().ToTerminalOptions(Ansi());

        terminal.Negotiation.ShouldNotBeNull().Overrides.ShouldNotBeNull().ColorDepth.ShouldBeNull();
    }

    /// <summary>Verifies parameterless mapping gives a complete profile precedence over compatibility capabilities.</summary>
    [Fact]
    public void ToTerminalOptions_WhenParameterlessAndProfileExists_PrefersProfile()
    {
        var profile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });
        var options = new ConsoleRunOptions
        {
            Profile = profile,
            Capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Basic16 }
        };

        var terminal = options.ToTerminalOptions();

        terminal.Profile.ShouldBeSameAs(profile);
        terminal.Negotiation.ShouldBeNull();
    }

    /// <summary>Verifies parameterless compatibility capabilities are wrapped in a usable ANSI profile.</summary>
    [Fact]
    public void ToTerminalOptions_WhenParameterlessAndCapabilitiesExist_WrapsExactCapabilities()
    {
        var database = new Feature(
            CapabilitySupport.Supported,
            Origin.Database);
        var capabilities = TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.Basic16,
            Osc52 = database
        };
        var options = new ConsoleRunOptions { Capabilities = capabilities };

        var terminal = options.ToTerminalOptions();

        terminal.Profile.Description.Name.ShouldBe("ansi");
        terminal.Profile.Capabilities.ShouldBeSameAs(capabilities);
        terminal.Profile.Capabilities.Osc52.ShouldBe(database);
        terminal.Negotiation.ShouldBeNull();
    }

    /// <summary>Verifies pinning compatibility capabilities to avoid probing discards the rest of a
    /// caller-supplied Negotiation but still preserves its multiplexer routing policy, so pinned
    /// hosts cannot have graphics silently leak unwrapped around an approved tmux/screen passthrough.</summary>
    [Fact]
    public void ToTerminalOptions_WhenCapabilitiesArePinned_PreservesMultiplexingFromNegotiation()
    {
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));
        var options = new ConsoleRunOptions
        {
            Capabilities = TerminalCapabilities.Conservative,
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                overrides: null,
                limits: null,
                multiplexing: policy)
        };

        var terminal = options.ToTerminalOptions();

        terminal.Negotiation.ShouldBeNull();
        terminal.Multiplexing.ShouldBeSameAs(policy);
    }

    /// <summary>Verifies pinning without an explicit Negotiation leaves Multiplexing null rather than
    /// fabricating a policy the caller never supplied.</summary>
    [Fact]
    public void ToTerminalOptions_WhenCapabilitiesArePinnedWithoutNegotiation_LeavesMultiplexingNull()
    {
        var options = new ConsoleRunOptions { Capabilities = TerminalCapabilities.Conservative };

        var terminal = options.ToTerminalOptions();

        terminal.Negotiation.ShouldBeNull();
        terminal.Multiplexing.ShouldBeNull();
    }

    /// <summary>Verifies profile, compatibility, negotiation, and color settings have stable precedence.</summary>
    [Fact]
    public void ToTerminalOptions_WhenAllOverridesExist_UsesDeterministicPrecedence()
    {
        var profile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });
        var options = new ConsoleRunOptions
        {
            Profile = profile,
            Capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Basic16 },
            ColorDepth = ColorDepth.Monochrome,
            Negotiation = new NegotiationOptions(new Dictionary<string, string?>())
        };

        var terminal = options.ToTerminalOptions(profile);

        terminal.Profile.Description.ShouldBeSameAs(profile.Description);
        terminal.Profile.Capabilities.ColorDepth.ShouldBe(ColorDepth.Monochrome);
        terminal.Profile.Capabilities.ColorOrigin.ShouldBe(Origin.Override);
        terminal.Negotiation.ShouldBeNull();
    }

    /// <summary>Verifies color override provenance and description retention for every profile-selection path.</summary>
    /// <param name="path">The discovered, explicit-profile, or compatibility-capabilities path.</param>
    [Theory]
    [InlineData("discovered")]
    [InlineData("profile")]
    [InlineData("capabilities")]
    public void ToTerminalOptions_WhenColorDepthForced_RecordsOverrideAndRetainsDescription(
        string path)
    {
        var resolved = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with
            {
                ColorDepth = ColorDepth.Indexed256,
                ColorOrigin = Origin.Database
            });
        var options = path switch
        {
            "discovered" => new ConsoleRunOptions { ColorDepth = ColorDepth.Monochrome },
            "profile" => new ConsoleRunOptions
            {
                Profile = resolved,
                ColorDepth = ColorDepth.Monochrome
            },
            "capabilities" => new ConsoleRunOptions
            {
                Capabilities = resolved.Capabilities,
                ColorDepth = ColorDepth.Monochrome
            },
            _ => throw new ArgumentOutOfRangeException(nameof(path))
        };

        var terminal = options.ToTerminalOptions(resolved);

        terminal.Profile.Capabilities.ColorDepth.ShouldBe(ColorDepth.Monochrome);
        terminal.Profile.Capabilities.ColorOrigin.ShouldBe(Origin.Override);
        terminal.Profile.Description.Name.ShouldBe("ansi");
        terminal.Profile.Description.Suitability.ShouldBe(Suitability.Usable);
    }

    private static TerminalProfile Ansi() =>
        TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
}
