// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using RuntimeNative = Terminal.Runtime.Native;

/// <summary>Verifies the Windows console lease restores both handles and reports failures.</summary>
/// <remarks>
/// These tests need a real console to enter the lease, so they skip elsewhere. The console-mode
/// write boundary is injected because a genuine <c>SetConsoleMode</c> failure cannot be provoked
/// against a live console without corrupting it.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsConsoleModeTests
{
    /// <summary>Verifies a failing input restore still attempts the output handle.</summary>
    [Fact]
    public void Dispose_WhenInputRestoreFails_StillAttemptsOutputAndThrows()
    {
        SkipWithoutWindowsConsole();
        var restores = new List<nint>();
        var entered = 0;
        using var mode = Enter((handle, value) =>
        {
            _ = value;

            if (entered < 2)
            {
                entered++;

                return true;
            }

            restores.Add(handle);

            return restores.Count != 1;
        });

        _ = Should.Throw<IOException>(mode.Dispose);

        restores.Count.ShouldBe(2);
        restores[0].ShouldNotBe(restores[1]);
    }

    /// <summary>Verifies a failing output restore is reported after input restored normally.</summary>
    [Fact]
    public void Dispose_WhenOutputRestoreFails_ReportsFailureAfterRestoringInput()
    {
        SkipWithoutWindowsConsole();
        var restores = 0;
        var entered = 0;
        using var mode = Enter((_, _) =>
        {
            if (entered < 2)
            {
                entered++;

                return true;
            }

            restores++;

            return restores != 2;
        });

        _ = Should.Throw<IOException>(mode.Dispose);

        restores.ShouldBe(2);
    }

    /// <summary>Verifies repeated disposal after a failed restore is quiet and retries nothing.</summary>
    [Fact]
    public void Dispose_WhenCalledAgainAfterFailure_IsQuietAndRetriesNothing()
    {
        SkipWithoutWindowsConsole();
        var restores = 0;
        var entered = 0;
        using var mode = Enter((_, _) =>
        {
            if (entered < 2)
            {
                entered++;

                return true;
            }

            restores++;

            return false;
        });
        _ = Should.Throw<IOException>(mode.Dispose);

        mode.Dispose();

        restores.ShouldBe(2);
    }

    private static WindowsConsoleMode Enter(Func<nint, uint, bool> setConsoleMode) =>
        WindowsConsoleMode.Enter(captureControlKeys: false, setConsoleMode);

    private static void SkipWithoutWindowsConsole()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows console lease requires Windows.");
        Assert.SkipUnless(
            RuntimeNative.TryGetConsoleMode(RuntimeNative.GetStandardHandle(RuntimeNative.StdInputHandle), out _),
            "The Windows console lease requires a real console.");
    }
}
