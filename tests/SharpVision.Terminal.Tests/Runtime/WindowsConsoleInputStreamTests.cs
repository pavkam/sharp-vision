// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>Verifies the console input stream wrapper's capability and unsupported-member contracts.</summary>
/// <remarks>
/// The constructor itself performs no Windows API call, so unlike
/// <see cref="WindowsConsoleModeTests"/> this suite runs on every platform; the
/// <see cref="SupportedOSPlatformAttribute"/> only satisfies the platform-compatibility analyzer
/// for a type that is otherwise exercised solely from Windows-only production code paths. The
/// UTF-16-to-UTF-8 transcoding this stream performs on an actual read is instead covered directly
/// by <see cref="ConsoleUtf16ToUtf8TranscoderTests"/>, which needs no console handle at all.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsConsoleInputStreamTests
{
    /// <summary>Verifies the capability flags match a forward-only, non-seekable input stream.</summary>
    [Fact]
    public void Capabilities_Always_MatchReadOnlyNonSeekableStream()
    {
        using var stream = new WindowsConsoleInputStream(0);

        stream.CanRead.ShouldBeTrue();
        stream.CanWrite.ShouldBeFalse();
        stream.CanSeek.ShouldBeFalse();
    }

    /// <summary>Verifies the members a forward-only stream cannot support throw instead of no-op.</summary>
    [Fact]
    public void UnsupportedMembers_WhenInvoked_ThrowNotSupportedException()
    {
        using var stream = new WindowsConsoleInputStream(0);

        _ = Should.Throw<NotSupportedException>(() => stream.Length);
        _ = Should.Throw<NotSupportedException>(() => stream.Position);
        _ = Should.Throw<NotSupportedException>(() => stream.Position = 0);
        _ = Should.Throw<NotSupportedException>(stream.Flush);
        _ = Should.Throw<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        _ = Should.Throw<NotSupportedException>(() => stream.SetLength(0));
        _ = Should.Throw<NotSupportedException>(() => stream.Write([], 0, 0));
    }

    /// <summary>
    /// Verifies a cancellation that lands before the pooled thread reaches the native call is
    /// caught by the stream's own pre-call check, so that call is never issued. This is the
    /// narrower race window the cancellation registration's pending-I/O abort alone cannot close,
    /// because it has no pending I/O left to abort until the native call has actually started.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenCancelledBeforeNativeReadStarts_ThrowsWithoutInvokingNativeReadAsync()
    {
        var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ReadConsoleDelegate fakeRead;

        unsafe
        {
            fakeRead = (_, _, _, out charsRead) =>
            {
                _ = invoked.TrySetResult();
                charsRead = 0;

                // A real ReadConsoleW blocks until input arrives; sleeping here rather than
                // returning immediately turns a lost race into an unmistakable failure (the
                // bounded wait below elapses first) instead of a silent false pass, without
                // leaking a blocked thread forever if that ever happens.
                Thread.Sleep(TimeSpan.FromSeconds(5));
                return true;
            };
        }

        using var stream = new WindowsConsoleInputStream(0, fakeRead, static _ => true);
        using var cts = new CancellationTokenSource();
        var buffer = new byte[4];

        var read = stream.ReadAsync(buffer, cts.Token).AsTask();
        cts.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => read.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        invoked.Task.IsCompleted.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies a cancellation that lands while the native read is already in flight still
    /// cancels the read, via the registration's pending-I/O cancellation path rather than the
    /// pre-call check that only helps when the call has not started yet.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenCancelledWhileNativeReadIsInFlight_ThrowsViaCancelPendingIoAsync()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelPendingIoCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ReadConsoleDelegate fakeRead;

        unsafe
        {
            fakeRead = (_, _, _, out charsRead) =>
            {
                _ = started.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                charsRead = 0;
                return false;
            };
        }

        bool CancelPendingIo(nint handle)
        {
            _ = handle;
            _ = cancelPendingIoCalled.TrySetResult();
            _ = release.TrySetResult();
            return true;
        }

        using var stream = new WindowsConsoleInputStream(0, fakeRead, CancelPendingIo);
        using var cts = new CancellationTokenSource();
        var buffer = new byte[4];

        var read = stream.ReadAsync(buffer, cts.Token).AsTask();
        await started.Task;
        cts.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => read);
        cancelPendingIoCalled.Task.IsCompleted.ShouldBeTrue();
    }
}
