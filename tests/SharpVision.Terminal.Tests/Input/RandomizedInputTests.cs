// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using SharpVision.Terminal.Input;

/// <summary>Verifies fixed-seed decoder boundedness and recovery.</summary>
public sealed class RandomizedInputTests
{
    private const int _seed = 0x1A2B3C;

    /// <summary>
    /// Verifies hostile fragments remain bounded and recover after an explicit cancel.
    /// </summary>
    [Fact]
    public void Decode_WhenBytesAreHostile_RemainsBoundedAndRecoversKnownKey()
    {
        var random = new Random(_seed);

        for (var testCase = 0; testCase < 256; testCase++)
        {
            var hostile = new byte[random.Next(1, 513)];
            random.NextBytes(hostile);
            var sink = new RecordingInputSink();
            var options = Options.Default with
            {
                MaxPasteBytes = 32,
                ParserLimits = ParserLimits.Default with
                {
                    MaxParameterBytes = 16,
                    MaxIntermediateBytes = 4,
                    MaxStringBytes = 32
                }
            };

            try
            {
                using InputDecoder decoder = new(sink, options);

                foreach (var fragment in Fragment(hostile, random))
                {
                    decoder.Decode(fragment.Span);
                }

                decoder.Decode("\u001b[201~\u0018\u0018\u0018\u0018\u0018\u0018\u0018\u0018x"u8);
                decoder.Complete();

                sink.Text.ShouldContain(static value => value.Value == new Rune('x'));
                sink.Pastes.ShouldAllBe(static paste => paste.Utf8.Length <= 32);
            }
            catch (Exception exception) when (exception is not ShouldAssertException)
            {
                throw new InvalidOperationException(
                    $"Input seed {_seed}, case {testCase}, bytes {Convert.ToHexString(hostile)}.",
                    exception);
            }
            catch (ShouldAssertException exception)
            {
                throw new InvalidOperationException(
                    $"Input seed {_seed}, case {testCase}, bytes {Convert.ToHexString(hostile)}.",
                    exception);
            }
        }
    }

    /// <summary>
    /// Verifies random described maps, query adjacency, invalid UTF-8 bindings,
    /// and transport fragmentation retain deterministic typed results.
    /// </summary>
    [Fact]
    public void Decode_WhenDescriptionMapsAndFragmentsVary_RecoversQueryKeyAndTrailingText()
    {
        const int descriptionSeed = 0x4B455953;
        var random = new Random(descriptionSeed);
        var bindings = new List<KeyBinding>();

        for (var function = 36; function <= 63; function++)
        {
            var sequence = Encoding.ASCII.GetBytes($"\u001b[{function + 64}~");
            function.TryGet(out var code).ShouldBeTrue();
            bindings.Add(new KeyBinding(sequence, code));
        }

        bindings.Add(new KeyBinding([0xff], Code.F62));
        bindings.Add(new KeyBinding([0xff, 0xfe], Code.F63));
        var options = Options.Default.WithKeyMap(
            new KeyMap(bindings),
            useAnsiKeyGrammar: false);

        for (var testCase = 0; testCase < 128; testCase++)
        {
            var binding = bindings[random.Next(0, bindings.Count)];
            var input = new byte["\u001b[?1;2c"u8.Length + binding.Sequence.Length + 1];
            "\u001b[?1;2c"u8.CopyTo(input);
            binding.Sequence.Span.CopyTo(input.AsSpan("\u001b[?1;2c"u8.Length));
            input[^1] = (byte) 'x';
            var sink = new RecordingProtocolSink();

            try
            {
                using InputDecoder decoder = new(sink, options);

                foreach (var fragment in Fragment(input, random))
                {
                    decoder.Decode(fragment.Span);
                }

                decoder.Complete();

                sink.Responses.Count.ShouldBe(1);
                sink.Strokes.ShouldContain(value =>
                    value.Code == binding.Code && value.Modifiers == binding.Modifiers);
                sink.Text.ShouldContain(static value => value.Value == new Rune('x'));
            }
            catch (Exception exception) when (exception is not ShouldAssertException)
            {
                throw new InvalidOperationException(
                    $"Description input seed {descriptionSeed}, case {testCase}, bytes {Convert.ToHexString(input)}.",
                    exception);
            }
            catch (ShouldAssertException exception)
            {
                throw new InvalidOperationException(
                    $"Description input seed {descriptionSeed}, case {testCase}, bytes {Convert.ToHexString(input)}.",
                    exception);
            }
        }
    }

