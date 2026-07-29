// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;


/// <summary>Verifies the Unix raw-input lease and its restoration reporting.</summary>
public sealed class UnixConsoleModeTests
{
    /// <summary>Verifies unsupported hosts receive a no-op raw-input lease.</summary>
    [Fact]
    public void Enter_WhenHostIsUnsupported_DisposesWithoutThrowing()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return;
        }

        using var mode = UnixConsoleMode.Enter(captureControlKeys: false);
        _ = mode.ShouldNotBeNull();
    }

    /// <summary>
    /// Verifies a failing restoration is reported instead of silently discarded, so cleanup can no
    /// longer claim success while the terminal stays raw and echo-less.
    /// </summary>
    [Fact]
    public void Dispose_WhenRestorationFails_ThrowsTheRestorationFailure()
    {
        var failure = new IOException("stty restore failed");
        var invocations = new List<string>();
        var mode = UnixConsoleMode.Enter(captureControlKeys: false, arguments =>
        {
            invocations.Add(string.Join(' ', arguments));

            return invocations.Count > 2 ? throw failure : "saved-state";
        });

        var thrown = Should.Throw<IOException>(mode.Dispose);

        thrown.ShouldBeSameAs(failure);
        invocations.ShouldBe(["-g", "raw -echo isig", "saved-state"]);
    }

    /// <summary>
    /// Verifies a second disposal after a failed restoration is quiet and retries nothing, so an
    /// outer cleanup path cannot repeat a failed restore.
    /// </summary>
    [Fact]
    public void Dispose_WhenCalledAgainAfterFailure_IsQuietAndRetriesNothing()
    {
        var invocations = 0;
        var mode = UnixConsoleMode.Enter(captureControlKeys: false, _ =>
        {
            invocations++;

            return invocations > 2 ? throw new IOException("stty restore failed") : "saved-state";
        });
        _ = Should.Throw<IOException>(mode.Dispose);

        mode.Dispose();

        invocations.ShouldBe(3);
    }

    /// <summary>Verifies a successful restoration replays the exact captured state once.</summary>
    [Fact]
    public void Dispose_WhenRestorationSucceeds_ReplaysCapturedStateOnce()
    {
        var invocations = new List<string>();
        var mode = UnixConsoleMode.Enter(captureControlKeys: true, arguments =>
        {
            invocations.Add(string.Join(' ', arguments));

            return "saved-state";
        });

        mode.Dispose();
        mode.Dispose();

        invocations.ShouldBe(["-g", "raw -echo", "saved-state"]);
    }

    /// <summary>
    /// Verifies a failure entering raw mode still surfaces the entry exception even when the
    /// best-effort undo also fails.
    /// </summary>
    [Fact]
    public void Enter_WhenRawModeAndUndoBothFail_PreservesTheEntryFailure()
    {
        var entryFailure = new IOException("raw mode failed");
        var invocations = 0;

        var thrown = Should.Throw<IOException>(() => UnixConsoleMode.Enter(
            captureControlKeys: false,
            _ =>
            {
                invocations++;

                return invocations == 1
                    ? "saved-state"
                    : invocations == 2
                        ? throw entryFailure
                        : throw new IOException("undo failed");
            }));

        thrown.ShouldBeSameAs(entryFailure);
        invocations.ShouldBe(3);
    }
}
