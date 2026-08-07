// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;

/// <summary>
/// Verifies host text is written straight to a raw file descriptor without ever touching
/// <see cref="Console"/>, whose first write side-effects application-keypad-mode bytes on Unix
/// and leaves them re-emitted on every later child-process exit.
/// </summary>
public sealed class ConsoleTextChannelTests
{
    /// <summary>
    /// Verifies <see cref="ConsoleTextChannel.WriteRawLine"/> writes the exact UTF-8 line, with a
    /// trailing newline, to whatever descriptor it is given - the same mechanism
    /// <see cref="ConsoleTextChannel.WriteLine"/> and <see cref="ConsoleTextChannel.WriteErrorLine"/>
    /// use for standard output and standard error, proven here against an arbitrary descriptor
    /// instead of the shared test process's own stdout/stderr.
    /// </summary>
    [Fact]
    public void WriteRawLine_WhenGivenAnArbitraryDescriptor_WritesTheExactLine()
    {
        Assert.SkipUnless(!OperatingSystem.IsWindows(), "Raw file-descriptor writes are Unix-only.");

        var path = Path.Combine(Path.GetTempPath(), $"sharpvision-console-text-channel-{Guid.NewGuid():N}.txt");

        try
        {
            using (var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                ConsoleTextChannel.WriteRawLine((int) target.SafeFileHandle.DangerousGetHandle(), "hello raw fd");
            }

            File.ReadAllText(path).ShouldBe($"hello raw fd{Environment.NewLine}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies a null line is rejected before any write is attempted.</summary>
    [Fact]
    public void WriteRawLine_WhenTextIsNull_ThrowsArgumentNullException()
    {
        Assert.SkipUnless(!OperatingSystem.IsWindows(), "Raw file-descriptor writes are Unix-only.");

        _ = Should.Throw<ArgumentNullException>(() => ConsoleTextChannel.WriteRawLine(1, null!));
    }

    /// <summary>Verifies the raw path is refused on Windows, where it would corrupt the CRT descriptor table.</summary>
    [Fact]
    public void WriteRawLine_WhenPlatformIsWindows_ThrowsPlatformNotSupportedException()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only meaningful on Windows.");

        _ = Should.Throw<PlatformNotSupportedException>(() => ConsoleTextChannel.WriteRawLine(1, "text"));
    }
}
