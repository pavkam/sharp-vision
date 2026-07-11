using SharpVision.Terminal.Transport;

using Shouldly;

namespace SharpVision.Terminal.Tests.Transport;

/// <summary>
/// Verifies memory-based stream transport ownership and write serialization.
/// </summary>
public sealed class StreamTransportTests
{
    /// <summary>
    /// Verifies async reads and writes preserve exact bytes.
    /// </summary>
    [Fact]
    public async Task ReadWriteAsync_WhenStreamsAreValid_TransfersExactBytesAsync()
    {
        await using var input = new MemoryStream("input"u8.ToArray());
        await using var output = new MemoryStream();
        await using var transport = new StreamTransport(input, output, leaveOpen: true);
        var destination = new byte[5];

        var read = await transport.ReadAsync(destination, TestContext.Current.CancellationToken);
        await transport.WriteAsync("output"u8.ToArray(), TestContext.Current.CancellationToken);
        await transport.FlushAsync(TestContext.Current.CancellationToken);

        read.ShouldBe(5);
        destination.ShouldBe("input"u8.ToArray());
        output.ToArray().ShouldBe("output"u8.ToArray());
    }

    /// <summary>
    /// Verifies invalid stream capabilities are rejected by the constructor.
    /// </summary>
    [Fact]
    public void Constructor_WhenStreamCapabilityIsInvalid_ThrowsArgumentException()
    {
        using var unreadable = new WriteOnlyStream();
        using var unwritable = new ReadOnlyStream();

        _ = Should.Throw<ArgumentException>(
            () => new StreamTransport(unreadable, Stream.Null));
        _ = Should.Throw<ArgumentException>(
            () => new StreamTransport(Stream.Null, unwritable));
    }

    /// <summary>
    /// Verifies concurrent calls cannot interleave underlying write operations.
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenCallsOverlap_SerializesWritesAsync()
    {
        await using var output = new BlockingStream();
        await using var transport = new StreamTransport(Stream.Null, output, leaveOpen: true);

        var first = transport.WriteAsync("one"u8.ToArray(), TestContext.Current.CancellationToken);
        await output.FirstStarted;
        var second = transport.WriteAsync("two"u8.ToArray(), TestContext.Current.CancellationToken);

        output.MaximumActive.ShouldBe(1);
        output.Release();
        await first;
        await second;
        output.MaximumActive.ShouldBe(1);
        output.Writes.ShouldBe(["one", "two"]);
    }

    /// <summary>
    /// Verifies a caller waiting behind another write observes cancellation.
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenWaitingCallerIsCancelled_ThrowsOperationCanceledExceptionAsync()
    {
        await using var output = new BlockingStream();
        await using var transport = new StreamTransport(Stream.Null, output, leaveOpen: true);
        var first = transport.WriteAsync("one"u8.ToArray(), TestContext.Current.CancellationToken);
        await output.FirstStarted;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await transport.WriteAsync("two"u8.ToArray(), cancellation.Token));
        output.Release();
        await first;

        output.Writes.ShouldBe(["one"]);
    }

    /// <summary>
    /// Verifies leave-open controls stream ownership and disposal is idempotent.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenLeaveOpenVaries_UsesDocumentedOwnershipAsync()
    {
        var owned = new TrackingStream();
        var borrowed = new TrackingStream();
        var ownedTransport = new StreamTransport(Stream.Null, owned);
        var borrowedTransport = new StreamTransport(Stream.Null, borrowed, leaveOpen: true);

        await ownedTransport.DisposeAsync();
        await ownedTransport.DisposeAsync();
        await borrowedTransport.DisposeAsync();

        owned.DisposeCount.ShouldBe(1);
        borrowed.DisposeCount.ShouldBe(0);
        await borrowed.DisposeAsync();
    }

    private sealed class WriteOnlyStream: MemoryStream
    {
        public override bool CanRead => false;
    }

    private sealed class ReadOnlyStream: MemoryStream
    {
        public override bool CanWrite => false;
    }

    private sealed class TrackingStream: MemoryStream
    {
        public int DisposeCount { get; private set; }

        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }
    }

    private sealed class BlockingStream: MemoryStream
    {
        private readonly TaskCompletionSource _firstStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;

        public Task FirstStarted => _firstStarted.Task;

        public int MaximumActive { get; private set; }

        public List<string> Writes { get; } = [];

        public void Release() => _release.TrySetResult();

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _active++;
            MaximumActive = Math.Max(MaximumActive, _active);
            _ = _firstStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            Writes.Add(System.Text.Encoding.UTF8.GetString(buffer.Span));
            _active--;
        }
    }
}
