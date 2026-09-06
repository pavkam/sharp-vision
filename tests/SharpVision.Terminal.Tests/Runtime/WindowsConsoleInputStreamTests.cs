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
}
