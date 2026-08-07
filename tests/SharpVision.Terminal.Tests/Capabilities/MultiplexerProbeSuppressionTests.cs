// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Discovery.Queries;
using SharpVision.Terminal.Multiplexing;

/// <summary>
/// Verifies startup probes that a multiplexer would consume are not emitted when that multiplexer
/// cannot carry them.
/// </summary>
/// <remarks>
/// tmux parses a bare APC string as a screen-compatible title, so an unwrapped Kitty graphics probe
/// overwrites the user's pane title and can never be answered.
/// </remarks>
public sealed class MultiplexerProbeSuppressionTests
{
    /// <summary>Verifies a detected but unroutable tmux suppresses the APC graphics probe.</summary>
    [Fact]
    public void TryStart_WhenMultiplexerIsDetectedWithoutPassthrough_OmitsApcProbe()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["TMUX"] = "/tmp/tmux-1000/default,1,0"
        });

        written.ShouldNotContain("\u001b_G");
        written.ShouldContain("\u001b[c");
    }

    /// <summary>Verifies the same probe is still emitted when no multiplexer is present.</summary>
    [Fact]
    public void TryStart_WhenNoMultiplexerIsDetected_EmitsApcProbe()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color"
        });

        written.ShouldContain("\u001b_G");
    }

    /// <summary>
    /// Verifies a lowercase case-insensitive environment suppresses the probe too, so the
    /// canonicalized snapshot and multiplexer detection stay consistent.
    /// </summary>
    [Fact]
    public void TryStart_WhenMultiplexerIsDetectedFromLowercaseKeys_OmitsApcProbe()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["term"] = "xterm-256color",
            ["tmux"] = "/tmp/tmux-1000/default,1,0"
        });

        written.ShouldNotContain("\u001b_G");
    }

    /// <summary>
    /// Verifies a detected but unroutable tmux also suppresses the OSC 1337 iTerm2 probe and the
    /// Kitty clipboard mode probe: environment evidence already narrows both to Unsupported, so
    /// writing them would only spend a round trip tmux cannot carry.
    /// </summary>
    [Fact]
    public void TryStart_WhenMultiplexerIsDetectedWithoutPassthrough_OmitsItermAndClipboardProbes()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["TMUX"] = "/tmp/tmux-1000/default,1,0"
        });

        written.ShouldNotContain("1337;Capabilities");
        written.ShouldNotContain("?5522$p");
        written.ShouldContain("[c");
    }

    /// <summary>
    /// Verifies SSH suppresses only the Kitty clipboard mode probe. OSC 1337 is unaffected by SSH
    /// in <c>EnvironmentEvidenceAdapter</c>, so it must still be emitted.
    /// </summary>
    [Fact]
    public void TryStart_WhenSshIsDetected_OmitsOnlyClipboardProbe()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["SSH_CONNECTION"] = "10.0.0.1 22 10.0.0.2 22"
        });

        written.ShouldNotContain("?5522$p");
        written.ShouldContain("1337;Capabilities");
    }

    /// <summary>
    /// Verifies neither probe is suppressed when no multiplexer or SSH is detected, the negative
    /// control for the two tests above.
    /// </summary>
    [Fact]
    public void TryStart_WhenNoMultiplexerOrSshIsDetected_EmitsBothProbes()
    {
        var written = Start(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color"
        });

        written.ShouldContain("1337;Capabilities");
        written.ShouldContain("?5522$p");
    }

    /// <summary>
    /// Verifies the routed-outer-profile carve-out: when an explicit outer route can carry
    /// capability queries, an inner multiplexer's environment variables must not narrow or
    /// suppress probes, because publication deliberately ignores that environment for the same
    /// reason.
    /// </summary>
    [Fact]
    public void TryStart_WhenRouteCanCarryCapabilityQueries_IgnoresInnerMultiplexerEnvironment()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["TMUX"] = "/tmp/tmux-1000/default,1,0"
        };
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var route = new MultiplexerRoute(policy);

        var started = strategy.TryStart(destination, cells: null, pixels: null, route);

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldContain("1337;Capabilities");
        written.ShouldContain("?5522$p");
    }

    /// <summary>
    /// Verifies the routed-outer-profile carve-out extends to the xterm-proprietary DCS probe
    /// gates: an approved route's own outer terminal identity decides whether the XTGETTCAP RGB
    /// and DECRQSS modifyOtherKeys probes are written, not the inner pane's TERM. tmux's own
    /// defaults (tmux-256color, screen-256color) must not suppress these probes when the outer
    /// terminal is explicitly known to be xterm.
    /// </summary>
    [Fact]
    public void TryStart_WhenRouteHasExplicitXtermOuterProfile_WritesBothDcsProbesRegardlessOfInnerTerm()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "tmux-256color"
        };
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();
        var outerProfile = new TerminalProfile(
            new Description("xterm-256color", DescriptionOrigin.BuiltIn, Suitability.Usable),
            TerminalCapabilities.Conservative);
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            outerProfile,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var route = new MultiplexerRoute(policy);

        var started = strategy.TryStart(destination, cells: null, pixels: null, route);

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldContain("+q524742");
        written.ShouldContain("$q>4m");
    }

    /// <summary>
    /// Verifies the negative of the test above: a declared non-xterm outer profile (here, plain
    /// ANSI) still withholds the xterm-proprietary probes even though the route is approved,
    /// because the outer terminal genuinely is not xterm.
    /// </summary>
    [Fact]
    public void TryStart_WhenRouteHasExplicitNonXtermOuterProfile_WithholdsBothDcsProbes()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color"
        };
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var route = new MultiplexerRoute(policy);

        var started = strategy.TryStart(destination, cells: null, pixels: null, route);

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldNotContain("+q524742");
        written.ShouldNotContain("$q>4m");
    }

    /// <summary>
    /// Verifies the built-in Windows VT connection carve-out: native Windows sessions almost
    /// never set TERM (not under classic conhost, and not under modern Windows Terminal either,
    /// which sets WT_SESSION instead), so the connection's own "windows-vt" description name -
    /// selected only after ENABLE_VIRTUAL_TERMINAL_PROCESSING is confirmed active - is accepted
    /// as an xterm-like hint for the XTGETTCAP RGB and DECRQSS modifyOtherKeys probes instead of
    /// permanently withholding them.
    /// </summary>
    [Fact]
    public void TryStart_WhenDescribedTerminalIsWindowsVtWithNoTerm_WritesBothDcsProbes()
    {
        var options = new NegotiationOptions(new Dictionary<string, string?>(StringComparer.Ordinal));
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();

        var started = strategy.TryStart(
            destination,
            cells: null,
            pixels: null,
            route: null,
            describedTerminalName: "windows-vt");

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldContain("+q524742");
        written.ShouldContain("$q>4m");
    }

    /// <summary>
    /// Verifies the negative of the test above: a described terminal name that is not the
    /// built-in Windows VT connection (and no TERM) still withholds both xterm-proprietary
    /// probes, so the carve-out is exact rather than treating every fallback description as
    /// xterm-compatible.
    /// </summary>
    [Fact]
    public void TryStart_WhenDescribedTerminalIsNotWindowsVtWithNoTerm_WithholdsBothDcsProbes()
    {
        var options = new NegotiationOptions(new Dictionary<string, string?>(StringComparer.Ordinal));
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();

        var started = strategy.TryStart(
            destination,
            cells: null,
            pixels: null,
            route: null,
            describedTerminalName: "ansi");

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldNotContain("+q524742");
        written.ShouldNotContain("$q>4m");
    }

    /// <summary>
    /// Verifies an explicit TERM still wins over the described-terminal fallback: a Windows
    /// connection whose caller-supplied environment happens to set a non-xterm TERM is not
    /// upgraded by the "windows-vt" carve-out, since a present TERM is a stronger, more specific
    /// signal than the generic built-in connection name.
    /// </summary>
    [Fact]
    public void TryStart_WhenTermIsPresentAndNonXterm_WindowsVtCarveOutDoesNotApply()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "dumb"
        };
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();

        var started = strategy.TryStart(
            destination,
            cells: null,
            pixels: null,
            route: null,
            describedTerminalName: "windows-vt");

        started.ShouldBeTrue();
        var written = Encoding.ASCII.GetString(destination.WrittenSpan);
        written.ShouldNotContain("+q524742");
        written.ShouldNotContain("$q>4m");
    }

    /// <summary>
    /// Verifies a suppressed probe still publishes Unsupported/Origin.Environment rather than
    /// sitting at Unknown, so callers see the same conclusion the probe would have proven, sourced
    /// honestly.
    /// </summary>
    [Fact]
    public void TryStart_WhenClipboardProbeIsSuppressed_PublishesUnsupportedEnvironmentEvidence()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["TMUX"] = "/tmp/tmux-1000/default,1,0"
        };
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();

        _ = strategy.TryStart(destination, cells: null, pixels: null, route: null);
        _ = strategy.Complete();

        strategy.Results.KittyClipboard.ShouldBeNull();
        strategy.Capabilities.KittyClipboard.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Environment));
    }

    private static string Start(Dictionary<string, string?> environment)
    {
        var options = new NegotiationOptions(environment);
        var strategy = new ActiveQueryDiscoveryStrategy(
            options,
            TerminalCapabilities.Conservative,
            TimeProvider.System);
        var destination = new ArrayBufferWriter<byte>();

        _ = strategy.TryStart(destination, cells: null, pixels: null, route: null);

        return Encoding.ASCII.GetString(destination.WrittenSpan);
    }
}
