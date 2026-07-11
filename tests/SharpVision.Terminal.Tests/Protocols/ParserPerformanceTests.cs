using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Tests.Support;

using Shouldly;

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies parser allocation and hostile-payload bounds.
/// </summary>
public sealed class ParserPerformanceTests
{
    /// <summary>
    /// Verifies a large unterminated OSC retains only the configured payload
    /// budget and recovers at a later ST.
    /// </summary>
    [Fact]
    public void Parse_WhenOscIsHostile_BoundsAllocationAndRecovers()
    {
        const int maxPayload = 1_024;
        var limits = Limits.Default with { MaxStringBytes = maxPayload };
        using var parser = new Parser(limits);
        var sink = new RecordingSink();
        var input = new byte[2 * 1_024 * 1_024];
        input[0] = 0x1b;
        input[1] = (byte) ']';
        input.AsSpan(2).Fill((byte) 's');

        var before = GC.GetAllocatedBytesForCurrentThread();
        parser.Parse(input, ref sink);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBeLessThan(256 * 1_024);
        sink.Observations.ShouldBeEmpty();

        parser.Parse("\u001b\\X"u8, ref sink);

        sink.Observations.Count.ShouldBe(2);
        sink.Observations[0].Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
        sink.Observations[0].Diagnostic!.Value.ToString().ShouldNotContain(new string('s', 8));
        sink.Observations[1].First.ShouldBe("X"u8.ToArray());
    }

    /// <summary>
    /// Verifies the warmed CSI hot loop allocates no managed objects per event.
    /// </summary>
    [Fact]
    public void Parse_WhenCsiLoopIsWarm_AllocatesNoManagedBytes()
    {
        using var parser = new Parser();
        var sink = new CountingSink();
        var input = "\u001b[2J"u8;

        for (var index = 0; index < 100; index++)
        {
            parser.Parse(input, ref sink);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 10_000; index++)
        {
            parser.Parse(input, ref sink);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
        sink.CsiCount.ShouldBe(10_100);
    }

}
