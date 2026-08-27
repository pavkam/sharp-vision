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

    /// <summary>
    /// Verifies the default mode enables VT input and clears line/echo input.
    /// </summary>
    [Fact]
    public void ComputeInputMode_WhenDefault_EnablesVtInputAndClearsLineAndEcho()
    {
        var current = RuntimeInterop.EnableProcessedInput | RuntimeInterop.EnableLineInput | RuntimeInterop.EnableEchoInput;

        var result = RuntimeInterop.ComputeInputMode(current, captureControlKeys: false);

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

        var result = RuntimeInterop.ComputeInputMode(current, captureControlKeys: true);

        (result & RuntimeInterop.EnableProcessedInput).ShouldBe(0u);
        (result & RuntimeInterop.EnableVirtualTerminalInput).ShouldNotBe(0u);
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
}
