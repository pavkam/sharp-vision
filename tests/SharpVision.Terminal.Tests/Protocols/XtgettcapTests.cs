// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Multiplexing;

using TerminalInputOptions = InputOptions;

/// <summary>Verifies strict finite XTGETTCAP query and response handling.</summary>
public sealed class XtgettcapTests
{
    /// <summary>Verifies approved names are uppercase-hex encoded exactly.</summary>
    [Fact]
    public void Query_WhenNamesAreApproved_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        CapabilityName[] names = [CapabilityName.DirectColor, CapabilityName.Up];

        XtermGetCap.Query(new ProtocolWriter(destination), names);

        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe(
            "\u001bP+q524742;6B63757531\u001b\\");
    }

    /// <summary>Verifies a representative multi-item reply at every split.</summary>
    [Fact]
    public void Route_WhenReplyIsFragmented_DeliversOwnedValuesAtEverySplit()
    {
        var input = "\u001bP1+r524742=3234;6B63757531=1B5B41\u001b\\"u8.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.CapabilityResponses.ShouldHaveSingleItem();
            response.Valid.ShouldBeTrue();
            response.Items[CapabilityName.DirectColor].ToArray().ShouldBe("24"u8.ToArray());
            response.Items[CapabilityName.Up].ToArray().ShouldBe("\u001b[A"u8.ToArray());
        }
    }

    /// <summary>Verifies unapproved names, duplicate names, odd hex, and value overflow remain raw.</summary>
    [Theory]
    [InlineData("1+r50415448=2F746D70")]
    [InlineData("1+r524742=31;524742=32")]
    [InlineData("1+r524742=F")]
    [InlineData("1+r524742=313233")]
    [InlineData("1+r524742=31;")]
    [InlineData("1+r524742=GG")]
    public void Route_WhenReplyViolatesPolicy_DoesNotPromoteTypedValue(string dcs)
    {
        var options = TerminalInputOptions.Default with
        {
            QueryLimits = QueryLimits.Default with { MaxCapabilityValueBytes = 2 }
        };
        var input = Encoding.ASCII.GetBytes($"\u001bP{dcs}\u001b\\x");

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink, options);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            sink.CapabilityResponses.ShouldBeEmpty($"split {split}");
            sink.Sequences.ShouldHaveSingleItem().Kind.ShouldBe(
                SequenceKind.Dcs,
                $"split {split}");
            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'), $"split {split}");
        }
    }

    /// <summary>Verifies exact item and value bounds accept the limit and reject limit plus one.</summary>
    [Fact]
    public void Route_WhenSpecificBoundsAreReached_EnforcesExactBoundary()
    {
        var limits = QueryLimits.Default with
        {
            MaxCapabilityItems = 1,
            MaxCapabilityValueBytes = 2
        };
        var accepted = new RecordingProtocolSink();
        using (var router = new ProtocolRouter(
                   accepted,
                   TerminalInputOptions.Default with { QueryLimits = limits }))
        {
            router.Route("\u001bP1+r524742=3234\u001b\\"u8);
        }

        accepted.CapabilityResponses.ShouldHaveSingleItem()
            .Items[CapabilityName.DirectColor].Length.ShouldBe(2);

        foreach (var value in new[]
                 {
                     "1+r524742=3234;6B63757531=1B5B41",
                     "1+r524742=323435"
                 })
        {
            var rejected = new RecordingProtocolSink();
            using var router = new ProtocolRouter(
                rejected,
                TerminalInputOptions.Default with { QueryLimits = limits });
            router.Route(Encoding.ASCII.GetBytes($"\u001bP{value}\u001b\\"));
            rejected.CapabilityResponses.ShouldBeEmpty(value);
            _ = rejected.Sequences.ShouldHaveSingleItem();
        }
    }

    /// <summary>Verifies identity-less failure remains typed and adjacent input recovers at every split.</summary>
    [Fact]
    public void Route_WhenCapabilityRequestFails_DeliversFailureAtEverySplit()
    {
        var input = "\u001bP0+r\u001b\\x"u8.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.CapabilityResponses.ShouldHaveSingleItem();
            response.Valid.ShouldBeFalse($"split {split}");
            response.Items.ShouldBeEmpty($"split {split}");
            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'), $"split {split}");
        }
    }

    /// <summary>
    /// Verifies a negative reply may echo the hex-encoded name(s) of the capabilities the
    /// terminal did not find, per xterm's ctlseqs.txt.
    /// </summary>
    [Fact]
    public void Route_WhenCapabilityRequestFailsAndEchoesTheName_DeliversFailureWithItsName()
    {
        var input = "\u001bP0+r524742\u001b\\"u8.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.CapabilityResponses.ShouldHaveSingleItem();
            response.Valid.ShouldBeFalse($"split {split}");
            response.Items.ShouldContainKey(CapabilityName.DirectColor, $"split {split}");
            response.Items[CapabilityName.DirectColor].Length.ShouldBe(0, $"split {split}");
        }
    }

    /// <summary>Verifies a representative tmux-wrapped reply reaches typed XTGETTCAP routing.</summary>
    [Fact]
    public void Route_WhenReplyWasWrappedByTmux_UnwrapsBeforeTypedRouting()
    {
        var wrapped = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(wrapped, "\u001bP1+r524742=3234\u001b\\"u8);
        var inner = new ArrayBufferWriter<byte>();
        TmuxWriter.TryUnwrap(wrapped.WrittenSpan[2..^2], inner).ShouldBeTrue();
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route(inner.WrittenSpan);

        sink.CapabilityResponses.ShouldHaveSingleItem()
            .Items.ShouldContainKey(CapabilityName.DirectColor);
    }
}
