using SharpVision.Terminal.Protocols;

using Shouldly;

namespace SharpVision.Terminal.Tests.Support;

/// <summary>
/// Compares parser observations across whole, split, and byte-at-a-time reads.
/// </summary>
public static class Fragmentation
{
    /// <summary>
    /// Asserts that every two-part split and byte-at-a-time delivery matches a
    /// complete read after adjacent text callbacks are normalized.
    /// </summary>
    /// <param name="input">A complete representative sequence.</param>
    /// <param name="limits">Optional parser limits.</param>
    public static void AssertAll(ReadOnlySpan<byte> input, Limits? limits = null)
    {
        var owned = input.ToArray();
        var expected = Parse([owned], limits);

        for (var split = 0; split <= owned.Length; split++)
        {
            var actual = Parse([owned[..split], owned[split..]], limits);
            actual.ShouldBe(expected, $"Input differed at split {split}.");
        }

        var bytes = owned.Select(static value => new[] { value }).ToArray();
        Parse(bytes, limits).ShouldBe(expected, "Byte-at-a-time input differed.");
    }

    private static string[] Normalize(IEnumerable<Observation> observations)
    {
        var normalized = new List<string>();
        var text = new List<byte>();

        foreach (var observation in observations)
        {
            if (observation.Type == "Text")
            {
                text.AddRange(observation.First);
                continue;
            }

            FlushText();
            normalized.Add(Describe(observation));
        }

        FlushText();

        return [.. normalized];

        void FlushText()
        {
            if (text.Count == 0)
            {
                return;
            }

            normalized.Add($"Text:{Convert.ToHexString([.. text])}");
            text.Clear();
        }
    }

    private static string[] Parse(IEnumerable<byte[]> reads, Limits? limits)
    {
        using var parser = new Parser(limits);
        var sink = new RecordingSink();

        foreach (var read in reads)
        {
            parser.Parse(read, ref sink);
        }

        parser.Complete(ref sink);

        return Normalize(sink.Observations);
    }

    private static string Describe(Observation observation) =>
        observation.Diagnostic is { } diagnostic
            ? $"Diagnostic:{diagnostic.Code}:{diagnostic.Kind}:" +
                $"{diagnostic.Offset}:{diagnostic.DiscardedBytes}"
            : $"{observation.Type}:{Convert.ToHexString(observation.First)}:" +
                $"{Convert.ToHexString(observation.Second)}:{observation.Final}";
}
