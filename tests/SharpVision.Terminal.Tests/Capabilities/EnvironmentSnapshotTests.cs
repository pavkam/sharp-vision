// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Multiplexing;

/// <summary>
/// Verifies an owned environment snapshot keeps the recognized evidence a caller supplied, whatever
/// key casing and comparer that caller used.
/// </summary>
public sealed class EnvironmentSnapshotTests
{
    /// <summary>Verifies the shared constants match their documented variable names, so the
    /// allowlist and every reader agree by construction rather than by review (see #93).</summary>
    [Fact]
    public void EnvironmentNames_WhenRead_MatchDocumentedVariableNames()
    {
        EnvironmentNames.Term.ShouldBe("TERM");
        EnvironmentNames.ColorTerm.ShouldBe("COLORTERM");
        EnvironmentNames.TermProgram.ShouldBe("TERM_PROGRAM");
        EnvironmentNames.Tmux.ShouldBe("TMUX");
        EnvironmentNames.SshConnection.ShouldBe("SSH_CONNECTION");
        EnvironmentNames.SshTty.ShouldBe("SSH_TTY");
    }

    /// <summary>
    /// Verifies a variable supplied only under its EnvironmentNames constant -- not a
    /// hand-typed literal -- is still recognized and canonicalized, proving the snapshot's
    /// allowlist and this test reference the same symbol rather than two copies that could drift.
    /// </summary>
    [Fact]
    public void Environment_WhenSuppliedUnderTheSharedConstant_IsRecognized()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [EnvironmentNames.Term.ToLowerInvariant()] = "xterm-256color"
        };

        var options = new NegotiationOptions(environment);

        options.Environment[EnvironmentNames.Term].ShouldBe("xterm-256color");
    }

    /// <summary>
    /// Verifies a case-insensitive caller dictionary with lowercase keys is republished under the
    /// canonical spellings that later exact lookups use.
    /// </summary>
    [Fact]
    public void Environment_WhenCallerIsCaseInsensitive_CanonicalizesRecognizedKeys()
    {
        var options = new NegotiationOptions(LowercaseEnvironment());

        options.Environment.ContainsKey("TERM").ShouldBeTrue();
        options.Environment["TERM"].ShouldBe("xterm-256color");
        options.Environment["COLORTERM"].ShouldBe("truecolor");
        options.Environment["TERM_PROGRAM"].ShouldBe("iTerm.app");
        options.Environment["TMUX"].ShouldBe("/tmp/tmux-1000/default,1,0");
    }

    /// <summary>Verifies unrecognized caller entries survive the copy with their original keys.</summary>
    [Fact]
    public void Environment_WhenCallerSuppliesUnknownKeys_PreservesThem()
    {
        var environment = LowercaseEnvironment();
        environment["custom_key"] = "value";

        var options = new NegotiationOptions(environment);

        options.Environment["custom_key"].ShouldBe("value");
    }

    /// <summary>
    /// Verifies the snapshot no longer tracks the caller dictionary, so later caller mutation
    /// cannot change negotiation evidence.
    /// </summary>
    [Fact]
    public void Environment_WhenCallerMutatesAfterwards_KeepsTheOwnedSnapshot()
    {
        var environment = LowercaseEnvironment();
        var options = new NegotiationOptions(environment);

        environment["term"] = "dumb";

        options.Environment["TERM"].ShouldBe("xterm-256color");
    }

    /// <summary>
    /// Verifies multiplexer detection agrees for lowercase and canonical input, which was the
    /// concrete divergence between passive detection and active negotiation.
    /// </summary>
    [Fact]
    public void Multiplexing_WhenCallerIsCaseInsensitive_DetectsTheSameLayerAsCanonicalInput()
    {
        var lowercase = new NegotiationOptions(LowercaseEnvironment());
        var canonical = new NegotiationOptions(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["COLORTERM"] = "truecolor",
            ["TERM_PROGRAM"] = "iTerm.app",
            ["TMUX"] = "/tmp/tmux-1000/default,1,0"
        });

        lowercase.Multiplexing.Layers.ShouldBe(canonical.Multiplexing.Layers);
        lowercase.Multiplexing.Layers.ShouldBe([Kind.Tmux]);
    }

    /// <summary>
    /// Verifies passive detection and the negotiation snapshot agree on true-color evidence for
    /// identical case-insensitive input.
    /// </summary>
    [Fact]
    public void Detector_WhenComparedWithNegotiationSnapshot_AgreesOnEnvironmentEvidence()
    {
        var environment = LowercaseEnvironment();
        var direct = Detector.Detect(TerminalCapabilities.Conservative, environment);
        var snapshot = new NegotiationOptions(environment);

        var throughSnapshot = Detector.Detect(TerminalCapabilities.Conservative, snapshot.Environment);

        throughSnapshot.ColorDepth.ShouldBe(direct.ColorDepth);
        throughSnapshot.Osc52.ShouldBe(direct.Osc52);
        throughSnapshot.KittyKeyboard.ShouldBe(direct.KittyKeyboard);
    }

    private static Dictionary<string, string?> LowercaseEnvironment() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["term"] = "xterm-256color",
            ["colorterm"] = "truecolor",
            ["term_program"] = "iTerm.app",
            ["tmux"] = "/tmp/tmux-1000/default,1,0"
        };
}
