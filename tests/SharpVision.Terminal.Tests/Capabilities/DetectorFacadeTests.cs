// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Discovery;

/// <summary>Verifies the compatibility detector facade preserves discovery behavior and validation.</summary>
public sealed class DetectorFacadeTests
{
    /// <summary>Verifies public and description-baseline facade calls reject their required inputs.</summary>
    [Fact]
    public void Detect_WhenRequiredInputIsNull_Throws()
    {
        // Arrange
        var environment = new Dictionary<string, string?>();

        // Act / Assert
        _ = Should.Throw<ArgumentNullException>(() => CapabilityDetector.Detect(null!));
        _ = Should.Throw<ArgumentNullException>(() => CapabilityDetector.Detect(null!, environment));
    }

    /// <summary>Verifies the facade delegates the exact fixed pipeline evidence result.</summary>
    [Fact]
    public void Detect_WhenEvidenceIsProvided_MatchesDiscoveryPipeline()
    {
        // Arrange
        var baseline = TerminalCapabilities.Conservative with
        {
            FocusReporting = new Feature(CapabilitySupport.Supported, Origin.Database)
        };
        var environment = new Dictionary<string, string?>
        {
            ["TERM"] = "xterm-kitty",
            ["COLORTERM"] = "truecolor"
        };
        var queries = new QueryResults { FocusReporting = false, KittyClipboard = true };
        var overrides = new CapabilityOverrides { KittyClipboard = false, AmbiguousWidth = Ambiguous.Wide };

        // Act
        var facade = CapabilityDetector.Detect(baseline, environment, queries, overrides);
        var pipeline = DiscoveryPipeline.Default.Detect(
            new DiscoveryContext(baseline, environment, queries, overrides));

        // Assert
        facade.ShouldBe(pipeline);
    }

    /// <summary>Verifies case-insensitive caller environment lookup remains compatible with the previous facade.</summary>
    [Fact]
    public void Detect_WhenCaseInsensitiveEnvironmentUsesLowerCaseCanonicalKeys_PreservesLegacyEvidence()
    {
        // Arrange
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["term"] = "xterm-kitty",
            ["colorterm"] = "truecolor",
            ["term_program"] = "iTerm.app",
            ["tmux"] = "/tmp/tmux-1000/default,1,0",
            ["ssh_connection"] = "client server",
            ["ssh_tty"] = "/dev/pts/1"
        };

        // Act
        var capabilities = CapabilityDetector.Detect(environment);

        // Assert
        capabilities.KittyKeyboard.ShouldBe(new Feature(CapabilitySupport.Tentative, Origin.Environment));
        capabilities.ColorDepth.ShouldBe(ColorDepth.TrueColor);
        capabilities.ColorOrigin.ShouldBe(Origin.Environment);
        capabilities.ItermImages.ShouldBe(new Feature(CapabilitySupport.Unsupported, Origin.Environment));
        capabilities.KittyGraphics.ShouldBe(new Feature(CapabilitySupport.Unsupported, Origin.Environment));
        capabilities.KittyClipboard.ShouldBe(new Feature(CapabilitySupport.Unsupported, Origin.Environment));
        capabilities.Osc52.ShouldBe(Feature.Unknown);
    }

    /// <summary>Verifies the public facade applies the conservative baseline through the discovery pipeline.</summary>
    [Fact]
    public void Detect_WhenPublicEvidenceIsProvided_MatchesDiscoveryPipeline()
    {
        // Arrange
        var environment = new Dictionary<string, string?>
        {
            ["TERM"] = "xterm-kitty"
        };
        var queries = new QueryResults { KittyClipboard = true };
        var overrides = new CapabilityOverrides { KittyClipboard = false };

        // Act
        var facade = CapabilityDetector.Detect(environment, queries, overrides);
        var pipeline = DiscoveryPipeline.Default.Detect(
            new DiscoveryContext(TerminalCapabilities.Conservative, environment, queries, overrides));

        // Assert
        facade.ShouldBe(pipeline);
    }

    /// <summary>
    /// Verifies TERM_PROGRAM_VERSION below iTerm2 3.5 withholds query-origin FILE evidence,
    /// downgrading it to Unsupported instead of leaving it Supported. This proves the
    /// corroborator can narrow — the multipart form this library emits did not exist yet, so a
    /// bare Capabilities FILE=true reply from an older build cannot be trusted alone.
    /// </summary>
    [Theory]
    [InlineData("3.4.0")]
    [InlineData("3.4.99")]
    public void Detect_WhenTermProgramVersionPredatesMultipart_NarrowsItermImagesToUnsupported(
        string version)
    {
        var environment = new Dictionary<string, string?>
        {
            ["TERM_PROGRAM"] = "iTerm.app",
            ["TERM_PROGRAM_VERSION"] = version
        };
        var queries = new QueryResults { ItermImages = true };

        var capabilities = CapabilityDetector.Detect(environment, queries);

        capabilities.ItermImages.ShouldBe(new Feature(CapabilitySupport.Unsupported, Origin.Query));
    }

    /// <summary>
    /// Verifies TERM_PROGRAM_VERSION at or above iTerm2 3.5, or absent, or unparseable, never
    /// itself grants ItermImages support -- it only narrows Supported evidence, so Query evidence
    /// standing alone still reaches Supported in every one of these cases.
    /// </summary>
    [Theory]
    [InlineData("3.5.0")]
    [InlineData("4.0.0")]
    [InlineData("not-a-version")]
    [InlineData(null)]
    public void Detect_WhenTermProgramVersionDoesNotPredateMultipart_LeavesQueryEvidenceSupported(
        string? version)
    {
        var environment = new Dictionary<string, string?>
        {
            ["TERM_PROGRAM"] = "iTerm.app",
            ["TERM_PROGRAM_VERSION"] = version
        };
        var queries = new QueryResults { ItermImages = true };

        var capabilities = CapabilityDetector.Detect(environment, queries);

        capabilities.ItermImages.ShouldBe(new Feature(CapabilitySupport.Supported, Origin.Query));
    }

    /// <summary>
    /// Verifies the version narrowing cannot itself grant support: a below-3.5 version alone,
    /// without positive query or override evidence, does not promote ItermImages past Unknown.
    /// </summary>
    [Fact]
    public void Detect_WhenTermProgramVersionPredatesMultipartWithoutPositiveEvidence_StaysUnresolved()
    {
        var environment = new Dictionary<string, string?>
        {
            ["TERM_PROGRAM_VERSION"] = "3.4.0"
        };

        var capabilities = CapabilityDetector.Detect(environment);

        capabilities.ItermImages.State.ShouldBe(CapabilitySupport.Unknown);
    }
}
