// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Transport;

/// <summary>
/// Verifies memory-based stream transport ownership and write serialization.
/// </summary>
public sealed class StreamTransportTests
{
    // POSIX EIO, the errno a Unix read reports once the terminal peer has hung up.
    private const int _inputOutputErrorNumber = 5;

    /// <summary>
    /// Verifies async reads and writes preserve exact bytes.
    /// </summary>
    [Fact]
    public async Task ReadWriteAsync_WhenStreamsAreValid_TransfersExactBytesAsync()
    {
        await using MemoryStream input = new("input"u8.ToArray());
        await using MemoryStream output = new();
        await using StreamTransport transport = new(input, output, leaveOpen: true);
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
        using WriteOnlyStream unreadable = new();
        using ReadOnlyStream unwritable = new();

        _ = Should.Throw<ArgumentException>(() => new StreamTransport(unreadable, Stream.Null));
        _ = Should.Throw<ArgumentException>(() => new StreamTransport(Stream.Null, unwritable));
    }

    /// <summary>
    /// Verifies concurrent calls cannot interleave underlying write operations.
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenCallsOverlap_SerializesWritesAsync()
    {
        await using BlockingStream output = new();
        await using StreamTransport transport = new(Stream.Null, output, leaveOpen: true);

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
        await using BlockingStream output = new();
        await using StreamTransport transport = new(Stream.Null, output, leaveOpen: true);
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

    /// <summary>Verifies disposal cannot invalidate the serialization gate while writes and a
    /// flush that were admitted before disposal are still queued on it.</summary>
    [Fact]
    public async Task DisposeAsync_WhenWritesAndFlushAreQueued_AllOperationsSettleBeforeStreamDisposalAsync()
    {
        await using BlockingStream output = new();
        var transport = new StreamTransport(Stream.Null, output, leaveInputOpen: true, leaveOutputOpen: false);
        var first = transport.WriteAsync("one"u8.ToArray(), TestContext.Current.CancellationToken).AsTask();
        await output.FirstStarted;
        var second = transport.WriteAsync("two"u8.ToArray(), TestContext.Current.CancellationToken).AsTask();
        var flush = transport.FlushAsync(TestContext.Current.CancellationToken).AsTask();

        var disposal = transport.DisposeAsync().AsTask();
        output.Release();

        await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await second.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await flush.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        await disposal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        output.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// Verifies disposal waits for a read that is still in flight and only disposes the input
    /// stream once that read has genuinely completed, even though the stream never honors the
    /// cancellation disposal requests of it.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenReadIsQueued_WaitsForReadBeforeStreamDisposalAsync()
    {
        var input = new BlockingReadStream(ignoresCancellation: true);
        var transport = new StreamTransport(
            input,
            Stream.Null,
            leaveInputOpen: false,
            leaveOutputOpen: true,
            readDrainTimeout: TimeSpan.FromSeconds(5));
        var destination = new byte[4];
        var read = transport.ReadAsync(destination, TestContext.Current.CancellationToken).AsTask();
        await input.FirstStarted;

        var disposal = transport.DisposeAsync().AsTask();
        input.Release(4);

        (await read.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBe(4);
        await disposal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        input.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// Verifies a read that neither completes nor honors the disposal-triggered cancellation
    /// cannot stall disposal past the configured read drain timeout: the streams are disposed
    /// out from under the abandoned read rather than blocking forever.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenReadIgnoresCancellation_AbandonsItAfterReadDrainTimeoutAsync()
    {
        var input = new BlockingReadStream(ignoresCancellation: true);
        var transport = new StreamTransport(
            input,
            Stream.Null,
            leaveInputOpen: false,
            leaveOutputOpen: true,
            readDrainTimeout: TimeSpan.FromMilliseconds(50));
        var read = transport.ReadAsync(new byte[4], TestContext.Current.CancellationToken).AsTask();
        await input.FirstStarted;

        await transport.DisposeAsync().AsTask().WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        input.DisposeCount.ShouldBe(1);
        read.IsCompleted.ShouldBeFalse();
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

    /// <summary>
    /// Verifies a failing input disposal never abandons the output. Both owned streams are
    /// attempted exactly once and the first exception is the one the caller observes.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenBothOwnedStreamsFail_AttemptsBothAndKeepsInputFailureAsync()
    {
        var input = new FailingStream();
        var output = new FailingStream();
        var transport = new StreamTransport(input, output);

        var thrown = await Should.ThrowAsync<IOException>(async () => await transport.DisposeAsync());

        thrown.ShouldBeSameAs(input.Failure);
        input.DisposeCount.ShouldBe(1);
        output.DisposeCount.ShouldBe(1);
    }

    /// <summary>Verifies an output-only failure is reported after the input closed normally.</summary>
    [Fact]
    public async Task DisposeAsync_WhenOnlyOutputFails_ReportsOutputFailureAsync()
    {
        var input = new TrackingStream();
        var output = new FailingStream();
        var transport = new StreamTransport(input, output);

        var thrown = await Should.ThrowAsync<IOException>(async () => await transport.DisposeAsync());

        thrown.ShouldBeSameAs(output.Failure);
        input.DisposeCount.ShouldBe(1);
        output.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// Verifies a second disposal after a failed first is quiet and attempts nothing again, so a
    /// failed teardown cannot be repeated by an outer <c>await using</c>.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalledAgainAfterFailure_IsQuietAndAttemptsNothingAsync()
    {
        var input = new FailingStream();
        var output = new FailingStream();
        var transport = new StreamTransport(input, output);
        _ = await Should.ThrowAsync<IOException>(async () => await transport.DisposeAsync());

        await transport.DisposeAsync();

        input.DisposeCount.ShouldBe(1);
        output.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// Verifies a failing stream supplied as both ends is attempted exactly once, so exception
    /// handling never turns a shared resource into a double release.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenSharedStreamFails_AttemptsItExactlyOnceAsync()
    {
        var shared = new FailingStream();
        var transport = new StreamTransport(shared, shared);

        var thrown = await Should.ThrowAsync<IOException>(async () => await transport.DisposeAsync());

        thrown.ShouldBeSameAs(shared.Failure);
        shared.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// Verifies a transport that owns its input but borrows its output closes only the input,
    /// which is the ownership split an interactive host needs when it opens its own tty device
    /// alongside a process-owned standard output stream.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenInputIsOwnedAndOutputIsBorrowed_ClosesOnlyInputAsync()
    {
        var input = new TrackingStream();
        var output = new TrackingStream();
        var transport = new StreamTransport(input, output, leaveInputOpen: false, leaveOutputOpen: true);

        await transport.DisposeAsync();
        await transport.DisposeAsync();

        input.DisposeCount.ShouldBe(1);
        output.DisposeCount.ShouldBe(0);
        await output.DisposeAsync();
    }

    /// <summary>
    /// Verifies the mirrored split closes only the output, proving the two ownership flags are
    /// genuinely independent rather than one flag applied twice.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenOutputIsOwnedAndInputIsBorrowed_ClosesOnlyOutputAsync()
    {
        var input = new TrackingStream();
        var output = new TrackingStream();
        var transport = new StreamTransport(input, output, leaveInputOpen: true, leaveOutputOpen: false);

        await transport.DisposeAsync();

        input.DisposeCount.ShouldBe(0);
        output.DisposeCount.ShouldBe(1);
        await input.DisposeAsync();
    }

    /// <summary>
    /// Verifies one stream supplied as both input and output is closed exactly once no matter
    /// which side claims ownership, and is never closed when both sides borrow it.
    /// </summary>
    /// <param name="leaveInputOpen">Whether the input side borrows the shared stream.</param>
    /// <param name="leaveOutputOpen">Whether the output side borrows the shared stream.</param>
    /// <param name="expected">The exact number of disposal calls the shared stream must observe.</param>
    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 0)]
    public async Task DisposeAsync_WhenOneStreamIsBothEnds_ClosesItAtMostOnceAsync(
        bool leaveInputOpen,
        bool leaveOutputOpen,
        int expected)
    {
        var shared = new TrackingStream();
        var transport = new StreamTransport(shared, shared, leaveInputOpen, leaveOutputOpen);

        await transport.DisposeAsync();

        shared.DisposeCount.ShouldBe(expected);
        await shared.DisposeAsync();
    }

    /// <summary>
    /// Verifies the shared-flag overload keeps its documented meaning by forwarding the same
    /// decision to both streams, so existing callers observe no behavioral change.
    /// </summary>
    /// <param name="leaveOpen">Whether the transport borrows both streams.</param>
    /// <param name="expected">The exact number of disposal calls each stream must observe.</param>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task DisposeAsync_WhenSharedFlagOverloadIsUsed_AppliesItToBothStreamsAsync(
        bool leaveOpen,
        int expected)
    {
        var input = new TrackingStream();
        var output = new TrackingStream();
        var transport = new StreamTransport(input, output, leaveOpen);

        await transport.DisposeAsync();

        input.DisposeCount.ShouldBe(expected);
        output.DisposeCount.ShouldBe(expected);
        await input.DisposeAsync();
        await output.DisposeAsync();
    }

    /// <summary>
    /// Verifies a Unix device hang-up reaches the session as orderly closure rather than as a
    /// transport fault, so a terminal that disappears drives the documented shutdown path.
    /// </summary>
    /// <remarks>
    /// A real pseudoterminal only reports the hang-up as EIO on some interleavings, so this drives
    /// the exact exception .NET raises for a failing Unix read instead of racing a device.
    /// </remarks>
    [Fact]
    public async Task ReadAsync_WhenUnixInputReportsHangUp_ReturnsEndOfFileAsync()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The hang-up errno is a Unix concept.");
        await using HangUpStream input = new(_inputOutputErrorNumber);
        await using StreamTransport transport = new(input, Stream.Null);

        var read = await transport.ReadAsync(new byte[1], TestContext.Current.CancellationToken);

        read.ShouldBe(0);
    }

    /// <summary>
    /// Verifies an unrelated input failure still propagates, so the hang-up translation cannot
    /// swallow a genuine transport defect.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenUnixInputFailsForAnotherReason_PropagatesTheFailureAsync()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The hang-up errno is a Unix concept.");

        // EBADF: a descriptor defect, not a peer that went away.
        await using HangUpStream input = new(9);
        await using StreamTransport transport = new(input, Stream.Null);

        _ = await Should.ThrowAsync<IOException>(async () =>
            await transport.ReadAsync(new byte[1], TestContext.Current.CancellationToken));
    }
}
