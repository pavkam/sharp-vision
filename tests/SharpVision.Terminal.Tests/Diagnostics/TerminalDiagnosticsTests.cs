// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Diagnostics;

using SharpVision.Terminal.Diagnostics;
using SharpVision.Terminal.Kitty.Keyboard;

/// <summary>Verifies immutable, redacted terminal runtime diagnostic snapshots.</summary>
public sealed class TerminalDiagnosticsTests
{
    /// <summary>Verifies caller-owned identity evidence cannot mutate a published snapshot.</summary>
    [Fact]
    public void Constructor_WhenCallerMutatesEvidence_PreservesOwnedSnapshot()
    {
        // Arrange
        var evidence = new[]
        {
            new TerminalBackendEvidence(
                TerminalBackendFamily.Kitty,
                TerminalBackendEvidenceSource.Environment)
        };
        var extensions = new[]
        {
            TerminalProtocolExtension.Vt,
            TerminalProtocolExtension.Xterm,
            TerminalProtocolExtension.Kitty
        };
        var route = new TerminalRouteDiagnostics(policy: null);
        var modes = new TerminalModeDiagnostics(TerminalOptions.Minimal, TerminalCapabilities.Conservative);

        // Act
        var diagnostics = new TerminalDiagnostics(
            TerminalBackendFamily.Kitty,
            "Kitty",
            evidence,
            extensions,
            TerminalNegotiationState.Disabled,
            queryResults: null,
            route,
            modes,
            TerminalGraphicsBackend.CellFallback);
        evidence[0] = new TerminalBackendEvidence(
            TerminalBackendFamily.Vt,
            TerminalBackendEvidenceSource.Description);
        extensions[0] = TerminalProtocolExtension.Iterm2;

        // Assert
        diagnostics.BackendFamily.ShouldBe(TerminalBackendFamily.Kitty);
        diagnostics.BackendName.ShouldBe("Kitty");
        diagnostics.BackendEvidence.ShouldBe(
        [
            new TerminalBackendEvidence(
                TerminalBackendFamily.Kitty,
                TerminalBackendEvidenceSource.Environment)
        ]);
        diagnostics.BackendExtensions.ShouldBe(
        [
            TerminalProtocolExtension.Vt,
            TerminalProtocolExtension.Xterm,
            TerminalProtocolExtension.Kitty
        ]);
    }

    /// <summary>Verifies detected tmux is reported without inventing outer routing authorization.</summary>
    [Fact]
    public void Constructor_WhenTmuxIsOnlyDetected_ReportsBlockedEffectiveRoutes()
    {
        // Arrange
        var policy = MultiplexingPolicy.Detect(new Dictionary<string, string?>
        {
            ["TERM"] = "tmux-256color",
            ["TMUX"] = "/redacted/socket,123,0"
        });

        // Act
        var route = new TerminalRouteDiagnostics(policy);

        // Assert
        route.Layers.ShouldBe([MultiplexerKind.Tmux]);
        route.OuterProfile.ShouldBeNull();
        route.IsActive.ShouldBeFalse();
        route.CanRouteCapabilityQueries.ShouldBeFalse();
        route.CanRouteClipboard.ShouldBeFalse();
        route.CanRouteGraphics.ShouldBeFalse();
        route.SupportsStringTerminatedQueries.ShouldBeFalse();
    }

    /// <summary>Verifies effective optional modes follow capability authority and keyboard fallback order.</summary>
    [Fact]
    public void Constructor_WhenOptionalModesAreConfigured_ReportsOnlyPermittedModesAsEnabled()
    {
        // Arrange
        var capabilities = TerminalCapabilities.Conservative with
        {
            FocusReporting = new Feature(CapabilitySupport.Supported, Origin.Query),
            BracketedPaste = new Feature(CapabilitySupport.Unsupported, Origin.Query),
            CellMouse = new Feature(CapabilitySupport.Supported, Origin.Override),
            KittyKeyboard = new Feature(CapabilitySupport.Supported, Origin.Query),
            XtermKeyboard = new Feature(CapabilitySupport.Supported, Origin.Query),
            KittyClipboard = new Feature(CapabilitySupport.Unsupported, Origin.Query)
        };
        var options = TerminalOptions.Minimal with
        {
            Focus = true,
            Paste = true,
            Tracking = MouseTracking.Any,
            Coordinates = MouseCoordinates.Sgr,
            Keyboard = KittyKeyboardEnhancement.Disambiguate,
            ModifyOtherKeys = 2,
            ClipboardPasteEvents = true
        };

        // Act
        var modes = new TerminalModeDiagnostics(options, capabilities);

        // Assert
        modes.FocusReportingConfigured.ShouldBeTrue();
        modes.FocusReportingAuthorized.ShouldBeTrue();
        modes.FocusReportingActive.ShouldBeFalse();
        modes.BracketedPasteConfigured.ShouldBeTrue();
        modes.BracketedPasteAuthorized.ShouldBeFalse();
        modes.BracketedPasteActive.ShouldBeFalse();
        modes.MouseTracking.ShouldBe(MouseTracking.Any);
        modes.MouseAuthorized.ShouldBeTrue();
        modes.MouseActive.ShouldBeFalse();
        modes.KittyKeyboardEnhancements.ShouldBe(KittyKeyboardEnhancement.Disambiguate);
        modes.KittyKeyboardAuthorized.ShouldBeTrue();
        modes.KittyKeyboardActive.ShouldBeFalse();
        modes.ModifyOtherKeysLevel.ShouldBe(2);
        modes.ModifyOtherKeysAuthorized.ShouldBeFalse();
        modes.ModifyOtherKeysActive.ShouldBeFalse();
        modes.ClipboardPasteEventsConfigured.ShouldBeTrue();
        modes.ClipboardPasteEventsAuthorized.ShouldBeFalse();
        modes.ClipboardPasteEventsActive.ShouldBeFalse();
    }

