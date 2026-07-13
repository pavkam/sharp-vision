namespace SharpVision.Terminal.Tests.Protocols;

using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

/// <summary>
/// Verifies deterministic randomized parser invariants.
/// </summary>
public sealed class ParserRandomizedTests
{
    private const int _validSeed = 0x51A2;
    private const int _hostileSeed = 0xC015;

    /// <summary>
    /// Verifies generated valid sequences are equivalent at every fragmentation.
    /// </summary>
    [Fact]
    public void Parse_WhenValidSequencesAreRandomized_MatchesEveryFragmentation()
    {
        var random = new Random(_validSeed);

        for (var index = 0; index < 64; index++)
        {
            var input = CreateValid(random);

            try
            {
                Fragmentation.AssertAll(input);
            }
            catch (ShouldAssertException exception)
            {
                throw new InvalidOperationException(
                    $"Valid parser seed {_validSeed}, case {index}, bytes {Convert.ToHexString(input)}.",
                    exception);
            }
        }
    }

    /// <summary>
    /// Verifies arbitrary bytes cannot prevent recovery into a known trailing CSI.
    /// </summary>
    [Fact]
    public void Parse_WhenHostileBytesAreRandomized_RecoversKnownTrailingCsi()
    {
        var random = new Random(_hostileSeed);

        for (var index = 0; index < 256; index++)
        {
            var input = new byte[69];
            random.NextBytes(input.AsSpan(0, 64));
            input[64] = 0x18;
            "\u001b[2J"u8.CopyTo(input.AsSpan(65));
            using var parser = new Parser(Limits.Default with
            {
                MaxParameterBytes = 16,
                MaxIntermediateBytes = 4,
                MaxStringBytes = 64,
            });
            var sink = new RecordingSink();

            parser.Parse(input, ref sink);
            parser.Complete(ref sink);

            sink.Observations.ShouldContain(
                static observation =>
                    observation.Type == "Csi" &&
                    observation.Final == (byte) 'J' &&
                    observation.First.Length == 1 &&
                    observation.First[0] == (byte) '2',
                $"Hostile parser seed {_hostileSeed}, case {index}.");
            parser.Offset.ShouldBe(input.Length);
        }
    }

    private static byte[] CreateValid(Random random)
    {
        var selector = random.Next(4);
        var value = random.Next(1, 10_000);

        return selector switch
        {
            0 => Encoding.ASCII.GetBytes($"left\u001b[{value}Aright"),
            1 => Encoding.ASCII.GetBytes($"\u001b]2;title-{value}\u001b\\"),
            2 => Encoding.ASCII.GetBytes($"\u001bP{value}$qdata-{value}\u001b\\"),
            _ => Encoding.ASCII.GetBytes($"\u001b_payload-{value}\u001b\\"),
        };
    }
}
