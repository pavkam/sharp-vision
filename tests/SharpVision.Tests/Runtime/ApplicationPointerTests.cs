// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies the application-owned pointer snapshot and focus state.</summary>
public sealed class ApplicationPointerTests
{
    /// <summary>Verifies the pointer snapshot is non-null and unobserved before any input.</summary>
    [Fact]
    public async Task Pointer_WhenConstructed_IsNonNullSnapshotAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        _ = application.Pointer.ShouldNotBeNull();
        application.Pointer.Position.ShouldBeNull();
        application.HasFocus.ShouldBeTrue();
    }
}
