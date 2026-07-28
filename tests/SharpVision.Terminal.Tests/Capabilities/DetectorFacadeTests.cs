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
        _ = Should.Throw<ArgumentNullException>(() => Detector.Detect(null!));
        _ = Should.Throw<ArgumentNullException>(() => Detector.Detect(null!, environment));
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
        var queries = new Queries { FocusReporting = false, KittyClipboard = true };
        var overrides = new Settings { KittyClipboard = false, AmbiguousWidth = Ambiguous.Wide };

        // Act
        var facade = Detector.Detect(baseline, environment, queries, overrides);
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
        var capabilities = Detector.Detect(environment);

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
        var queries = new Queries { KittyClipboard = true };
        var overrides = new Settings { KittyClipboard = false };

        // Act
        var facade = Detector.Detect(environment, queries, overrides);
        var pipeline = DiscoveryPipeline.Default.Detect(
            new DiscoveryContext(TerminalCapabilities.Conservative, environment, queries, overrides));

        // Assert
        facade.ShouldBe(pipeline);
    }
}
