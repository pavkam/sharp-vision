// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

using SharpVision.Terminal.Multiplexing;

/// <summary>Verifies the finite DECRQSS request and typed reply boundary.</summary>
public sealed class DecrqssTests
{
    /// <summary>Verifies every approved selector uses the exact official command.</summary>
    [Theory]
    [InlineData(StatusName.Rendition, "\u001bP$qm\u001b\\")]
    [InlineData(StatusName.CursorStyle, "\u001bP$q q\u001b\\")]
    [InlineData(StatusName.VerticalMargins, "\u001bP$qr\u001b\\")]
    [InlineData(StatusName.HorizontalMargins, "\u001bP$qs\u001b\\")]
    [InlineData(StatusName.ModifyOtherKeys, "\u001bP$q>4m\u001b\\")]
    [InlineData(StatusName.FormatOtherKeys, "\u001bP$q>4f\u001b\\")]
    public void Query_WhenNameIsApproved_WritesExactBytes(StatusName name, string expected)
    {
        var destination = new ArrayBufferWriter<byte>();

        XtermDecrqss.Query(new ProtocolWriter(destination), name);

        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe(expected);
    }

    /// <summary>Verifies a representative reply is equivalent at every transport split.</summary>
    [Fact]
    public void Route_WhenReplyIsFragmented_DeliversTypedStatusAtEverySplit()
    {
        var input = "\u001bP1$r0;4;38:2::1:2:3m\u001b\\"u8.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.StatusResponses.ShouldHaveSingleItem();
            response.Name.ShouldBe(StatusName.Rendition);
            response.Valid.ShouldBeTrue();
            response.Value.ToArray().ShouldBe("0;4;38:2::1:2:3m"u8.ToArray());
            sink.Sequences.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies every approved status and the failure form across every split with adjacent recovery.</summary>
    /// <param name="payload">The returned CSI body.</param>
    /// <param name="valid">Whether the request succeeded.</param>
    /// <param name="expected">The exact recognized identity.</param>
    [Theory]
    [InlineData("0;4;38:2::1:2:3m", true, StatusName.Rendition)]
    [InlineData("2 q", true, StatusName.CursorStyle)]
    [InlineData("1;24r", true, StatusName.VerticalMargins)]
    [InlineData("1;80s", true, StatusName.HorizontalMargins)]
    [InlineData(">4;2m", true, StatusName.ModifyOtherKeys)]
    [InlineData(">4;1f", true, StatusName.FormatOtherKeys)]
    [InlineData("", false, StatusName.Unknown)]
    public void Route_WhenApprovedOrFailedReplyIsFragmented_RecoversAtEverySplit(
        string payload,
        bool valid,
        StatusName expected)
    {
        var prefix = valid ? "\u001bP1$r" : "\u001bP0$r";
        var input = Encoding.ASCII.GetBytes($"{prefix}{payload}\u001b\\x");

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.StatusResponses.ShouldHaveSingleItem();
            response.Valid.ShouldBe(valid, $"split {split}");
            response.Name.ShouldBe(expected, $"split {split}");
            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'), $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies structurally valid selector spoofs remain unknown diagnostics at every split.</summary>
    /// <param name="payload">The spoofed or invalid returned CSI body.</param>
    [Theory]
    [InlineData(">40;2m")]
    [InlineData(">4;2;3m")]
    [InlineData(">4;9m")]
    [InlineData(">4;2f")]
    public void Route_WhenReturnedSelectorIsSpoofed_ReportsUnknownAtEverySplit(string payload)
    {
        var input = Encoding.ASCII.GetBytes($"\u001bP1$r{payload}\u001b\\x");

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            var response = sink.StatusResponses.ShouldHaveSingleItem();
            response.Name.ShouldBe(StatusName.Unknown, $"split {split}");
            response.Valid.ShouldBeTrue($"split {split}");
            sink.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(
                DiagnosticCode.Unsupported,
                $"split {split}");
            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'), $"split {split}");
        }
    }

    /// <summary>Verifies malformed returned CSI grammar stays raw and adjacent input recovers at every split.</summary>
    [Fact]
    public void Route_WhenReturnedCsiBodyIsMalformed_RejectsTypedValueAndRecoversAtEverySplit()
    {
        var input = "\u001bP1$r>4;xm\u001b\\z"u8.ToArray();

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(input.AsSpan(0, split));
            router.Route(input.AsSpan(split));

            sink.StatusResponses.ShouldBeEmpty($"split {split}");
            sink.Sequences.ShouldHaveSingleItem($"split {split}").Kind.ShouldBe(
                SequenceKind.Dcs,
                $"split {split}");
            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('z'), $"split {split}");
        }
    }

    /// <summary>Verifies unknown valid status remains typed and diagnostic while following input recovers.</summary>
    [Fact]
    public void Route_WhenStatusIsUnknown_ObservesDiagnosticAndRecovers()
    {
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route("\u001bP1$r?999h\u001b\\x"u8);

        sink.StatusResponses.ShouldHaveSingleItem().Name.ShouldBe(StatusName.Unknown);
        sink.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCode.Unsupported);
        sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>Verifies a representative tmux-wrapped reply reaches the same typed router seam.</summary>
    [Fact]
    public void Route_WhenReplyWasWrappedByTmux_UnwrapsBeforeTypedRouting()
    {
        var wrapped = new ArrayBufferWriter<byte>();
        TmuxWriter.WritePassthrough(wrapped, "\u001bP1$r>4;2m\u001b\\"u8);
        var outerPayload = wrapped.WrittenSpan[2..^2];
        var inner = new ArrayBufferWriter<byte>();
        TmuxWriter.TryUnwrap(outerPayload, inner).ShouldBeTrue();
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink);

        router.Route(inner.WrittenSpan);

        sink.StatusResponses.ShouldHaveSingleItem().Name.ShouldBe(StatusName.ModifyOtherKeys);
    }
}
