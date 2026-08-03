// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Discovery.Queries;

using SharpVision.Terminal.Capabilities;

using SharpVision.Terminal.Discovery.Queries;

using KittyResponse = Kitty.Graphics.Response;

/// <summary>Proves bounded numeric Kitty graphics capability negotiation.</summary>
public sealed class KittyGraphicsQueryDiscoveryTests
{
    /// <summary>Verifies the stable query-kind ABI appends graphics without renumbering prior families.</summary>
    [Fact]
    public void QueryKind_WhenGraphicsIsAdded_PreservesExistingNumericValues()
    {
        ((int) QueryKind.ModifyOtherKeys).ShouldBe(14);
        ((int) QueryKind.KittyGraphics).ShouldBe(15);
    }

    /// <summary>Verifies graphics correlation accepts only canonical nonzero unsigned decimal IDs.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("4294967296")]
    public void TryRegister_WhenGraphicsIdentifierIsInvalid_RejectsIt(string? value)
    {
        var tracker = new QueryTracker();

        _ = Should.Throw<ArgumentException>(() =>
            tracker.TryRegister(QueryKind.KittyGraphics, value, out _));
    }

    /// <summary>Verifies the official direct-data query is emitted before the DA barrier.</summary>
    [Fact]
    public void TryStart_WhenGraphicsIsUnresolved_EmitsOfficialQueryBeforeDa()
    {
        var negotiator = Create();
        var output = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(output, null, null);

        Encoding.ASCII.GetString(output.WrittenSpan).ShouldStartWith(
            "\u001b[?u\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\\u001b[c");
    }

    /// <summary>Verifies repeated warmed startup always emits the explicit three-zero query pixel.</summary>
    [Fact]
    public void TryStart_WhenRepeated_EmitsStableOfficialZeroPixelQuery()
    {
        for (var iteration = 0; iteration < 256; iteration++)
        {
            var negotiator = Create();
            var output = new ArrayBufferWriter<byte>();

            _ = negotiator.TryStart(output, null, null);

            Encoding.ASCII.GetString(output.WrittenSpan).ShouldContain(
                "\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\");
        }
    }

    /// <summary>Verifies query capacity below the graphics slot suppresses probing exactly at the boundary.</summary>
    /// <param name="capacity">The bounded concurrent query capacity.</param>
    /// <param name="expected">Whether the graphics query is expected.</param>
    [Theory]
    [InlineData(15, false)]
    [InlineData(16, false)]
    [InlineData(17, true)]
    public void TryStart_WhenQueryCapacityCrossesGraphicsSlot_EmitsExpectedProbe(
        int capacity,
        bool expected)
    {
        var negotiator = Create(QueryLimits.Default with { MaxConcurrentQueries = capacity });
        var output = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(output, null, null);

        Encoding.ASCII.GetString(output.WrittenSpan).Contains("\u001b_G").ShouldBe(expected);
    }

    /// <summary>Verifies any valid correlated graphics reply proves support, including an error reply.</summary>
    [Fact]
    public void Accept_WhenGraphicsReplyIsCorrelated_RecordsQuerySupport()
    {
        var negotiator = Create();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var response = KittyResponse.Parse("Gi=31;EINVAL:query inspected"u8);

        negotiator.Accept(response).ShouldBe(QueryMatch.Matched);
        _ = negotiator.Complete();

        negotiator.Results.KittyGraphics.ShouldBe(true);
        negotiator.Capabilities.KittyGraphics.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
    }

    /// <summary>Verifies another renderer identity cannot consume the active query.</summary>
    [Fact]
    public void Accept_WhenGraphicsIdentifierDoesNotMatch_LeavesQueryActive()
    {
        var negotiator = Create();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);

        negotiator.Accept(KittyResponse.Parse("Gi=32;OK"u8)).ShouldBe(QueryMatch.Unknown);
        negotiator.Accept(KittyResponse.Parse("Gi=31;OK"u8)).ShouldBe(QueryMatch.Matched);
    }

    /// <summary>Verifies a leading-zero reply cannot alias the active canonical identifier.</summary>
    [Fact]
    public void Accept_WhenGraphicsIdentifierHasLeadingZero_DoesNotConsumeCanonicalQuery()
    {
        var negotiator = Create();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);

        negotiator.Accept(KittyResponse.Parse("Gi=031;OK"u8)).ShouldBe(QueryMatch.Unknown);
        negotiator.Accept(KittyResponse.Parse("Gi=31;OK"u8)).ShouldBe(QueryMatch.Matched);
    }

    /// <summary>Verifies primary DA acts as the ordered unsupported barrier for an unanswered query.</summary>
    [Fact]
    public void Accept_WhenDaArrivesBeforeGraphicsReply_RecordsUnsupportedQueryEvidence()
    {
        var negotiator = Create();
        _ = negotiator.TryStart(new ArrayBufferWriter<byte>(), null, null);
        var attributes = new Response(ResponseKind.PrimaryAttributes, [1, 2]);

        negotiator.Accept(in attributes).ShouldBe(QueryMatch.Matched);

        _ = negotiator.Complete();
        negotiator.Results.KittyGraphics.ShouldBe(false);
        negotiator.Accept(KittyResponse.Parse("Gi=31;OK"u8)).ShouldBe(QueryMatch.Late);
    }

    /// <summary>Verifies explicit policy suppresses probing and remains authoritative.</summary>
    [Fact]
    public void TryStart_WhenGraphicsOverrideIsDisabled_DoesNotEmitQuery()
    {
        var options = new NegotiationOptions(
            new Dictionary<string, string?>(),
            new CapabilityOverrides { KittyGraphics = false });
        var negotiator = new ActiveQueryDiscoveryStrategy(options);
        var output = new ArrayBufferWriter<byte>();

        _ = negotiator.TryStart(output, null, null);
        _ = negotiator.Complete();

        Encoding.ASCII.GetString(output.WrittenSpan).ShouldNotContain("\u001b_G");
        negotiator.Capabilities.KittyGraphics.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
    }

    private static ActiveQueryDiscoveryStrategy Create(QueryLimits? limits = null) => new(
        new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" },
            limits: limits));
}
