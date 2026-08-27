// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Discovery;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Discovery;


/// <summary>
/// Verifies capability evidence precedence and safe narrowing.
/// </summary>
public sealed class DiscoveryPipelineTests
{
    /// <summary>Verifies the ordered discovery seam is available to capability callers.</summary>
    [Fact]
    public void DiscoveryPipeline_WhenLoaded_ExposesOrderedDiscoveryTypes()
    {
        // Arrange
        var assembly = typeof(CapabilityDetector).Assembly;

        // Act
        var pipeline = assembly.GetType("SharpVision.Terminal.Discovery.DiscoveryPipeline");
        var context = assembly.GetType("SharpVision.Terminal.Discovery.DiscoveryContext");
        var strategy = assembly.GetType("SharpVision.Terminal.Discovery.IDiscoveryStrategy");

        // Assert
        _ = pipeline.ShouldNotBeNull();
        _ = context.ShouldNotBeNull();
        _ = strategy.ShouldNotBeNull();
    }

    /// <summary>
    /// Verifies terminal-name hints remain tentative rather than proven support.
    /// </summary>
    [Fact]
    public void Detect_WhenKittyEnvironmentIsPresent_RecordsTentativeFeatures()
    {
        var environment = new Dictionary<string, string?>() { ["TERM"] = "xterm-kitty", ["COLORTERM"] = "truecolor" };

        var capabilities = DiscoveryPipeline.Default.Detect(
            new DiscoveryContext(TerminalCapabilities.Conservative, environment));

        capabilities.KittyKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Tentative, Origin.Environment));
        capabilities.KittyClipboard.ShouldBe(
            new Feature(CapabilitySupport.Tentative, Origin.Environment));
        capabilities.StyledUnderlines.ShouldBe(
            new Feature(CapabilitySupport.Tentative, Origin.Environment));
        capabilities.ColorDepth.ShouldBe(ColorDepth.TrueColor);
        capabilities.ColorOrigin.ShouldBe(Origin.Environment);
    }

    /// <summary>
    /// Verifies multiplexers narrow vendor hints until a query proves support.
    /// </summary>
    [Fact]
    public void Detect_WhenTmuxAndQueryArePresent_QueryWinsNarrowing()
    {
        var environment = new Dictionary<string, string?>()
        {
            ["TERM"] = "xterm-kitty",
            ["TMUX"] = "/tmp/tmux-1000/default,1,0"
        };
        var queries = new QueryResults() { KittyClipboard = true };

        var capabilities = DiscoveryPipeline.Default.Detect(
            new DiscoveryContext(TerminalCapabilities.Conservative, environment, queries));

        capabilities.KittyClipboard.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.KittyGraphics.State.ShouldBe(CapabilitySupport.Unsupported);
    }

    /// <summary>
    /// Verifies screen narrows vendor clipboard protocols independently of SSH handling.
    /// </summary>
    [Fact]
    public void Detect_WhenSessionIsRemoteOrScreen_NarrowsClipboardHints()
    {
        var environment = new Dictionary<string, string?>()
        {
            ["TERM"] = "screen-256color",
            ["SSH_CONNECTION"] = "client server"
        };

        var capabilities = DiscoveryPipeline.Default.Detect(
            new DiscoveryContext(TerminalCapabilities.Conservative, environment));

        capabilities.Osc52.State.ShouldBe(CapabilitySupport.Unknown);
        capabilities.KittyClipboard.State.ShouldBe(CapabilitySupport.Unsupported);
        capabilities.ColorDepth.ShouldBe(ColorDepth.Indexed256);
    }

    /// <summary>Verifies SSH detection preserves authoritative Osc52 evidence instead of
    /// discarding it — OSC 52's primary use case is copying to the local clipboard from a
    /// remote SSH session, so terminfo/query/override evidence must outrank the blanket
    /// environment-based guess.</summary>
    [Fact]
    public void Detect_WhenSessionIsRemoteWithAuthoritativeOsc52Evidence_PreservesIt()
    {
        var database = new Feature(CapabilitySupport.Supported, Origin.Database);
        var baseline = TerminalCapabilities.Conservative with { Osc52 = database };
        var environment = new Dictionary<string, string?>()
        {
            ["TERM"] = "xterm-256color",
            ["SSH_CONNECTION"] = "client server"
        };

        var capabilities = DiscoveryPipeline.Default.Detect(
            new DiscoveryContext(baseline, environment));

        capabilities.Osc52.ShouldBe(database);
        capabilities.KittyClipboard.State.ShouldBe(CapabilitySupport.Unknown);
    }

    /// <summary>
    /// Verifies explicit caller overrides always win over hints and queries.
    /// </summary>
    [Fact]
    public void Detect_WhenOverridesAreProvided_OverridesWinLast()
    {
        var environment = new Dictionary<string, string?>() { ["TERM"] = "xterm-kitty" };
        var queries = new QueryResults()
        {
            KittyClipboard = true,
            SynchronizedOutput = true,
            StyledUnderlines = true,
            UnderlineColor = false,
            Overline = false
        };
        var overrides = new CapabilityOverrides()
        {
            KittyClipboard = false,
            SynchronizedOutput = false,
            Osc52 = true,
            ColorDepth = ColorDepth.Monochrome,
            StyledUnderlines = false,
            UnderlineColor = true,
            Overline = true,
            Notifications = true
        };

        var capabilities = DiscoveryPipeline.Default.Detect(
            new DiscoveryContext(TerminalCapabilities.Conservative, environment, queries, overrides));

        capabilities.KittyClipboard.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
        capabilities.SynchronizedOutput.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
        capabilities.Osc52.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Override));
        capabilities.ColorDepth.ShouldBe(ColorDepth.Monochrome);
        capabilities.ColorOrigin.ShouldBe(Origin.Override);
        capabilities.StyledUnderlines.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
        capabilities.UnderlineColor.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Override));
        capabilities.Overline.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Override));
        capabilities.Notifications.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Override));
    }

    /// <summary>Verifies tentative environment hints cannot erase accepted database evidence.</summary>
    [Fact]
    public void Detect_WhenBaselineHasDatabaseEvidence_PreservesItOverHint()
    {
        var database = new Feature(CapabilitySupport.Supported, Origin.Database);
        var baseline = TerminalCapabilities.Conservative with
        {
            FocusReporting = database,
            ColorDepth = ColorDepth.Indexed256,
            ColorOrigin = Origin.Database
        };
        var environment = new Dictionary<string, string?>
        {
            ["TERM"] = "xterm-kitty",
            ["COLORTERM"] = "truecolor"
        };

        var capabilities = DiscoveryPipeline.Default.Detect(new DiscoveryContext(baseline, environment));

        capabilities.FocusReporting.ShouldBe(database);
        capabilities.ColorDepth.ShouldBe(ColorDepth.Indexed256);
        capabilities.ColorOrigin.ShouldBe(Origin.Database);
    }

    /// <summary>Verifies presence of NO_COLOR forces monochrome ahead of color heuristics.</summary>
    [Fact]
    public void Detect_WhenNoColorIsPresent_ForcesMonochrome()
    {
        var environment = new Dictionary<string, string?> { ["NO_COLOR"] = "1" };

        var capabilities = DiscoveryPipeline.Default.Detect(
            new DiscoveryContext(TerminalCapabilities.Conservative, environment));

        capabilities.ColorDepth.ShouldBe(ColorDepth.Monochrome);
        capabilities.ColorOrigin.ShouldBe(Origin.Environment);
    }

    /// <summary>Verifies NO_COLOR forces monochrome by presence alone, even with an empty value,
    /// per the no-color.org convention.</summary>
    [Fact]
    public void Detect_WhenNoColorIsEmptyString_StillForcesMonochrome()
    {
        var environment = new Dictionary<string, string?> { ["NO_COLOR"] = "" };

        var capabilities = DiscoveryPipeline.Default.Detect(
            new DiscoveryContext(TerminalCapabilities.Conservative, environment));

        capabilities.ColorDepth.ShouldBe(ColorDepth.Monochrome);
        capabilities.ColorOrigin.ShouldBe(Origin.Environment);
    }

    /// <summary>Verifies NO_COLOR wins over a simultaneously present COLORTERM hint.</summary>
    [Fact]
    public void Detect_WhenNoColorAndColortermBothPresent_NoColorWins()
    {
        var environment = new Dictionary<string, string?>
        {
            ["NO_COLOR"] = "1",
            ["COLORTERM"] = "truecolor"
        };

        var capabilities = DiscoveryPipeline.Default.Detect(
            new DiscoveryContext(TerminalCapabilities.Conservative, environment));

        capabilities.ColorDepth.ShouldBe(ColorDepth.Monochrome);
        capabilities.ColorOrigin.ShouldBe(Origin.Environment);
    }

    /// <summary>Verifies database color evidence still outranks NO_COLOR, same as every other
    /// environment hint — NO_COLOR is a hint-only hint, not a database or override origin.</summary>
    [Fact]
    public void Detect_WhenBaselineHasDatabaseEvidenceAndNoColorIsPresent_PreservesDatabaseOverHint()
    {
        var baseline = TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.Indexed256,
            ColorOrigin = Origin.Database
        };
        var environment = new Dictionary<string, string?> { ["NO_COLOR"] = "1" };

        var capabilities = DiscoveryPipeline.Default.Detect(new DiscoveryContext(baseline, environment));

        capabilities.ColorDepth.ShouldBe(ColorDepth.Indexed256);
        capabilities.ColorOrigin.ShouldBe(Origin.Database);
    }

    /// <summary>Verifies each phase receives evidence in the fixed database, query, and override precedence order.</summary>
    [Fact]
    public void Detect_WhenDatabaseQueryAndOverrideEvidenceConflict_AppliesFixedPhasePrecedence()
    {
        // Arrange
        var database = new Feature(CapabilitySupport.Supported, Origin.Database);
        var baseline = TerminalCapabilities.Conservative with
        {
            FocusReporting = database,
            KittyClipboard = database
        };
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" };
        var queries = new QueryResults { FocusReporting = false, KittyClipboard = true };
        var overrides = new CapabilityOverrides { KittyClipboard = false };

        // Act
        var pipeline = new DiscoveryPipeline(
        [
            new OverrideDiscoveryStrategy(),
            new QueryDiscoveryStrategy(),
            new EnvironmentDiscoveryStrategy()
        ]);
        var capabilities = pipeline.Detect(
            new DiscoveryContext(baseline, environment, queries, overrides));

        // Assert
        capabilities.FocusReporting.ShouldBe(new Feature(CapabilitySupport.Unsupported, Origin.Query));
        capabilities.KittyClipboard.ShouldBe(new Feature(CapabilitySupport.Unsupported, Origin.Override));
    }

    /// <summary>Verifies the context owns its ordinal environment snapshot before the source can change.</summary>
    [Fact]
    public void Detect_WhenEnvironmentChangesAfterContextCreation_UsesOriginalSnapshot()
    {
        // Arrange
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" };
        var context = new DiscoveryContext(TerminalCapabilities.Conservative, environment);
        environment["TERM"] = "dumb";

        // Act
        var capabilities = DiscoveryPipeline.Default.Detect(context);

        // Assert
        capabilities.KittyKeyboard.ShouldBe(new Feature(CapabilitySupport.Tentative, Origin.Environment));
    }

    /// <summary>Verifies incomplete and duplicate concrete phase sets are rejected during construction.</summary>
    [Fact]
    public void Constructor_WhenPhaseSetIsMissingOrDuplicate_Throws()
    {
        // Arrange / Act / Assert
        _ = Should.Throw<ArgumentException>(() => new DiscoveryPipeline(
        [
            new EnvironmentDiscoveryStrategy(),
            new QueryDiscoveryStrategy()
        ]));
        _ = Should.Throw<ArgumentException>(() => new DiscoveryPipeline(
        [
            new EnvironmentDiscoveryStrategy(),
            new EnvironmentDiscoveryStrategy(),
            new OverrideDiscoveryStrategy()
        ]));
        _ = Should.Throw<ArgumentNullException>(() => new DiscoveryPipeline(
        [
            new EnvironmentDiscoveryStrategy(),
            null!,
            new OverrideDiscoveryStrategy()
        ]));
        _ = Should.Throw<ArgumentException>(() => new DiscoveryPipeline(
        [
            new UndefinedDiscoveryStrategy(),
            new QueryDiscoveryStrategy(),
            new OverrideDiscoveryStrategy()
        ]));
    }

    /// <summary>Verifies pipeline construction and detection reject missing required inputs.</summary>
    [Fact]
    public void ConstructorAndDetect_WhenRequiredInputsAreNull_Throw()
    {
        // Arrange
        var pipeline = DiscoveryPipeline.Default;

        // Act / Assert
        _ = Should.Throw<ArgumentNullException>(() => new DiscoveryPipeline(null!));
        _ = Should.Throw<ArgumentNullException>(() => pipeline.Detect(null!));
    }
}
