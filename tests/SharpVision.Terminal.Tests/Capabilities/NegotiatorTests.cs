using System.Buffers;
using System.Text;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;

using Shouldly;

namespace SharpVision.Terminal.Tests.Capabilities;

/// <summary>Verifies bounded startup query encoding and profile publication.</summary>
public sealed class NegotiatorTests
{
    /// <summary>Verifies the configured query limit truncates by fixed priority.</summary>
    /// <param name="capacity">The maximum concurrent query count.</param>
    /// <param name="expected">The exact expected startup bytes.</param>
    [Theory]
    [InlineData(1, "\u001b[c")]
    [InlineData(2, "\u001b[?u\u001b[c")]
    [InlineData(3, "\u001b[?u\u001b[c\u001b[?2026$p")]
    [InlineData(4, "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p")]
    [InlineData(5, "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p\u001b[?2004$p")]
    [InlineData(6, "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p\u001b[?2004$p\u001b[?1006$p")]
    [InlineData(7, "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p")]
    public void Start_WhenCapacityVaries_TruncatesByPriority(
        int capacity,
        string expected)
    {
        // Arrange
        var limits = Limits.Default with
        {
            MaxConcurrentQueries = capacity,
        };
        var options = new NegotiationOptions(
            new Dictionary<string, string?>(),
            limits: limits);
        var negotiator = new Negotiator(options, new ManualTimeProvider());
        var output = new ArrayBufferWriter<byte>();

        // Act
        negotiator.Start(output);

        // Assert
        Encoding.ASCII.GetString(output.WrittenSpan).ShouldBe(expected);
    }

    /// <summary>Verifies the default batch is safe, exact, and ordered.</summary>
    [Fact]
    public void Start_WhenDefaultCapacityIsAvailable_WritesSafeQueriesInOrder()
    {
        // Arrange
        var options = new NegotiationOptions(
            new Dictionary<string, string?>());
        var negotiator = new Negotiator(options, new ManualTimeProvider());
        var output = new ArrayBufferWriter<byte>();

        // Act
        negotiator.Start(output);

        // Assert
        Encoding.ASCII.GetString(output.WrittenSpan).ShouldBe(
            "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p" +
            "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p");
        negotiator.IsComplete.ShouldBeFalse();
    }

    /// <summary>Verifies invalid calls are rejected without ambiguous state.</summary>
    [Fact]
    public void Start_WhenStateIsInvalid_ThrowsDeterministically()
    {
        // Arrange
        var negotiator = new Negotiator(
            new NegotiationOptions(new Dictionary<string, string?>()));

        // Act / Assert
        _ = Should.Throw<ArgumentNullException>(() => negotiator.Start(null!));
        negotiator.IsStarted.ShouldBeFalse();
        _ = Should.Throw<InvalidOperationException>(() => _ = negotiator.Capabilities);
        var output = new ArrayBufferWriter<byte>();
        negotiator.Start(output);
        _ = Should.Throw<InvalidOperationException>(() => negotiator.Start(output));
    }
}
