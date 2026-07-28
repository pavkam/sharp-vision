// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.



namespace SharpVision.Terminal.Tests.Runtime;

using RuntimeNative = Terminal.Runtime.Native;


/// <summary>
/// Verifies the pure bit-math behind the Windows console-mode boundary.
/// </summary>
public sealed class NativeConsoleModeTests
{
    /// <summary>
    /// Verifies the default mode enables VT input and clears line/echo input.
    /// </summary>
    [Fact]
    public void ComputeInputMode_WhenDefault_EnablesVtInputAndClearsLineAndEcho()
    {
        var current = RuntimeNative.EnableProcessedInput | RuntimeNative.EnableLineInput | RuntimeNative.EnableEchoInput;

        var result = RuntimeNative.ComputeInputMode(current, captureControlKeys: false);

        (result & RuntimeNative.EnableVirtualTerminalInput).ShouldNotBe(0u);
        (result & RuntimeNative.EnableLineInput).ShouldBe(0u);
        (result & RuntimeNative.EnableEchoInput).ShouldBe(0u);
        (result & RuntimeNative.EnableProcessedInput).ShouldNotBe(0u); // signals still processed
    }

    /// <summary>
    /// Verifies capturing control keys clears processed input while keeping VT input.
    /// </summary>
    [Fact]
    public void ComputeInputMode_WhenCapturingControlKeys_ClearsProcessedInput()
    {
        var current = RuntimeNative.EnableProcessedInput | RuntimeNative.EnableLineInput | RuntimeNative.EnableEchoInput;

        var result = RuntimeNative.ComputeInputMode(current, captureControlKeys: true);

        (result & RuntimeNative.EnableProcessedInput).ShouldBe(0u);
        (result & RuntimeNative.EnableVirtualTerminalInput).ShouldNotBe(0u);
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
        var result = RuntimeNative.ComputeOutputMode(unrelatedSavedMode);

        // Assert
        (result & RuntimeNative.EnableProcessedOutput).ShouldNotBe(0u);
        (result & RuntimeNative.EnableWrapAtEolOutput).ShouldNotBe(0u);
        (result & RuntimeNative.EnableVirtualTerminalProcessing).ShouldNotBe(0u);
        (result & RuntimeNative.DisableNewlineAutoReturn).ShouldNotBe(0u);
        (result & unrelatedSavedMode).ShouldBe(unrelatedSavedMode);
    }
}
