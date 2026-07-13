// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using System.Text;

using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

using InputDecoder = Terminal.Input.Decoder;

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
        Random random = new Random(_seed);

        for (var testCase = 0; testCase < 256; testCase++)
        {
            var hostile = new byte[random.Next(1, 513)];
            random.NextBytes(hostile);
            RecordingInputSink sink = new RecordingInputSink();
            Options options = Options.Default with
            {
                MaxPasteBytes = 32,
                Limits = Limits.Default with
                {
                    MaxParameterBytes = 16,
                    MaxIntermediateBytes = 4,
                    MaxStringBytes = 32,
                },
            };

            try
            {
                using InputDecoder decoder = new InputDecoder(sink, options);

                foreach (ReadOnlyMemory<byte> fragment in Fragment(hostile, random))
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