    /// <summary>Verifies refinements preserve fixed identity while replacing only evolving runtime facts.</summary>
    [Fact]
    public void WithMethods_WhenRuntimeEvidenceChanges_PreserveFixedIdentity()
    {
        // Arrange
        var evidence = new[]
        {
            new TerminalBackendEvidence(
                TerminalBackendFamily.Iterm2,
                TerminalBackendEvidenceSource.Environment)
        };
        var diagnostics = new TerminalDiagnostics(
            TerminalBackendFamily.Iterm2,
            "iTerm2",
            evidence,
            [TerminalProtocolExtension.Vt, TerminalProtocolExtension.Xterm, TerminalProtocolExtension.Iterm2],
            TerminalNegotiationState.Pending,
            queryResults: null,
            new TerminalRouteDiagnostics(policy: null),
            new TerminalModeDiagnostics(TerminalOptions.Minimal, TerminalCapabilities.Conservative),
            TerminalGraphicsBackend.CellFallback);
        var results = new QueryResults
        {
            FocusReporting = true,
            Sixel = false,
            CapabilityString = new CapabilityResponse(
                isValid: true,
                new Dictionary<CapabilityName, byte[]>
                {
                    [CapabilityName.TerminalName] = "sensitive-terminal-name"u8.ToArray()
                })
        };

        // Act
        var completed = diagnostics.WithNegotiation(
            TerminalNegotiationState.Completed,
            results,
            TerminalCapabilities.Conservative);
        var rendered = completed.WithGraphicsBackend(TerminalGraphicsBackend.NonRetained);

        // Assert
        rendered.BackendFamily.ShouldBe(TerminalBackendFamily.Iterm2);
        rendered.BackendName.ShouldBe("iTerm2");
        rendered.BackendEvidence.ShouldBe(evidence);
        rendered.NegotiationState.ShouldBe(TerminalNegotiationState.Completed);
        rendered.QueryResults.ShouldNotBeNull().CapabilityNames.ShouldBe([CapabilityName.TerminalName]);
        rendered.QueryResults.FocusReporting.ShouldBe(true);
        rendered.QueryResults.Sixel.ShouldBe(false);
        rendered.GraphicsBackend.ShouldBe(TerminalGraphicsBackend.NonRetained);
    }

    /// <summary>Verifies invalid typed values and inconsistent negotiation snapshots are rejected.</summary>
    [Fact]
    public void Boundaries_WhenValuesAreInvalid_ThrowBeforePublication()
    {
        // Arrange
        var route = new TerminalRouteDiagnostics(policy: null);
        var modes = new TerminalModeDiagnostics(TerminalOptions.Minimal, TerminalCapabilities.Conservative);

        // Act
        // Assert
        _ = Should.Throw<ArgumentOutOfRangeException>(InvalidEvidence);
        _ = Should.Throw<ArgumentException>(BlankName);
        _ = Should.Throw<ArgumentException>(InconsistentNegotiation);

        return;

        void InvalidEvidence()
        {
            _ = new TerminalBackendEvidence(
                (TerminalBackendFamily) 99,
                TerminalBackendEvidenceSource.Description);
        }

        void BlankName()
        {
            _ = new TerminalDiagnostics(
                TerminalBackendFamily.Vt,
                " ",
                [],
                [TerminalProtocolExtension.Vt],
                TerminalNegotiationState.Disabled,
                queryResults: null,
                route,
                modes,
                TerminalGraphicsBackend.CellFallback);
        }

        void InconsistentNegotiation()
        {
            _ = new TerminalDiagnostics(
                TerminalBackendFamily.Vt,
                "VT",
                [],
                [TerminalProtocolExtension.Vt],
                TerminalNegotiationState.Pending,
                new TerminalQueryDiagnostics(new QueryResults()),
                route,
                modes,
                TerminalGraphicsBackend.CellFallback);
        }
    }
}
