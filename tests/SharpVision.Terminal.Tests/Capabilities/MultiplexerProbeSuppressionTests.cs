// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Discovery.Queries;

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
