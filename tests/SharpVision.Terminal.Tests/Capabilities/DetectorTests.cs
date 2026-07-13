namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

using Shouldly;

using CapabilitySupport = Terminal.Capabilities.Support;

/// <summary>
/// Verifies capability evidence precedence and safe narrowing.
/// </summary>
public sealed class DetectorTests
{
    /// <summary>
    /// Verifies terminal-name hints remain tentative rather than proven support.
    /// </summary>
    [Fact]
    public void Detect_WhenKittyEnvironmentIsPresent_RecordsTentativeFeatures()
    {
        var environment = new Dictionary<string, string?>
        {
            ["TERM"] = "xterm-kitty",
            ["COLORTERM"] = "truecolor",
        };

        var capabilities = Detector.Detect(environment);

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
        var environment = new Dictionary<string, string?>
        {
            ["TERM"] = "xterm-kitty",
            ["TMUX"] = "/tmp/tmux-1000/default,1,0",
        };
        var queries = new Queries { KittyClipboard = true };

        var capabilities = Detector.Detect(environment, queries);

        capabilities.KittyClipboard.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.KittyGraphics.State.ShouldBe(CapabilitySupport.Unsupported);
    }

    /// <summary>
    /// Verifies SSH and screen prevent unsafe clipboard assumptions.
    /// </summary>
    [Fact]
    public void Detect_WhenSessionIsRemoteOrScreen_NarrowsClipboardHints()
    {
        var environment = new Dictionary<string, string?>
        {
            ["TERM"] = "screen-256color",
            ["SSH_CONNECTION"] = "client server",
        };

        var capabilities = Detector.Detect(environment);

        capabilities.Osc52.State.ShouldBe(CapabilitySupport.Unknown);
        capabilities.KittyClipboard.State.ShouldBe(CapabilitySupport.Unsupported);
        capabilities.ColorDepth.ShouldBe(ColorDepth.Indexed256);
    }

    /// <summary>
    /// Verifies explicit caller overrides always win over hints and queries.
    /// </summary>
    [Fact]
    public void Detect_WhenOverridesAreProvided_OverridesWinLast()
    {
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" };
        var queries = new Queries
        {
            KittyClipboard = true,
            SynchronizedOutput = true,
            StyledUnderlines = true,
            UnderlineColor = false,
            Overline = false,
        };
        var overrides = new Settings
        {
            KittyClipboard = false,
            SynchronizedOutput = false,
            Osc52 = true,
            ColorDepth = ColorDepth.Monochrome,
            StyledUnderlines = false,
            UnderlineColor = true,
            Overline = true,
        };

        var capabilities = Detector.Detect(environment, queries, overrides);

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
    }
}
