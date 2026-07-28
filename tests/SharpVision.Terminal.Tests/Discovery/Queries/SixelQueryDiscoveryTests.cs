// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Discovery.Queries;

using SharpVision.Terminal.Capabilities;

using SharpVision.Terminal.Discovery.Queries;

/// <summary>Proves DA1-correlated sixel evidence and explicit-override precedence.</summary>
public sealed class SixelQueryDiscoveryTests
{
    /// <summary>Verifies DA1 parameter 4 proves sixel support at every transport split.</summary>
    [Fact]
    public void Accept_WhenFragmentedDa1ContainsSixel_RecordsQuerySupportAtEverySplit()
    {
        var wire = "\u001b[?62;4c"u8.ToArray();

        for (var split = 0; split <= wire.Length; split++)
        {
            var negotiator = Create();
            _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(wire.AsSpan(0, split));
            router.Route(wire.AsSpan(split));
            var response = sink.Responses.ShouldHaveSingleItem();

            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched, $"split {split}");
            _ = negotiator.Complete();

            negotiator.Capabilities.Sixel.ShouldBe(
                new Feature(CapabilitySupport.Supported, Origin.Query),
                $"split {split}");
            negotiator.Results.Sixel.ShouldBe(true);
        }
    }

    /// <summary>Verifies a validated DA1 without parameter 4 records unsupported query evidence.</summary>
    [Fact]
    public void Accept_WhenDa1OmitsSixel_RecordsUnsupportedQueryEvidence()
    {
        var negotiator = Create();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var response = Response("?62;1;6"u8);

        negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Capabilities.Sixel.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Query));
        negotiator.Results.Sixel.ShouldBe(false);
    }

    /// <summary>Verifies explicit sixel enablement remains authoritative over a negative DA1 reply.</summary>
    [Fact]
    public void Accept_WhenOverrideEnablesSixel_OverrideWinsNegativeDa1()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm" },
            new Settings { Sixel = true });
        var negotiator = new ActiveQueryDiscoveryStrategy(options);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var response = Response("?62;1;6"u8);

        negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Capabilities.Sixel.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Override));
    }

    /// <summary>Verifies explicit sixel disablement remains authoritative over positive DA1.</summary>
    [Fact]
    public void Accept_WhenOverrideDisablesSixel_OverrideWinsPositiveDa1()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm" },
            new Settings { Sixel = false });
        var negotiator = new ActiveQueryDiscoveryStrategy(options);
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var response = Response("?62;4"u8);

        negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Capabilities.Sixel.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
    }

    private static ActiveQueryDiscoveryStrategy Create() => new(
        new NegotiationOptions(new Dictionary<string, string?>()));

    private static Response Response(ReadOnlySpan<byte> parameters)
    {
        XtermResponses.TryCsi(parameters, [], (byte) 'c', out var response).ShouldBeTrue();
        return response;
    }
}
