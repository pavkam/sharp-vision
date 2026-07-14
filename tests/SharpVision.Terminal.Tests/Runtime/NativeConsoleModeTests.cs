// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Runtime;

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
        uint current = Native.EnableProcessedInput | Native.EnableLineInput | Native.EnableEchoInput;

        uint result = Native.ComputeInputMode(current, captureControlKeys: false);

        (result & Native.EnableVirtualTerminalInput).ShouldNotBe(0u);
        (result & Native.EnableLineInput).ShouldBe(0u);
        (result & Native.EnableEchoInput).ShouldBe(0u);
        (result & Native.EnableProcessedInput).ShouldNotBe(0u); // signals still processed
    }

    /// <summary>
    /// Verifies capturing control keys clears processed input while keeping VT input.
    /// </summary>
    [Fact]
    public void ComputeInputMode_WhenCapturingControlKeys_ClearsProcessedInput()
    {
        uint current = Native.EnableProcessedInput | Native.EnableLineInput | Native.EnableEchoInput;

        uint result = Native.ComputeInputMode(current, captureControlKeys: true);

        (result & Native.EnableProcessedInput).ShouldBe(0u);
        (result & Native.EnableVirtualTerminalInput).ShouldNotBe(0u);
    }

    /// <summary>
    /// Verifies the default output mode enables VT processing and disables auto-return.
    /// </summary>
    [Fact]
    public void ComputeOutputMode_WhenDefault_EnablesVtProcessingAndDisablesAutoReturn()
    {
        uint result = Native.ComputeOutputMode(Native.EnableProcessedOutput);

        (result & Native.EnableVirtualTerminalProcessing).ShouldNotBe(0u);
        (result & Native.DisableNewlineAutoReturn).ShouldNotBe(0u);
        (result & Native.EnableProcessedOutput).ShouldNotBe(0u);
    }
}
