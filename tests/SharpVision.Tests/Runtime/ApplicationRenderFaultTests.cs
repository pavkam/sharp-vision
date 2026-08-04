// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies a render fault raised synchronously, before RenderAsync's first await,
/// still retires the render cycle and reaches a completed shutdown instead of hanging (see #269).</summary>
public sealed class ApplicationRenderFaultTests
{
    /// <summary>Verifies a synchronous Prepare fault on the very first render surfaces through
    /// StartAsync and Failure, and still disposes the transport, instead of leaving _rendering
    /// permanently set and the shutdown drain awaiting an unobservable render task.</summary>
    [Fact]
    public async Task StartAsync_WhenRenderAsyncFaultsSynchronously_CompletesInsteadOfHangingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(4, 2)));
        var backend = new ThrowingGraphicsBackend();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        application.SeedRenderer(new Renderer(backend));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StartAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(backend.Failure);
        application.Failure.ShouldBeSameAs(backend.Failure);
        terminal.Disposals.ShouldBe(1);
    }
}
