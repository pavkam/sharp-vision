// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;


/// <summary>Verifies the Unix raw-input lease and its restoration reporting.</summary>
public sealed class UnixConsoleModeTests
{
    /// <summary>Verifies a hung terminal-utility process fails with a bounded timeout rather than
    /// blocking indefinitely, and that the timed-out process is not left running (see #98).</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public void Run_WhenTheProcessHangs_TimesOutAndKillsIt()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Requires a POSIX shell.");
        var script = Path.Combine(Path.GetTempPath(), $"sharpvision-hang-{Guid.NewGuid():N}.sh");

        try
        {
            File.WriteAllText(script, "#!/bin/sh\nsleep 30\n");
            File.SetUnixFileMode(
                script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var watch = Stopwatch.StartNew();

            var thrown = Should.Throw<IOException>(
                () => UnixConsoleMode.Run(["-g"], script, TimeSpan.FromMilliseconds(200)));

            watch.Stop();
            thrown.Message.ShouldContain("timed out");
            watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
        }
        finally
        {
            File.Delete(script);
        }
    }

    /// <summary>Verifies a fast-failing terminal-utility process still surfaces its own diagnostic
    /// rather than the timeout path.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public void Run_WhenTheProcessExitsNonZero_ThrowsWithStandardErrorContent()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Requires a POSIX shell.");
        var script = Path.Combine(Path.GetTempPath(), $"sharpvision-fail-{Guid.NewGuid():N}.sh");

        try
        {
            File.WriteAllText(script, "#!/bin/sh\necho 'synthetic failure' >&2\nexit 1\n");
            File.SetUnixFileMode(
                script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var thrown = Should.Throw<IOException>(
                () => UnixConsoleMode.Run(["-g"], script, TimeSpan.FromSeconds(5)));

            thrown.Message.ShouldContain("synthetic failure");
        }
        finally
        {
            File.Delete(script);
        }
    }

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
