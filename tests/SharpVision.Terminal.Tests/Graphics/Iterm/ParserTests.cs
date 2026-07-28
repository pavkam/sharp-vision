// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Iterm;

using InputOptions = Terminal.Input.Options;

/// <summary>Proves generic bounded OSC 1337 ST/BEL recognition and recovery.</summary>
public sealed class ParserTests
{
    /// <summary>Verifies multipart OSC 1337 is owned at every split for ST and BEL.</summary>
    [Theory]
    [InlineData("\u001b]1337;FileEnd\u001b\\", StringTerminator.EscapeBackslash)]
    [InlineData("\u001b]1337;FileEnd\u0007", StringTerminator.Bell)]
    public void Route_WhenMultipartOscIsFragmented_DeliversOwnedBoundedSequence(
        string value,
        StringTerminator terminator)
    {
        var bytes = Encoding.ASCII.GetBytes(value);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var candidate = bytes.ToArray();
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(candidate.AsSpan(0, split));
            router.Route(candidate.AsSpan(split));
            candidate.AsSpan().Clear();

            var sequence = sink.Sequences.ShouldHaveSingleItem($"split {split}");
            sequence.Kind.ShouldBe(SequenceKind.Osc);
            sequence.Payload.ToArray().ShouldBe("1337;FileEnd"u8.ToArray());
            sequence.Terminator.ShouldBe(terminator);
        }
    }

    /// <summary>Verifies oversized OSC 1337 is discarded and following text remains input.</summary>
    [Fact]
    public void Route_WhenMultipartOscExceedsBound_RecoversFollowingText()
    {
        var sink = new RecordingProtocolSink();
        var options = InputOptions.Default with { Limits = Limits.Default with { MaxStringBytes = 8 } };
        using var router = new ProtocolRouter(sink, options);

        router.Route("\u001b]1337;FileEnd\u001b\\ok"u8);

        sink.Diagnostics.ShouldContain(value =>
            value.Code == DiagnosticCode.StringLimit && value.Kind == SequenceKind.Osc);
        sink.Sequences.ShouldBeEmpty();
        sink.Text.Select(value => value.Value.ToString()).ShouldBe(["o", "k"]);
    }
}
