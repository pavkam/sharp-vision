// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using System.Reflection;

/// <summary>Verifies the Windows console lease restores both handles and reports failures.</summary>
/// <remarks>
/// Most of these tests need a real console to enter the lease, so they skip elsewhere. The
/// console-mode write boundary is injected because a genuine <c>SetConsoleMode</c> failure cannot
/// be provoked against a live console without corrupting it.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsConsoleModeTests
{
    private const nint _inputHandle = 0x1;
    private const nint _outputHandle = 0x2;
    private const uint _savedInputMode = 0x10;
    private const uint _savedOutputMode = 0x20;

    /// <summary>Verifies the success path restores output before input, mirroring Enter's own LIFO unwind.</summary>
    /// <remarks>
    /// <see cref="WindowsConsoleMode.Dispose"/> never calls into Win32 directly - only the
    /// injected write boundary - so this test builds the lease via its private constructor
    /// instead of through <see cref="WindowsConsoleMode.Enter"/>, which requires a real console.
    /// That lets the restoration order be verified without a Windows host or a live console.
    /// </remarks>
    [Fact]
    public void Dispose_OnSuccess_RestoresOutputBeforeInput()
    {
        var order = new List<nint>();
        using var mode = CreateForDisposeOnly((handle, _) =>
        {
            order.Add(handle);

            return true;
        });

        mode.Dispose();

        order.ShouldBe(new[] { _outputHandle, _inputHandle });
    }

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

    /// <summary>
    /// Builds a lease directly via its private constructor, bypassing <see cref="WindowsConsoleMode.Enter"/>
    /// and the real console handles/modes it reads. Only <see cref="WindowsConsoleMode.Dispose"/> is under
    /// test here, and it touches nothing but the injected write boundary.
    /// </summary>
    private static WindowsConsoleMode CreateForDisposeOnly(Func<nint, uint, bool> setConsoleMode)
    {
        var constructor = typeof(WindowsConsoleMode)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single();

        return (WindowsConsoleMode) constructor.Invoke(
        [
            _inputHandle, _outputHandle, _savedInputMode, _savedOutputMode, setConsoleMode,
        ]);
    }

    private static void SkipWithoutWindowsConsole()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows console lease requires Windows.");
        Assert.SkipUnless(
            RuntimeInterop.TryGetConsoleMode(RuntimeInterop.GetStandardHandle(RuntimeInterop.StdInputHandle), out _),
            "The Windows console lease requires a real console.");
    }
}
