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
    /// writing them would only spend a round trip tmux cannot carry (see #249).
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
    /// in <c>EnvironmentEvidenceAdapter</c>, so it must still be emitted (see #249).
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
    /// reason (see #249).
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
    /// Verifies a suppressed probe still publishes Unsupported/Origin.Environment rather than
    /// sitting at Unknown, so callers see the same conclusion the probe would have proven, sourced
    /// honestly (see #249).
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
