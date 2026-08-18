// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Tests.Support;

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
    public void Dispose_WhenRestorationFails_ThrowsAnIOException()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Requires Unix termios math.");

        var setInvocations = 0;
        var mode = UnixConsoleMode.Enter(
            captureControlKeys: false,
            getAttributes: static _ => new byte[RuntimeInterop.TermiosStateLength],
            setAttributes: (_, _) =>
            {
                setInvocations++;
                return setInvocations <= 1;
            });

        _ = Should.Throw<IOException>(mode.Dispose);

        setInvocations.ShouldBe(2);
    }

    /// <summary>
    /// Verifies a second disposal after a failed restoration is quiet and retries nothing, so an
    /// outer cleanup path cannot repeat a failed restore.
    /// </summary>
    [Fact]
    public void Dispose_WhenCalledAgainAfterFailure_IsQuietAndRetriesNothing()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Requires Unix termios math.");

        var setInvocations = 0;
        var mode = UnixConsoleMode.Enter(
            captureControlKeys: false,
            getAttributes: static _ => new byte[RuntimeInterop.TermiosStateLength],
            setAttributes: (_, _) =>
            {
                setInvocations++;
                return setInvocations <= 1;
            });
        _ = Should.Throw<IOException>(mode.Dispose);

        mode.Dispose();

        setInvocations.ShouldBe(2);
    }

    /// <summary>Verifies a successful restoration replays the exact captured state once.</summary>
    [Fact]
    public void Dispose_WhenRestorationSucceeds_ReplaysCapturedStateOnce()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Requires Unix termios math.");

        var captured = new byte[RuntimeInterop.TermiosStateLength];
        var replayed = new List<byte[]>();
        var mode = UnixConsoleMode.Enter(
            captureControlKeys: true,
            getAttributes: _ => captured,
            setAttributes: (_, state) =>
            {
                replayed.Add(state);
                return true;
            });

        // Enter itself writes the derived raw-mode state once; only the writes from here on are
        // Dispose's restoration replays.
        replayed.Clear();

        mode.Dispose();
        mode.Dispose();

        replayed.ShouldBe([captured]);
    }

    /// <summary>
    /// Verifies a failure entering raw mode still surfaces the entry exception even when the
    /// best-effort undo also fails.
    /// </summary>
    [Fact]
    public void Enter_WhenRawModeAndUndoBothFail_PreservesTheEntryFailure()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Requires Unix termios math.");

        var setInvocations = 0;

        var thrown = Should.Throw<IOException>(() => UnixConsoleMode.Enter(
            captureControlKeys: false,
            getAttributes: static _ => new byte[RuntimeInterop.TermiosStateLength],
            setAttributes: (_, _) =>
            {
                setInvocations++;
                return false;
            }));

        thrown.Message.ShouldContain("raw mode");
        setInvocations.ShouldBe(2);
    }

    /// <summary>
    /// Verifies a failure reading the initial state surfaces as the same failure the old
    /// stty-based lease reported for an unreadable terminal.
    /// </summary>
    [Fact]
    public void Enter_WhenAttributesCannotBeRead_ThrowsAnIOException()
    {
        _ = Should.Throw<IOException>(() => UnixConsoleMode.Enter(
            captureControlKeys: false,
            getAttributes: static _ => null,
            setAttributes: static (_, _) => true));
    }

    /// <summary>
    /// Verifies entry against a fresh pseudoterminal actually clears canonical mode and echo, sets
    /// or clears ISIG per <c>captureControlKeys</c>, and that disposal restores the exact captured
    /// termios state byte-for-byte - proving the syscall-based lease behaves like the stty
    /// invocations it replaces, without spawning a subprocess.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Enter_OnAFreshPseudoterminal_EntersAndRestoresRawModeByDirectSyscallAsync(
        bool captureControlKeys)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Requires a Unix pseudoterminal.");

        await using var pty = UnixPseudoterminal.Open();

        RuntimeInterop.TryGetTerminalAttributes(pty.SlaveDescriptor, out var before).ShouldBeTrue();

        var mode = UnixConsoleMode.Enter(
            captureControlKeys,
            getAttributes: _ => RuntimeInterop.TryGetTerminalAttributes(pty.SlaveDescriptor, out var state)
                ? state
                : null,
            setAttributes: (_, state) => RuntimeInterop.TrySetTerminalAttributes(pty.SlaveDescriptor, state));

        RuntimeInterop.TryGetTerminalAttributes(pty.SlaveDescriptor, out var afterEnter).ShouldBeTrue();
        afterEnter.ShouldBe(RuntimeInterop.ComputeRawTerminalAttributes(before, captureControlKeys));

        mode.Dispose();

        RuntimeInterop.TryGetTerminalAttributes(pty.SlaveDescriptor, out var afterRestore).ShouldBeTrue();
        afterRestore.ShouldBe(before);
    }
}
