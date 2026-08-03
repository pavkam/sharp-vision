// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Discovery.Queries;

using Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Discovery.Queries;

/// <summary>Verifies bounded xterm-specific startup refinements.</summary>
public sealed class XtermQueryDiscoveryTests
{
    /// <summary>Verifies xterm probes append after the standard safe query priority.</summary>
    [Fact]
    public void TryStart_WhenXtermIsHinted_AppendsExactBoundedDcsQueries()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color" },
            limits: QueryLimits.Default with { MaxConcurrentQueries = 18 });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        var destination = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(destination, null, null);

        var bytes = Encoding.ASCII.GetString(destination.WrittenSpan);
        bytes.ShouldEndWith("\u001bP+q524742\u001b\\\u001bP$q>4m\u001b\\");
    }

    /// <summary>Verifies DCS replies match once and retain owned semantic evidence.</summary>
    [Fact]
    public void Accept_WhenDcsRepliesRepeat_ClassifiesDuplicatesWithoutRawProgramMutation()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm" },
            limits: QueryLimits.Default with { MaxConcurrentQueries = 18 });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        XtermDecrqss.TryParse("1"u8, "$"u8, (byte) 'r', ">4;2m"u8, out var status)
            .ShouldBeTrue();
        XtermGetCap.TryParse(
            "1"u8,
            "+"u8,
            (byte) 'r',
            "524742=3234"u8,
            QueryLimits.Default,
            out var capability).ShouldBeTrue();

        negotiator.Accept(in status).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(in status).ShouldBe(QueryMatch.Duplicate);
        negotiator.Accept(capability!).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(capability!).ShouldBe(QueryMatch.Duplicate);
    }

    /// <summary>Verifies a valid response for another selector cannot consume requested work.</summary>
    [Fact]
    public void Accept_WhenDcsSelectorDoesNotMatch_KeepsRequestedFamilyActive()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm" },
            limits: QueryLimits.Default with { MaxConcurrentQueries = 18 });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        XtermDecrqss.TryParse("1"u8, "$"u8, (byte) 'r', "0m"u8, out var otherStatus)
            .ShouldBeTrue();
        XtermDecrqss.TryParse("1"u8, "$"u8, (byte) 'r', ">4;2m"u8, out var requestedStatus)
            .ShouldBeTrue();
        XtermGetCap.TryParse(
            "1"u8,
            "+"u8,
            (byte) 'r',
            "6B63757531=1B5B41"u8,
            QueryLimits.Default,
            out var otherCapability).ShouldBeTrue();
        XtermGetCap.TryParse(
            "1"u8,
            "+"u8,
            (byte) 'r',
            "524742=3234"u8,
            QueryLimits.Default,
            out var requestedCapability).ShouldBeTrue();

        negotiator.Accept(in otherStatus).ShouldBe(QueryMatch.Unknown);
        negotiator.Accept(in requestedStatus).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(otherCapability!).ShouldBe(QueryMatch.Unknown);
        negotiator.Accept(requestedCapability!).ShouldBe(QueryMatch.Matched);
    }

    /// <summary>Verifies validated query evidence refines semantics while overrides remain final.</summary>
    [Fact]
    public void Detect_WhenXtermEvidenceIsValidated_RefinesOnlySemanticValues()
    {
        XtermGetCap.TryParse(
            "1"u8,
            "+"u8,
            (byte) 'r',
            "524742=3234"u8,
            QueryLimits.Default,
            out var capability).ShouldBeTrue();
        var queries = new QueryResults
        {
            XtermKeyboard = true,
            CapabilityString = capability
        };
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm" };

        var detected = Detector.Detect(environment, queries);
        var overridden = Detector.Detect(
            environment,
            queries,
            new Settings { XtermKeyboard = false, ColorDepth = ColorDepth.Basic16 });

        detected.XtermKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        detected.ColorDepth.ShouldBe(ColorDepth.TrueColor);
        detected.ColorOrigin.ShouldBe(Origin.Query);
        overridden.XtermKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
        overridden.ColorDepth.ShouldBe(ColorDepth.Basic16);
        overridden.ColorOrigin.ShouldBe(Origin.Override);
    }

    /// <summary>Verifies query evidence supersedes default and environment-only color heuristics.</summary>
    /// <param name="term">The optional terminal name hint.</param>
    /// <param name="colorTerm">The optional color environment hint.</param>
    /// <param name="heuristicDepth">The expected pre-query heuristic depth.</param>
    /// <param name="heuristicOrigin">The expected pre-query origin.</param>
    [Theory]
    [InlineData("xterm", null, ColorDepth.Basic16, Origin.Default)]
    [InlineData("xterm-256color", null, ColorDepth.Indexed256, Origin.Environment)]
    [InlineData("xterm", "truecolor", ColorDepth.TrueColor, Origin.Environment)]
    public void Detect_WhenRgbQueryIsDirect_OverridesOnlyHeuristicColorEvidence(
        string term,
        string? colorTerm,
        ColorDepth heuristicDepth,
        Origin heuristicOrigin)
    {
        var environment = new Dictionary<string, string?> { ["TERM"] = term };

        if (colorTerm is not null)
        {
            environment["COLORTERM"] = colorTerm;
        }

        var heuristic = Detector.Detect(environment);
        var capability = Capability("524742=3234"u8);
        var detected = Detector.Detect(
            environment,
            new QueryResults { CapabilityString = capability });

        heuristic.ColorDepth.ShouldBe(heuristicDepth);
        heuristic.ColorOrigin.ShouldBe(heuristicOrigin);
        detected.ColorDepth.ShouldBe(ColorDepth.TrueColor);
        detected.ColorOrigin.ShouldBe(Origin.Query);
    }

    /// <summary>Verifies query color refinement cannot replace authoritative evidence or use unrelated values.</summary>
    [Fact]
    public void Detect_WhenColorEvidenceIsAuthoritativeOrNonDirect_PreservesExistingColor()
    {
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm-256color" };
        var negative = Detector.Detect(
            environment,
            new QueryResults { CapabilityString = Capability("524742=30"u8) });
        var unrelated = Detector.Detect(
            environment,
            new QueryResults { CapabilityString = Capability("6B63757531=1B5B41"u8) });
        var database = TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.Indexed256,
            ColorOrigin = Origin.Database
        };
        var authoritative = Detector.Detect(
            database,
            environment,
            new QueryResults { CapabilityString = Capability("524742=3234"u8) });
        var queryBaseline = Detector.Detect(
            TerminalCapabilities.Conservative with
            {
                ColorDepth = ColorDepth.Indexed256,
                ColorOrigin = Origin.Query
            },
            environment,
            new QueryResults { CapabilityString = Capability("524742=3234"u8) });
        var overrideBaseline = Detector.Detect(
            TerminalCapabilities.Conservative with
            {
                ColorDepth = ColorDepth.Basic16,
                ColorOrigin = Origin.Override
            },
            environment,
            new QueryResults { CapabilityString = Capability("524742=3234"u8) });

        negative.ColorDepth.ShouldBe(ColorDepth.Indexed256);
        negative.ColorOrigin.ShouldBe(Origin.Environment);
        unrelated.ColorDepth.ShouldBe(ColorDepth.Indexed256);
        unrelated.ColorOrigin.ShouldBe(Origin.Environment);
        authoritative.ColorDepth.ShouldBe(ColorDepth.Indexed256);
        authoritative.ColorOrigin.ShouldBe(Origin.Database);
        queryBaseline.ColorDepth.ShouldBe(ColorDepth.Indexed256);
        queryBaseline.ColorOrigin.ShouldBe(Origin.Query);
        overrideBaseline.ColorDepth.ShouldBe(ColorDepth.Basic16);
        overrideBaseline.ColorOrigin.ShouldBe(Origin.Override);
    }

    /// <summary>Verifies an explicit color override suppresses RGB bytes and tracker capacity.</summary>
    /// <param name="capacity">The bounded startup capacity.</param>
    /// <param name="expectsStatus">Whether the next-priority status query fits.</param>
    [Theory]
    [InlineData(16, false)]
    [InlineData(17, true)]
    [InlineData(18, true)]
    public void TryStart_WhenColorDepthIsExplicit_DoesNotRegisterRgbQuery(
        int capacity,
        bool expectsStatus)
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color" },
            new Settings { ColorDepth = ColorDepth.Basic16 },
            QueryLimits.Default with { MaxConcurrentQueries = capacity });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        var destination = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(destination, null, null);

        var bytes = Encoding.ASCII.GetString(destination.WrittenSpan);
        bytes.ShouldNotContain("\u001bP+q524742\u001b\\");
        bytes.Contains("\u001bP$q>4m\u001b\\", StringComparison.Ordinal)
            .ShouldBe(expectsStatus);
    }

    /// <summary>Verifies the final slot normally belongs to RGB before the following status query.</summary>
    [Theory]
    [InlineData(17, true, false)]
    [InlineData(18, true, true)]
    public void TryStart_WhenColorDepthIsNotExplicit_PreservesRgbThenStatusPriority(
        int capacity,
        bool expectsRgb,
        bool expectsStatus)
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color" },
            limits: QueryLimits.Default with { MaxConcurrentQueries = capacity });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, new ManualTimeProvider());
        var destination = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(destination, null, null);

        var bytes = Encoding.ASCII.GetString(destination.WrittenSpan);
        bytes.Contains("\u001bP+q524742\u001b\\", StringComparison.Ordinal)
            .ShouldBe(expectsRgb);
        bytes.Contains("\u001bP$q>4m\u001b\\", StringComparison.Ordinal)
            .ShouldBe(expectsStatus);
    }

    /// <summary>Verifies suppressed RGB work neither consumes capacity nor delays publication.</summary>
    [Fact]
    public void Accept_WhenColorDepthIsExplicit_CompletesWithoutRgbReply()
    {
        var database = new Feature(CapabilitySupport.Supported, Origin.Database);
        var baseline = TerminalCapabilities.Conservative with
        {
            KittyKeyboard = database,
            SynchronizedOutput = database,
            FocusReporting = database,
            BracketedPaste = database,
            CellMouse = database,
            PixelMouse = database,
            KittyClipboard = database,
            ItermImages = database
        };
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm" },
            new Settings { ColorDepth = ColorDepth.Basic16 },
            QueryLimits.Default with { MaxConcurrentQueries = 6 });
        var negotiator = new ActiveQueryDiscoveryStrategy(options, baseline, new ManualTimeProvider());
        var destination = new ArrayBufferWriter<byte>();
        _ = negotiator.TryStart(destination, new Size(80, 24), new Size(800, 480));
        XtermDecrqss.TryParse("1"u8, "$"u8, (byte) 'r', ">4;2m"u8, out var status)
            .ShouldBeTrue();

        negotiator.Accept(in status).ShouldBe(QueryMatch.Matched);

        foreach (var palette in new[]
                 {
                     Palette("4;0;rgb:0000/0000/0000"u8),
                     Palette("10;rgb:ffff/ffff/ffff"u8),
                     Palette("11;rgb:0000/0000/0000"u8)
                 })
        {
            negotiator.Accept(in palette).ShouldBe(QueryMatch.Matched);
        }

        var secondary = Numeric(">41;410;0"u8, (byte) 'c');
        negotiator.Accept(in secondary).ShouldBe(QueryMatch.Matched);
        var primary = Numeric("?1;2"u8, (byte) 'c');
        negotiator.Accept(in primary).ShouldBe(QueryMatch.Matched);

        negotiator.IsComplete.ShouldBeTrue();
        negotiator.Results.CapabilityString.ShouldBeNull();
        negotiator.Capabilities.ColorDepth.ShouldBe(ColorDepth.Basic16);
        negotiator.Capabilities.ColorOrigin.ShouldBe(Origin.Override);
    }

    private static CapabilityResponse Capability(ReadOnlySpan<byte> payload)
    {
        XtermGetCap.TryParse(
            "1"u8,
            "+"u8,
            (byte) 'r',
            payload,
            QueryLimits.Default,
            out var capability).ShouldBeTrue();
        return capability!;
    }

    private static Response Numeric(ReadOnlySpan<byte> parameters, byte final)
    {
        XtermResponses.TryCsi(parameters, [], final, out var response).ShouldBeTrue();
        return response;
    }

    private static PaletteResponse Palette(ReadOnlySpan<byte> value)
    {
        XtermResponses.TryOsc(value, out var response).ShouldBeTrue();
        return response;
    }
}
