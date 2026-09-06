// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.



namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Tests.Support;

/// <summary>
/// Verifies the pure bit-math behind the Windows console-mode boundary.
/// </summary>
public sealed class RuntimeInteropTests
{
    /// <summary>Verifies terminal identity is descriptor-specific, so separate ttys cannot be
    /// treated as one interactive console merely because each descriptor is a tty.</summary>
    [Fact]
    public async Task TerminalDevicesMatch_WhenDescriptorsNameSameOrDifferentPtys_DistinguishesIdentityAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Terminal device identity requires Unix pseudoterminals.");
        await using var first = UnixPseudoterminal.Open();
        await using var second = UnixPseudoterminal.Open();

        RuntimeInterop.TerminalDevicesMatch(first.SlaveDescriptor, first.SlaveDescriptor).ShouldBeTrue();
        RuntimeInterop.TerminalDevicesMatch(first.SlaveDescriptor, second.SlaveDescriptor).ShouldBeFalse();
    }

    /// <summary>Verifies the controlling-terminal alias identifies the same terminal as standard
    /// input and output even when the operating system reports a different pathname for it.</summary>
    [Fact]
    public void TerminalDevicesMatch_WhenControllingTerminalUsesAlias_TreatsDescriptorsAsSame()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Terminal device identity requires Unix terminal descriptors.");
        Assert.SkipUnless(
            RuntimeInterop.TerminalDevicesMatch(
                RuntimeInterop.StandardInputFileDescriptor,
                RuntimeInterop.StandardOutputFileDescriptor),
            "The test process requires standard input and output on the same terminal.");
        using var controllingTerminal = new FileStream(
            "/dev/tty",
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        var descriptor = (int) controllingTerminal.SafeFileHandle.DangerousGetHandle();

        RuntimeInterop.TerminalDevicesMatch(
            RuntimeInterop.StandardInputFileDescriptor,
            descriptor).ShouldBeTrue();
        RuntimeInterop.TerminalDevicesMatch(
            RuntimeInterop.StandardOutputFileDescriptor,
            descriptor).ShouldBeTrue();
    }

    /// <summary>Verifies different reported paths still identify one terminal when POSIX reports
    /// the same controlling session, while different sessions remain distinct.</summary>
    [Fact]
    public void TerminalIdentitiesMatch_WhenPathsAliasControllingSession_UsesSessionIdentity()
    {
        var actualPath = "/dev/ttys003"u8;
        var controllingAlias = "/dev/tty"u8;

        RuntimeInterop.TerminalIdentitiesMatch(
            actualPath,
            firstSessionId: 42,
            controllingAlias,
            secondSessionId: 42).ShouldBeTrue();
        RuntimeInterop.TerminalIdentitiesMatch(
            actualPath,
            firstSessionId: 42,
            controllingAlias,
            secondSessionId: 84).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies the default mode enables VT input and clears line/echo input.
    /// </summary>
    [Fact]
    public void ComputeInputMode_WhenDefault_EnablesVtInputAndClearsLineAndEcho()
    {
        var current = RuntimeInterop.EnableProcessedInput | RuntimeInterop.EnableLineInput | RuntimeInterop.EnableEchoInput;

        var result = RuntimeInterop.ComputeInputMode(current, captureControlKeys: false, enableMouseInput: false);

        (result & RuntimeInterop.EnableVirtualTerminalInput).ShouldNotBe(0u);
        (result & RuntimeInterop.EnableLineInput).ShouldBe(0u);
        (result & RuntimeInterop.EnableEchoInput).ShouldBe(0u);
        (result & RuntimeInterop.EnableProcessedInput).ShouldNotBe(0u); // signals still processed
    }

    /// <summary>
    /// Verifies capturing control keys clears processed input while keeping VT input.
    /// </summary>
    [Fact]
    public void ComputeInputMode_WhenCapturingControlKeys_ClearsProcessedInput()
    {
        var current = RuntimeInterop.EnableProcessedInput | RuntimeInterop.EnableLineInput | RuntimeInterop.EnableEchoInput;

        var result = RuntimeInterop.ComputeInputMode(current, captureControlKeys: true, enableMouseInput: false);

        (result & RuntimeInterop.EnableProcessedInput).ShouldBe(0u);
        (result & RuntimeInterop.EnableVirtualTerminalInput).ShouldNotBe(0u);
    }

    /// <summary>
    /// Verifies QuickEdit is always cleared and extended flags are always set, regardless of
    /// the other parameters, because a stray QuickEdit selection freezes the console outright.
    /// </summary>
    [Fact]
    public void ComputeInputMode_Always_ClearsQuickEditAndSetsExtendedFlags()
    {
        var current = RuntimeInterop.EnableQuickEditMode;

        var result = RuntimeInterop.ComputeInputMode(current, captureControlKeys: true, enableMouseInput: true);

        (result & RuntimeInterop.EnableQuickEditMode).ShouldBe(0u);
        (result & RuntimeInterop.EnableExtendedFlags).ShouldNotBe(0u);
    }

    /// <summary>
    /// Verifies mouse input is enabled only when mouse tracking is requested.
    /// </summary>
    [Fact]
    public void ComputeInputMode_WhenMouseInputRequested_SetsEnableMouseInput()
    {
        const uint current = 0;

        var result = RuntimeInterop.ComputeInputMode(current, captureControlKeys: false, enableMouseInput: true);

        (result & RuntimeInterop.EnableMouseInput).ShouldNotBe(0u);
    }

    /// <summary>
    /// Verifies mouse input is left disabled when mouse tracking is not requested.
    /// </summary>
    [Fact]
    public void ComputeInputMode_WhenMouseInputNotRequested_LeavesEnableMouseInputClear()
    {
        const uint current = 0;

        var result = RuntimeInterop.ComputeInputMode(current, captureControlKeys: false, enableMouseInput: false);

        (result & RuntimeInterop.EnableMouseInput).ShouldBe(0u);
    }

    /// <summary>
    /// Verifies output setup establishes wrapping, VT processing, and delayed auto-return.
    /// </summary>
    [Fact]
    public void ComputeOutputMode_WhenWrapWasDisabled_EnablesVtWrappingAndDelayedAutoReturn()
    {
        // Arrange
        const uint unrelatedSavedMode = 0x4000_0000;

        // Act
        var result = RuntimeInterop.ComputeOutputMode(unrelatedSavedMode);

        // Assert
        (result & RuntimeInterop.EnableProcessedOutput).ShouldNotBe(0u);
        (result & RuntimeInterop.EnableWrapAtEolOutput).ShouldNotBe(0u);
        (result & RuntimeInterop.EnableVirtualTerminalProcessing).ShouldNotBe(0u);
        (result & RuntimeInterop.DisableNewlineAutoReturn).ShouldNotBe(0u);
        (result & unrelatedSavedMode).ShouldBe(unrelatedSavedMode);
    }

    /// <summary>
    /// Verifies the pure termios/window-size layout selection returns the exact documented tuple
    /// for macOS, the generic Linux architectures, and Linux PowerPC (ppc64le) - the one Linux
    /// architecture whose ABI diverges from every other supported target - without depending on
    /// which architecture the test process itself happens to run on.
    /// </summary>
    [Fact]
    public void SelectLayout_ForKnownPlatformsAndArchitectures_ReturnsTheDocumentedTuples()
    {
        var macOs = RuntimeInterop.SelectLayout(isMacOs: true, Architecture.Arm64);
        ((ulong) macOs.WindowSizeRequest).ShouldBe(0ul);
        macOs.TermiosStateLength.ShouldBe(72);
        macOs.LocalFlagsOffset.ShouldBe(24);
        macOs.LocalFlagsWidth.ShouldBe(8);
        macOs.SignalsEnabledFlag.ShouldBe(0x0000_0080ul);
        macOs.ControlCharactersOffset.ShouldBe(32);
        macOs.SuspendCharacterIndex.ShouldBe(10);
        macOs.DelayedSuspendCharacterIndex.ShouldBe(11);
        macOs.DisabledControlCharacter.ShouldBe((byte) 0xff);

        // macOS ships no ppc64/ppc64le runtime, so the same tuple applies regardless of the
        // architecture argument.
        RuntimeInterop.SelectLayout(isMacOs: true, Architecture.X64).ShouldBe(macOs);

        var genericLinuxX64 = RuntimeInterop.SelectLayout(isMacOs: false, Architecture.X64);
        ((ulong) genericLinuxX64.WindowSizeRequest).ShouldBe(0x5413ul);
        genericLinuxX64.TermiosStateLength.ShouldBe(60);
        genericLinuxX64.LocalFlagsOffset.ShouldBe(12);
        genericLinuxX64.LocalFlagsWidth.ShouldBe(4);
        genericLinuxX64.SignalsEnabledFlag.ShouldBe(0x0000_0001ul);
        genericLinuxX64.ControlCharactersOffset.ShouldBe(17);
        genericLinuxX64.SuspendCharacterIndex.ShouldBe(10);
        genericLinuxX64.DelayedSuspendCharacterIndex.ShouldBeNull();
        genericLinuxX64.DisabledControlCharacter.ShouldBe((byte) 0);

        // Every non-ppc64le Linux architecture (x64, arm64, and everything else) shares one tuple.
        RuntimeInterop.SelectLayout(isMacOs: false, Architecture.Arm64).ShouldBe(genericLinuxX64);

        var ppc64Le = RuntimeInterop.SelectLayout(isMacOs: false, Architecture.Ppc64le);
        ((ulong) ppc64Le.WindowSizeRequest).ShouldBe(0x40087468ul);
        ppc64Le.TermiosStateLength.ShouldBe(44);
        ppc64Le.LocalFlagsOffset.ShouldBe(12);
        ppc64Le.LocalFlagsWidth.ShouldBe(4);
        ppc64Le.SignalsEnabledFlag.ShouldBe(0x0000_0080ul);
        ppc64Le.ControlCharactersOffset.ShouldBe(16);
        ppc64Le.SuspendCharacterIndex.ShouldBe(12);
        ppc64Le.DelayedSuspendCharacterIndex.ShouldBeNull();
        ppc64Le.DisabledControlCharacter.ShouldBe((byte) 0);
    }

    /// <summary>
    /// Verifies that restoring ISIG (the default <c>captureControlKeys: false</c> raw-mode shape)
    /// also disables the SUSP character (and DSUSP on macOS) at the exact byte offsets the current
    /// platform's layout declares, so Ctrl+Z arrives as an ordinary input byte instead of stopping
    /// the process while it is still raw and on the alternate screen.
    /// </summary>
    [Fact]
    public void ComputeRawTerminalAttributes_WhenCaptureControlKeysIsFalse_DisablesTheSuspendCharacters()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Requires Unix termios math.");

        var layout = RuntimeInterop.SelectLayout(OperatingSystem.IsMacOS(), RuntimeInformation.ProcessArchitecture);
        var captured = new byte[RuntimeInterop.TermiosStateLength];
        captured[layout.ControlCharactersOffset + layout.SuspendCharacterIndex] = 0x1a;

        if (layout.DelayedSuspendCharacterIndex is int delayedSuspendCharacterIndex)
        {
            captured[layout.ControlCharactersOffset + delayedSuspendCharacterIndex] = 0x19;
        }

        var raw = RuntimeInterop.ComputeRawTerminalAttributes(captured, captureControlKeys: false);

        raw[layout.ControlCharactersOffset + layout.SuspendCharacterIndex]
            .ShouldBe(layout.DisabledControlCharacter);

        if (layout.DelayedSuspendCharacterIndex is int delayedSuspendCharacterIndexAssertion)
        {
            raw[layout.ControlCharactersOffset + delayedSuspendCharacterIndexAssertion]
                .ShouldBe(layout.DisabledControlCharacter);
        }
    }

    /// <summary>
    /// Verifies that when the caller wants Ctrl-key combinations delivered as ordinary input bytes
    /// (<c>captureControlKeys: true</c>), the SUSP/DSUSP bytes are left exactly as captured - ISIG
    /// is already cleared in that shape, which already leaves the SUSP character inert, so nothing
    /// needs to touch it.
    /// </summary>
    [Fact]
    public void ComputeRawTerminalAttributes_WhenCaptureControlKeysIsTrue_LeavesTheSuspendCharactersUntouched()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Requires Unix termios math.");

        var layout = RuntimeInterop.SelectLayout(OperatingSystem.IsMacOS(), RuntimeInformation.ProcessArchitecture);
        var captured = new byte[RuntimeInterop.TermiosStateLength];
        captured[layout.ControlCharactersOffset + layout.SuspendCharacterIndex] = 0x1a;

        if (layout.DelayedSuspendCharacterIndex is int delayedSuspendCharacterIndex)
        {
            captured[layout.ControlCharactersOffset + delayedSuspendCharacterIndex] = 0x19;
        }

        var raw = RuntimeInterop.ComputeRawTerminalAttributes(captured, captureControlKeys: true);

        raw[layout.ControlCharactersOffset + layout.SuspendCharacterIndex].ShouldBe((byte) 0x1a);

        if (layout.DelayedSuspendCharacterIndex is int delayedSuspendCharacterIndexAssertion)
        {
            raw[layout.ControlCharactersOffset + delayedSuspendCharacterIndexAssertion].ShouldBe((byte) 0x19);
        }
    }
}
