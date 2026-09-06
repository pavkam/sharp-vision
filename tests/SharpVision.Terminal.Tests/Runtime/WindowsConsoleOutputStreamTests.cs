// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>Verifies the console output stream wrapper's capability and unsupported-member contracts.</summary>
/// <remarks>
/// The constructor itself performs no Windows API call, so unlike
/// <see cref="WindowsConsoleModeTests"/> this suite runs on every platform; the
/// <see cref="SupportedOSPlatformAttribute"/> only satisfies the platform-compatibility analyzer
/// for a type that is otherwise exercised solely from Windows-only production code paths. The
/// UTF-8-to-UTF-16 transcoding this stream performs on an actual write is instead covered
/// directly by <see cref="ConsoleUtf8ToUtf16TranscoderTests"/>, which needs no console handle at
/// all, and its native write loop needs a real console handle covered only by
/// <see cref="WindowsConsoleHostConPtyTests"/>.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsConsoleOutputStreamTests
{
    /// <summary>Verifies the capability flags match a forward-only, non-seekable output stream.</summary>
    [Fact]
    public void Capabilities_Always_MatchWriteOnlyNonSeekableStream()
    {
        using var stream = new WindowsConsoleOutputStream(0);

        stream.CanRead.ShouldBeFalse();
        stream.CanWrite.ShouldBeTrue();
        stream.CanSeek.ShouldBeFalse();
    }

    /// <summary>Verifies the members a forward-only stream cannot support throw instead of no-op.</summary>
    [Fact]
    public void UnsupportedMembers_WhenInvoked_ThrowNotSupportedException()
    {
        using var stream = new WindowsConsoleOutputStream(0);

        _ = Should.Throw<NotSupportedException>(() => stream.Length);
        _ = Should.Throw<NotSupportedException>(() => stream.Position);
        _ = Should.Throw<NotSupportedException>(() => stream.Position = 0);
        _ = Should.Throw<NotSupportedException>(() => stream.Read([], 0, 0));
        _ = Should.Throw<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        _ = Should.Throw<NotSupportedException>(() => stream.SetLength(0));
    }

    /// <summary>Verifies flushing never throws, since there is no separate buffering stage to flush.</summary>
    [Fact]
    public async Task Flush_Always_CompletesWithoutThrowingAsync()
    {
        using var stream = new WindowsConsoleOutputStream(0);

        Should.NotThrow(stream.Flush);
        await Should.NotThrowAsync(() => stream.FlushAsync(TestContext.Current.CancellationToken));
    }
}