    /// <summary>Verifies one overlapping non-UTF-8 key is identical at every read boundary.</summary>
    [Fact]
    public void Decode_WhenInvalidUtf8DescriptionKeyIsFragmented_UsesLongestMatchAtEverySplit()
    {
        var sequence = new byte[] { 0xff, 0xfe };
        var options = Options.Default.WithKeyMap(
            new KeyMap(
            [
                new KeyBinding([0xff], Code.F62),
                new KeyBinding(sequence, Code.F63)
            ]),
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
                new Stroke(Code.F63, null, 0, Modifiers.None, KeyAction.Press)
            ], $"split {split}");
            sink.Text.ShouldBeEmpty($"split {split}");
        }
    }

    /// <summary>Verifies random continuation-leading fallback bindings cannot steal valid UTF-8.</summary>
    [Fact]
    public void Decode_WhenRandomFallbackBindingsStartWithContinuations_UnicodeAlwaysWins()
    {
        const int continuationSeed = 0x554E4943;
        var random = new Random(continuationSeed);
        var bindings = Enumerable.Range(0xa0, 0x20)
            .Select(static value => new KeyBinding([checked((byte) value)], Code.F63))
            .ToArray();
        var options = Options.Default.WithKeyMap(
            new KeyMap(bindings),
            useAnsiKeyGrammar: false);

        for (var testCase = 0; testCase < 128; testCase++)
        {
            var continuation = checked((byte) random.Next(0xa0, 0xc0));
            var input = new byte[] { 0xc2, continuation };
            var split = random.Next(0, input.Length + 1);
            var sink = new RecordingInputSink();

            using (InputDecoder decoder = new(sink, options))
            {
                decoder.Decode(input.AsSpan(0, split));
                decoder.Decode(input.AsSpan(split));
                decoder.Complete();
            }

            sink.Text.ShouldHaveSingleItem().Value.ShouldBe(
                new Rune(continuation),
                $"seed {continuationSeed}, case {testCase}, split {split}");
            sink.Strokes.ShouldHaveSingleItem().Code.ShouldBe(Code.Character);
            sink.Diagnostics.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies random shorter-match suffixes are rematched as adjacent keys.</summary>
    [Fact]
    public void Decode_WhenRandomOverlappingFallbackSuffixesArrive_RematchesEverySuffix()
    {
        const int overlapSeed = 0x53554658;
        var random = new Random(overlapSeed);
        var bindings = new List<KeyBinding>
        {
            new([0xff], Code.F61),
            new([0xff, 0xfe, 0xff], Code.F62)
        };

        for (var final = 0xa0; final < 0xc0; final++)
        {
            bindings.Add(new KeyBinding([0xfe, checked((byte) final)], Code.F63));
        }

        var options = Options.Default.WithKeyMap(
            new KeyMap(bindings),
            useAnsiKeyGrammar: false);

        for (var testCase = 0; testCase < 128; testCase++)
        {
            var final = checked((byte) random.Next(0xa0, 0xc0));
            var input = new byte[] { 0xff, 0xfe, final };
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            foreach (var fragment in Fragment(input, random))
            {
                decoder.Decode(fragment.Span);
            }

            decoder.Complete();

            sink.Strokes.Select(static value => value.Code).ShouldBe(
                [Code.F61, Code.F63],
                $"seed {overlapSeed}, case {testCase}, bytes {Convert.ToHexString(input)}");
            sink.Text.ShouldBeEmpty();
            sink.Diagnostics.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies random adjacent fallback counts accumulate into absolute diagnostics.</summary>
    [Fact]
    public void Decode_WhenRandomFallbackCountsPrecedeMalformedProtocol_AccumulatesOffsets()
    {
        const int offsetSeed = 0x4F464653;
        var random = new Random(offsetSeed);
        var options = Options.Default.WithKeyMap(
            new KeyMap([new KeyBinding([0xff], Code.F63)]),
            useAnsiKeyGrammar: false);

        for (var testCase = 0; testCase < 128; testCase++)
        {
            var keyCount = random.Next(1, 17);
            var input = new byte[keyCount + "\u001b[1:x"u8.Length];
            input.AsSpan(0, keyCount).Fill(0xff);
            "\u001b[1:x"u8.CopyTo(input.AsSpan(keyCount));
            var sink = new RecordingInputSink();
            using InputDecoder decoder = new(sink, options);

            foreach (var fragment in Fragment(input, random))
            {
                decoder.Decode(fragment.Span);
            }

            decoder.Complete();

            sink.Strokes.Count.ShouldBe(keyCount);
            sink.Diagnostics.ShouldHaveSingleItem().Offset.ShouldBe(
                keyCount + 5,
                $"seed {offsetSeed}, case {testCase}, count {keyCount}");
        }
    }

    private static IEnumerable<ReadOnlyMemory<byte>> Fragment(byte[] input, Random random)
    {
        var offset = 0;

        while (offset < input.Length)
        {
            var length = Math.Min(random.Next(1, 9), input.Length - offset);
            yield return input.AsMemory(offset, length);
            offset += length;
        }
    }
}
