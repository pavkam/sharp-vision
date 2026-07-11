using SharpVision.Terminal.Input;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

using Rune = System.Text.Rune;

namespace SharpVision.Terminal.Tests.Input;

/// <summary>Verifies terminal focus transitions and adjacent input.</summary>
public sealed class FocusTests
{
    /// <summary>
    /// Verifies gained/lost reports preserve order and do not consume text.
    /// </summary>
    [Fact]
    public void Decode_WhenFocusAndTextAreAdjacent_EmitsOrderedValues()
    {
        var bytes = "\u001b[Ix\u001b[O"u8.ToArray();

        for (var split = 0; split <= bytes.Length; split++)
        {
            var sink = new RecordingInputSink();
            using var decoder = new Decoder(sink);
            decoder.Decode(bytes.AsSpan(0, split));
            decoder.Decode(bytes.AsSpan(split));
            decoder.Complete();

            sink.Focus.ShouldBe([new Focus(true), new Focus(false)], $"split {split}");
            sink.Text.Single().Value.ShouldBe(new Rune('x'), $"split {split}");
        }
    }
}
