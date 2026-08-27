// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Discovery;

/// <summary>
/// Verifies <see cref="CapabilityDetector"/>: the compatibility detector facade preserves
/// discovery behavior and validation, including Unicode ambiguous-width override precedence.
/// </summary>
public sealed class CapabilityDetectorTests
{
    #region Detector facade

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
    /// Verifies query-origin FILE evidence cannot authorize multipart output even when a version
    /// is available, because the duplicated wire code does not prove which Boolean was reported.
    /// </summary>
    [Theory]
    [InlineData("3.4.0")]
    [InlineData("3.4.99")]
    public void Detect_WhenQueryClaimsItermFileWithOlderVersion_DoesNotAuthorizeMultipartImages(
        string version)
    {
        var environment = new Dictionary<string, string?>
        {
            ["TERM_PROGRAM"] = "iTerm.app",
            ["TERM_PROGRAM_VERSION"] = version
        };
        var queries = new QueryResults { ItermImages = true };

        var capabilities = CapabilityDetector.Detect(environment, queries);

        capabilities.ItermImages.ShouldBe(new Feature(CapabilitySupport.Tentative, Origin.Environment));
    }

    /// <summary>
    /// Verifies TERM_PROGRAM_VERSION at or above iTerm2 3.5, absent, or unparseable cannot turn
    /// ambiguous query evidence into multipart authorization.
    /// </summary>
    [Theory]
    [InlineData("3.5.0")]
    [InlineData("4.0.0")]
    [InlineData("not-a-version")]
    [InlineData(null)]
    public void Detect_WhenQueryClaimsItermFileWithAnyVersion_DoesNotAuthorizeMultipartImages(
        string? version)
    {
        var environment = new Dictionary<string, string?>
        {
            ["TERM_PROGRAM"] = "iTerm.app",
            ["TERM_PROGRAM_VERSION"] = version
        };
        var queries = new QueryResults { ItermImages = true };

        var capabilities = CapabilityDetector.Detect(environment, queries);

        capabilities.ItermImages.ShouldBe(new Feature(CapabilitySupport.Tentative, Origin.Environment));
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

    #endregion

    #region Unicode ambiguous-width override

    /// <summary>
    /// Verifies callers may explicitly select wide ambiguous characters.
    /// </summary>
    [Fact]
    public void Detect_WhenAmbiguousWidthIsOverridden_AppliesOverrideLast()
    {
        var capabilities = CapabilityDetector.Detect(
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color" },
            overrides: new CapabilityOverrides { AmbiguousWidth = Ambiguous.Wide });

        capabilities.AmbiguousWidth.ShouldBe(Ambiguous.Wide);
    }

    #endregion
}
