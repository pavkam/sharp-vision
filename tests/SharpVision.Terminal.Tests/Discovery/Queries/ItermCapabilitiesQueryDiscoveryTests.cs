// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Discovery.Queries;

using Capabilities;

using SharpVision.Terminal.Capabilities;

using SharpVision.Terminal.Discovery.Queries;

/// <summary>Proves bounded iTerm2 OSC 1337 Capabilities feature-reporting negotiation.</summary>
public sealed class ItermCapabilitiesQueryDiscoveryTests
{
    /// <summary>Verifies the stable query-kind ABI appends the iTerm2 family without renumbering prior families.</summary>
    [Fact]
    public void QueryKind_WhenItermCapabilitiesIsAdded_PreservesExistingNumericValues()
    {
        ((int) QueryKind.KittyGraphics).ShouldBe(15);
        ((int) QueryKind.ItermCapabilities).ShouldBe(16);
    }

    /// <summary>Verifies the official Capabilities query is emitted when iTerm2 baseline evidence is unresolved.</summary>
    [Fact]
    public void TryStart_WhenItermImagesIsUnresolved_EmitsCapabilitiesQuery()
    {
        var negotiator = Create();
        var output = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(output, null, null);

        Encoding.ASCII.GetString(output.WrittenSpan).ShouldContain("]1337;Capabilities\u001b\\");
    }

    /// <summary>Verifies the same gate as every other bounded query family: no baseline override skips probing.</summary>
    [Fact]
    public void TryStart_WhenItermImagesOverrideIsDisabled_DoesNotEmitQuery()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?>(),
            new Settings { ItermImages = false });
        var negotiator = new ActiveQueryDiscoveryStrategy(options);
        var output = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(output, null, null);
        _ = negotiator.Complete();

        Encoding.ASCII.GetString(output.WrittenSpan).ShouldNotContain("Capabilities");
        negotiator.Capabilities.ItermImages.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
    }

    /// <summary>Verifies a reply carrying the FILE code proves query-origin support.</summary>
    [Fact]
    public void Accept_WhenReplyContainsFileCode_RecordsQuerySupport()
    {
        var negotiator = Create();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        _ = XtermResponses.TryOscItermCapabilities("1337;Capabilities=U9MFH"u8, out var response);

        negotiator.Accept(response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Results.ItermImages.ShouldBe(true);
        negotiator.Capabilities.ItermImages.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
    }

    /// <summary>Verifies a reply without the FILE code proves query-origin absence.</summary>
    [Fact]
    public void Accept_WhenReplyOmitsFileCode_RecordsQueryUnsupported()
    {
        var negotiator = Create();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        _ = XtermResponses.TryOscItermCapabilities("1337;Capabilities=U9M"u8, out var response);

        negotiator.Accept(response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Results.ItermImages.ShouldBe(false);
        negotiator.Capabilities.ItermImages.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Query));
    }

    /// <summary>Verifies a silent terminal resolves to false instead of sitting at Unknown, since the
    /// Capabilities reply has no natural piggyback response to expire against.</summary>
    [Fact]
    public void Expire_WhenTerminalNeverReplies_ResolvesItermImagesToFalse()
    {
        var clock = new ManualTimeProvider();
        var limits = QueryLimits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var negotiator = Create(limits, clock);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);

        clock.Advance(TimeSpan.FromSeconds(1));
        negotiator.Expire().ShouldBeTrue();

        negotiator.Results.ItermImages.ShouldBe(false);
        negotiator.Capabilities.ItermImages.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Query));
    }

    /// <summary>Verifies a late reply after expiration cannot mutate the already-published profile.</summary>
    [Fact]
    public void Accept_WhenReplyArrivesAfterExpiry_IsLateAndDoesNotMutate()
    {
        var clock = new ManualTimeProvider();
        var limits = QueryLimits.Default with { QueryTimeout = TimeSpan.FromSeconds(1) };
        var negotiator = Create(limits, clock);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        clock.Advance(TimeSpan.FromSeconds(1));
        _ = negotiator.Expire();
        var published = negotiator.Capabilities;
        _ = XtermResponses.TryOscItermCapabilities("1337;Capabilities=F"u8, out var response);

        negotiator.Accept(response).ShouldBe(QueryMatch.Late);

        negotiator.Capabilities.ShouldBeSameAs(published);
    }

    private static ActiveQueryDiscoveryStrategy Create(
        QueryLimits? limits = null,
        TimeProvider? timeProvider = null) => new(
        new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM_PROGRAM"] = "iTerm.app" },
            limits: limits),
        timeProvider);
}
