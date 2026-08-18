// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>Verifies construction guards for the console input stream wrapper.</summary>
/// <remarks>
/// The constructor itself performs no Windows API call, so unlike
/// <see cref="WindowsConsoleModeTests"/> this suite runs on every platform; the
/// <see cref="SupportedOSPlatformAttribute"/> only satisfies the platform-compatibility analyzer
/// for a type that is otherwise exercised solely from Windows-only production code paths.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsConsoleInputStreamTests
{
    /// <summary>Verifies a null inner stream is rejected instead of failing later inside a read.</summary>
    [Fact]
    public void Constructor_WhenInnerIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new WindowsConsoleInputStream(null!, 0));
}
