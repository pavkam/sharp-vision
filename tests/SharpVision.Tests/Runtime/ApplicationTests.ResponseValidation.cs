// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>
/// Verifies <c>Application.Response(in PaletteResponse)</c> and
/// <c>Application.Response(in MetricsResponse)</c> reject the empty default sentinel
/// synchronously, matching the sibling <c>Application.Response(in StatusResponse)</c> overload
/// immediately above them. Without this guard, an empty value is silently enqueued and the
/// <see cref="ArgumentException"/> that <see cref="PaletteResponseEventArgs"/> and
/// <see cref="MetricsResponseEventArgs"/> already declare for the same condition is instead thrown
/// much later, off the caller's own stack, inside <c>Application.Dispatch</c> - which the
/// dispatcher's own callback-failure path then treats as an unhandled application failure and
/// force-stops the whole application, rather than rejecting the bad call where it happened.
/// </summary>
public sealed partial class ApplicationTests
{
    /// <summary>Verifies an empty <see cref="PaletteResponse"/> is rejected synchronously instead
    /// of being enqueued and only failing later during dispatch.</summary>
    [Fact]
    public async Task Response_WhenPaletteResponseIsEmpty_ThrowsArgumentExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);

        _ = Should.Throw<ArgumentException>(() => application.Response(default(PaletteResponse)));

        await application.DisposeAsync();
    }

    /// <summary>Verifies an empty <see cref="MetricsResponse"/> is rejected synchronously instead
    /// of being enqueued and only failing later during dispatch.</summary>
    [Fact]
    public async Task Response_WhenMetricsResponseIsEmpty_ThrowsArgumentExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        var application = new Application(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);

        _ = Should.Throw<ArgumentException>(() => application.Response(default(MetricsResponse)));

        await application.DisposeAsync();
    }
}
