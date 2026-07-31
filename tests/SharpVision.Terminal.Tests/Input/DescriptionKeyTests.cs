// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

using InputAction = Terminal.Input.Action;

/// <summary>Verifies terminal-description key decoding, precedence, and streaming recovery.</summary>
public sealed class DescriptionKeyTests
{
    /// <summary>Verifies a described CSI spelling overrides the ANSI legacy meaning at every split.</summary>
    [Fact]
    public void Decode_WhenDescriptionChangesCsiMeaning_UsesDescriptionAtEverySplit()
    {
        var sequence = "\u001b[99~"u8.ToArray();
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding(sequence, Code.F63)]),
            useAnsiKeyGrammar: false);

        for (var split = 0; split <= sequence.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            decoder.Decode(sequence.AsSpan(0, split));
            decoder.Decode(sequence.AsSpan(split));
            decoder.Complete();

            sink.Strokes.ShouldBe(
            [
                new Stroke(Code.F63, null, 0, Modifiers.None, InputAction.Press)
            ], $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies typed protocol and enhanced-input grammars retain precedence over a described key.</summary>
    [Theory]
    [InlineData("\u001b[?1;2c")]
    [InlineData("\u001b[?2026;1$y")]
    [InlineData("\u001b[97u")]
    [InlineData("\u001b[I")]
    [InlineData("\u001b[<0;1;1M")]
    public void Decode_WhenDescriptionConflictsWithRegisteredGrammar_RegisteredGrammarWins(string sequence)
    {
        var bytes = Encoding.ASCII.GetBytes(sequence);
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding(bytes, Code.F63)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingProtocolSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode(bytes);
            decoder.Complete();
        }

        sink.Strokes.ShouldNotContain(static value => value.Code == Code.F63);
        (sink.Responses.Count + sink.Focus.Count + sink.Pointers.Count + sink.Strokes.Count)
            .ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies an active paste consumes its terminator before described-key matching.</summary>
    [Fact]
    public void Decode_WhenPasteTerminatorIsAlsoDescribed_PasteTerminatorWins()
    {
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding("\u001b[201~"u8, Code.F63)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode("\u001b[200~payload\u001b[201~"u8);
            decoder.Complete();
        }

        sink.Pastes.ShouldHaveSingleItem().Utf8.Span.SequenceEqual("payload"u8).ShouldBeTrue();
        sink.Strokes.ShouldNotContain(static value => value.Code == Code.F63);
    }

    /// <summary>Verifies a described non-signature prefix uses longest match and replays mismatch bytes once.</summary>
    [Fact]
    public void Decode_WhenDescriptionPrefixesOverlap_UsesLongestMatchAndReplaysMismatchOnce()
    {
        var options = Options.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0xff], Code.F62),
                new KeyBinding([0xff, 0xfe], Code.F63)
            ]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode([0xff, (byte) 'x']);
            decoder.Complete();
        }

        sink.Strokes.Select(static value => value.Code).ShouldBe([Code.F62, Code.Character]);
        sink.Text.Select(static value => value.Value).ShouldBe([new Rune('x')]);
    }

    /// <summary>Verifies equivalent parser signatures cannot publish conflicting meanings.</summary>
    [Fact]
    public void Constructor_WhenEquivalentSignaturesConflict_Throws()
    {
        KeyBinding[] bindings =
        [
            new("\u001b[A"u8, Code.Up),
            new([0x9b, (byte) 'A'], Code.Down)
        ];

        _ = Should.Throw<ArgumentException>(() => new KeyMap(bindings));
    }

    /// <summary>Verifies a non-ANSI profile does not inherit an undescribed xterm key.</summary>
    [Fact]
    public void Decode_WhenProfileDoesNotDescribeLegacySequence_DoesNotApplyAnsiGrammar()
    {
        var options = Options.Default.WithKeyMap(KeyMap.Empty, useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode("\u001b[99~"u8);
            decoder.Complete();
        }

        sink.Strokes.ShouldNotContain(static value => value.NativeCode == 99);
    }

    /// <summary>Verifies a lone Escape retains the configured finite deadline with described keys active.</summary>
    [Fact]
    public void ExpireEscape_WhenDescriptionIsActive_StillUsesFiniteDeadline()
    {
        var time = new ManualTimeProvider();
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding("\u001b[A"u8, Code.Up)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();
        using InputDecoder decoder = new(sink, options, time);

        decoder.Decode("\u001b"u8);
        decoder.ExpireEscape().ShouldBeFalse();
        time.Advance(options.EscapeTimeout);

        decoder.ExpireEscape().ShouldBeTrue();
        sink.Strokes.Single().Code.ShouldBe(Code.Escape);
    }

    /// <summary>Verifies an Escape key with intermediates maps at every transport split.</summary>
    [Fact]
    public void Decode_WhenDescriptionUsesEscapeIntermediates_MapsAtEverySplit()
    {
        var sequence = "\u001b(B"u8.ToArray();
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding(sequence, Code.F63)]),
            useAnsiKeyGrammar: false);

        AssertDescribedAtEverySplit(sequence, Code.F63, options);
    }

    /// <summary>Verifies an unmatched Escape-intermediate signature reports once and recovers text.</summary>
    [Fact]
    public void Decode_WhenEscapeIntermediateIsUndescribed_ReportsAndRecovers()
    {
        var options = Options.Default.WithKeyMap(KeyMap.Empty, useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode("\u001b(Bx"u8);
            decoder.Complete();
        }

        sink.Diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(SequenceKind.Escape);
        sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>Verifies seven-bit and eight-bit SS3 spellings share one structural identity.</summary>
    [Fact]
    public void Constructor_WhenSevenAndEightBitSs3MeaningsConflict_Throws()
    {
        KeyBinding[] bindings =
        [
            new("\u001bOA"u8, Code.Up),
            new([0x8f, (byte) 'A'], Code.Down)
        ];

        _ = Should.Throw<ArgumentException>(() => new KeyMap(bindings));
    }

    /// <summary>Verifies described eight-bit SS3 input maps at every split.</summary>
    [Fact]
    public void Decode_WhenDescriptionUsesEightBitSs3_MapsAtEverySplit()
    {
        var sequence = new byte[] { 0x8f, (byte) 'A' };
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding(sequence, Code.Up)]),
            useAnsiKeyGrammar: false);

        options.KeyMap.FallbackBindings.ShouldBeEmpty();
        AssertDescribedAtEverySplit(sequence, Code.Up, options);
    }

    /// <summary>Verifies an unmatched eight-bit SS3 final reports once and recovers following text.</summary>
    [Fact]
    public void Decode_WhenEightBitSs3FinalIsUndescribed_ReportsAndRecovers()
    {
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding([0x8f, (byte) 'A'], Code.Up)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode([0x8f, (byte) 'Z', (byte) 'x']);
            decoder.Complete();
        }

        sink.Diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(SequenceKind.Escape);
        sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>Verifies representative structural families decode identically at every boundary.</summary>
    [Fact]
    public void Decode_WhenStructuralDescriptionFamiliesAreFragmented_MapAtEverySplit()
    {
        (byte[] Sequence, Code Code)[] cases =
        [
            ([0x08], Code.Backspace),
            ("\u001b(B"u8.ToArray(), Code.F60),
            ("\u001b[91~"u8.ToArray(), Code.F61),
            ([0x9b, (byte) '9', (byte) '2', (byte) '~'], Code.F62),
            ("\u001bOA"u8.ToArray(), Code.Up),
            ([0x8f, (byte) 'B'], Code.Down)
        ];

        foreach (var item in cases)
        {
            var options = Options.Default.WithKeyMap(
                new KeyMap([new KeyBinding(item.Sequence, item.Code)]),
                useAnsiKeyGrammar: false);

            AssertDescribedAtEverySplit(item.Sequence, item.Code, options);
        }
    }

    /// <summary>Verifies valid UTF-8 continuation bytes never become described C1 introducers.</summary>
    [Fact]
    public void Decode_WhenUtf8ContainsC1ContinuationBytes_PreservesUnicodeAtEverySplit()
    {
        var input = new byte[] { 0xc2, 0x8f, 0xc2, 0x9b };
        var options = Options.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0x8f, (byte) 'A'], Code.Up),
                new KeyBinding([0x9b, (byte) '9', (byte) '2', (byte) '~'], Code.F63)
            ]),
            useAnsiKeyGrammar: false);

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            decoder.Decode(input.AsSpan(0, split));
            decoder.Decode(input.AsSpan(split));
            decoder.Complete();

            sink.Text.Select(static value => value.Value)
                .ShouldBe([new Rune(0x8f), new Rune(0x9b)], $"split {split}");
            sink.Strokes.Select(static value => value.Code)
                .ShouldBe([Code.Character, Code.Character], $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies an unmatched described eight-bit CSI family reports and recovers text.</summary>
    [Fact]
    public void Decode_WhenEightBitCsiSignatureIsUndescribed_ReportsAndRecovers()
    {
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding([0x9b, (byte) '9', (byte) '2', (byte) '~'], Code.F63)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode([0x9b, (byte) '9', (byte) '3', (byte) '~', (byte) 'x']);
            decoder.Complete();
        }

        sink.Diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(SequenceKind.Csi);
        sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune('x'));
    }

    /// <summary>Verifies explicit parser-wide C1 policy remains independent of described keys.</summary>
    [Fact]
    public void Decode_WhenCallerExplicitlyEnablesC1WithoutMap_PreservesParserControlSemantics()
    {
        var configured = Options.Default with
        {
            ParserLimits = ParserLimits.Default with { AcceptEightBitControls = true }
        };
        var options = configured.WithKeyMap(KeyMap.Empty, useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode([0x8f]);
            decoder.Complete();
        }

        sink.Strokes.ShouldBe(
        [
            new Stroke(Code.Unknown, null, 0x8f, Modifiers.None, InputAction.Press)
        ]);
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>Verifies a fallback binding cannot steal a pending UTF-8 continuation.</summary>
    [Fact]
    public void Decode_WhenFallbackStartsWithUtf8Continuation_PendingUnicodeWinsAtEverySplit()
    {
        var input = "\u00a0"u8.ToArray();
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding([0xa0], Code.F63)]),
            useAnsiKeyGrammar: false);

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            decoder.Decode(input.AsSpan(0, split));
            decoder.Decode(input.AsSpan(split));
            decoder.Complete();

            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(new Rune(0xa0), $"split {split}");
            sink.Strokes.ShouldHaveSingleItem().Code.ShouldBe(Code.Character, $"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies an established matcher prefix continues to own its remaining bytes.</summary>
    [Fact]
    public void Decode_WhenMatcherPrefixPrecedesUtf8Continuation_ExistingMatchWins()
    {
        var sequence = "\u00a0"u8.ToArray();
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding(sequence, Code.F63)]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode(sequence.AsSpan(0, 1));
            decoder.Decode(sequence.AsSpan(1));
            decoder.Complete();
        }

        sink.Strokes.ShouldHaveSingleItem().Code.ShouldBe(Code.F63);
        sink.Text.ShouldBeEmpty();
        sink.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>Verifies a suffix after a shorter match is rematched as an adjacent described key.</summary>
    [Fact]
    public void Decode_WhenShorterMatchLeavesDescribedSuffix_RematchesSuffixAtEverySplit()
    {
        var input = new byte[] { 0xff, 0xfe, 0x78 };
        var options = Options.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0xff], Code.F61),
                new KeyBinding([0xff, 0xfe, 0xff], Code.F62),
                new KeyBinding([0xfe, 0x78], Code.F63)
            ]),
            useAnsiKeyGrammar: false);

        for (var split = 0; split <= input.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            decoder.Decode(input.AsSpan(0, split));
            decoder.Decode(input.AsSpan(split));
            decoder.Complete();

            sink.Strokes.Select(static value => value.Code)
                .ShouldBe([Code.F61, Code.F63], $"split {split}");
            sink.Text.ShouldBeEmpty($"split {split}");
            sink.Diagnostics.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies every fallback match contributes its exact bytes to later diagnostic offsets.</summary>
    [Fact]
    public void Decode_WhenFallbackKeysPrecedeMalformedProtocol_PreservesAbsoluteOffset()
    {
        var options = Options.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0xff], Code.F62),
                new KeyBinding([0xfe], Code.F63)
            ]),
            useAnsiKeyGrammar: false);
        var sink = new RecordingInputSink();

        using (InputDecoder decoder = new(sink, options))
        {
            decoder.Decode([0xff, 0xfe]);
            decoder.Decode("\u001b[1:x"u8);
            decoder.Complete();
        }

        sink.Strokes.Select(static value => value.Code).ShouldBe([Code.F62, Code.F63]);
        sink.Diagnostics.ShouldHaveSingleItem().Offset.ShouldBe(7);
    }

    /// <summary>Verifies CSI parameter signatures accept the exact active limit and reject one more.</summary>
    [Fact]
    public void Constructor_WhenCsiParametersMeetOrExceedActiveLimit_EnforcesLimit()
    {
        var limits = ParserLimits.Default with { MaxParameterBytes = 3 };

        var exact = new KeyBinding("\u001b[123A"u8, Code.Up, Modifiers.None, limits);

        _ = exact.Signature.ShouldNotBeNull();
        _ = Should.Throw<ArgumentException>(() =>
            new KeyBinding("\u001b[1234A"u8, Code.Up, Modifiers.None, limits));
    }

    /// <summary>Verifies Escape intermediates accept the exact active limit and reject one more.</summary>
    [Fact]
    public void Constructor_WhenEscapeIntermediatesMeetOrExceedActiveLimit_EnforcesLimit()
    {
        var limits = ParserLimits.Default with { MaxIntermediateBytes = 2 };

        var exact = new KeyBinding("\u001b()B"u8, Code.F62, Modifiers.None, limits);

        _ = exact.Signature.ShouldNotBeNull();
        _ = Should.Throw<ArgumentException>(() =>
            new KeyBinding("\u001b()#B"u8, Code.F63, Modifiers.None, limits));
    }

    /// <summary>Verifies the ordinary constructor compiles against default parser limits.</summary>
    [Fact]
    public void Constructor_WhenCsiParametersExceedDefaultLimit_RejectsSignature()
    {
        var exact = CsiWithParameters(ParserLimits.Default.MaxParameterBytes);
        var over = CsiWithParameters(ParserLimits.Default.MaxParameterBytes + 1);

        _ = new KeyBinding(exact, Code.Up);
        _ = Should.Throw<ArgumentException>(() => new KeyBinding(over, Code.Up));
    }

    /// <summary>Verifies matcher disposal clears a retained prefix and releases every owned array.</summary>
    [Fact]
    public void Dispose_WhenMatcherRetainsPrefix_ReleasesOwnedStorageIdempotently()
    {
        var matcher = new KeySequenceMatcher(
        [
            new KeyBinding([0xff], Code.F62),
            new KeyBinding([0xff, 0xfe], Code.F63)
        ]);

        var status = matcher.Add(0xff, out _, out _, out _);

        status.ShouldBe(KeySequenceMatchStatus.Pending);
        matcher.IsPending.ShouldBeTrue();
        matcher.RetainsStorage.ShouldBeTrue();

        matcher.Dispose();
        matcher.Dispose();

        matcher.IsPending.ShouldBeFalse();
        matcher.RetainsStorage.ShouldBeFalse();
    }

    /// <summary>Verifies decoder disposal releases matcher and rematch workspace ownership.</summary>
    [Fact]
    public void Dispose_WhenDecoderUsedRematchWorkspace_ReleasesKeyStorageAndRejectsUse()
    {
        var options = Options.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0xff], Code.F61),
                new KeyBinding([0xff, 0xfe, 0xff], Code.F62),
                new KeyBinding([0xfe, (byte) 'x'], Code.F63)
            ]),
            useAnsiKeyGrammar: false);
        var ownership = DecoderOwnershipProbe.CreateAfterRematch(options);
        var decoder = ownership.Decoder;

        DecoderOwnershipProbe.Dispose(decoder);
        decoder.Dispose();

        DecoderOwnershipProbe.WaitForRelease(ownership.Matcher, ownership.Replay).ShouldBeTrue();
        _ = Should.Throw<ObjectDisposedException>(() => decoder.Decode([]));
        _ = Should.Throw<ObjectDisposedException>(decoder.Complete);
        _ = Should.Throw<ObjectDisposedException>(() => decoder.ExpireEscape());
        GC.KeepAlive(decoder);
    }

    private static byte[] CsiWithParameters(int count)
    {
        var sequence = new byte[count + 3];
        sequence[0] = 0x1b;
        sequence[1] = (byte) '[';
        sequence.AsSpan(2, count).Fill((byte) '1');
        sequence[^1] = (byte) 'A';
        return sequence;
    }

    private static void AssertDescribedAtEverySplit(byte[] sequence, Code code, Options options)
    {
        for (var split = 0; split <= sequence.Length; split++)
        {
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            decoder.Decode(sequence.AsSpan(0, split));
            decoder.Decode(sequence.AsSpan(split));
            decoder.Complete();

            sink.Strokes.ShouldBe(
            [
                new Stroke(code, null, 0, Modifiers.None, InputAction.Press)
            ], $"{Convert.ToHexString(sequence)} split {split}");
            sink.Diagnostics.ShouldBeEmpty($"{Convert.ToHexString(sequence)} split {split}");
        }
    }
}
