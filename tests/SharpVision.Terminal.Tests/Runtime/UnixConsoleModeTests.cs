// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using System.Buffers.Binary;

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
            setAttributes: (_, state) => RuntimeInterop.TrySetTerminalAttributes(pty.SlaveDescriptor, state),
            restoreAttributes: (_, state) => RuntimeInterop.TryRestoreTerminalAttributes(
                pty.SlaveDescriptor,
                state));

        RuntimeInterop.TryGetTerminalAttributes(pty.SlaveDescriptor, out var afterEnter).ShouldBeTrue();
        afterEnter.ShouldBe(RuntimeInterop.ComputeRawTerminalAttributes(before, captureControlKeys));

        var layout = RuntimeInterop.SelectLayout(OperatingSystem.IsMacOS());

        if (captureControlKeys)
        {
            // ISIG stays cleared, and cfmakeraw() never touches c_cc[VSUSP], so the pseudoterminal's
            // SUSP byte is whatever the kernel initialized it to (already inert with ISIG off).
            (ReadLocalFlags(afterEnter, layout) & layout.SignalsEnabledFlag).ShouldBe(0ul);
        }
        else
        {
            // ISIG is restored so Ctrl+C keeps raising SIGINT, which also re-arms SUSP unless it is
            // explicitly disabled - assert both halves of that fix directly against the real kernel
            // state, not merely against this test's own expectation of what Enter should have done.
            (ReadLocalFlags(afterEnter, layout) & layout.SignalsEnabledFlag).ShouldNotBe(0ul);
            afterEnter[layout.ControlCharactersOffset + layout.SuspendCharacterIndex]
                .ShouldBe(layout.DisabledControlCharacter);

            if (layout.DelayedSuspendCharacterIndex is int delayedSuspendCharacterIndex)
            {
                afterEnter[layout.ControlCharactersOffset + delayedSuspendCharacterIndex]
                    .ShouldBe(layout.DisabledControlCharacter);
            }
        }

        mode.Dispose();

        RuntimeInterop.TryGetTerminalAttributes(pty.SlaveDescriptor, out var afterRestore).ShouldBeTrue();
        afterRestore.ShouldBe(before);

        // The whole-buffer comparison above already proves this, but restoring exactly the
        // captured c_cc bytes - undoing the SUSP/DSUSP disablement from Enter - is the specific
        // claim this fix makes, so assert it directly too.
        afterRestore[layout.ControlCharactersOffset + layout.SuspendCharacterIndex]
            .ShouldBe(before[layout.ControlCharactersOffset + layout.SuspendCharacterIndex]);

        if (layout.DelayedSuspendCharacterIndex is int delayedSuspendCharacterIndexForRestore)
        {
            afterRestore[layout.ControlCharactersOffset + delayedSuspendCharacterIndexForRestore]
                .ShouldBe(before[layout.ControlCharactersOffset + delayedSuspendCharacterIndexForRestore]);
        }
    }

    private static ulong ReadLocalFlags(byte[] termios, RuntimeInterop.UnixTerminalLayout layout) =>
        layout.LocalFlagsWidth == 8
            ? BinaryPrimitives.ReadUInt64LittleEndian(termios.AsSpan(layout.LocalFlagsOffset))
            : BinaryPrimitives.ReadUInt32LittleEndian(termios.AsSpan(layout.LocalFlagsOffset));

    /// <summary>
    /// Verifies restoration discards an unread mouse-report tail before echo and canonical input
    /// can expose it to the shell that resumes after the application exits.
    /// </summary>
    [Fact]
    public async Task Dispose_WhenUnreadPointerTailIsPending_DiscardsItBeforeRestoringAttributesAsync()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Requires a Unix pseudoterminal.");

        await using var pty = UnixPseudoterminal.Open();
        var mode = UnixConsoleMode.Enter(
            captureControlKeys: false,
            getAttributes: _ => RuntimeInterop.TryGetTerminalAttributes(pty.SlaveDescriptor, out var state)
                ? state
                : null,
            setAttributes: (_, state) => RuntimeInterop.TrySetTerminalAttributes(pty.SlaveDescriptor, state),
            restoreAttributes: (_, state) => RuntimeInterop.TryRestoreTerminalAttributes(
                pty.SlaveDescriptor,
                state));
        var pointerTail = "35;193;5M"u8.ToArray();
        await pty.Master.WriteAsync(pointerTail, TestContext.Current.CancellationToken);
        await pty.Master.FlushAsync(TestContext.Current.CancellationToken);

        mode.Dispose();

        var received = new byte[pointerTail.Length];
        var read = pty.Slave.ReadAsync(received, TestContext.Current.CancellationToken).AsTask();
        var boundary = Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        var completed = await Task.WhenAny(read, boundary);
        var leaked = ReferenceEquals(completed, read);
        await pty.CloseMasterAsync();

        if (!read.IsCompleted)
        {
            try
            {
                _ = await read;
            }
            catch (IOException)
            {
                // Closing a PTY master may report EOF or EIO on its blocked slave read.
            }
        }

        leaked.ShouldBeFalse("the unread pointer tail survived terminal restoration");
    }
}
