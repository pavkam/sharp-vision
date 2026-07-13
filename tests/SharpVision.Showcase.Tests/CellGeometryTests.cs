// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using SharpVision.Layout;
using SharpVision.Runtime;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Runtime;

using Shouldly;

using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies the Text page geometry specimens render and explain the active contract.</summary>
public sealed class CellGeometryTests
{
    /// <summary>Verifies the geometry specimen renders replacement cells and descriptive copy.</summary>
    [Fact]
    public void Render_WhenTextPageIsSelected_ShowsGeometrySpecimen()
    {
        using Gallery gallery = new Gallery();
        gallery.Select(IndexOf(gallery, "Text"));
        Size size = new Size(120, 80);
        new Engine().Layout(gallery, size);
        using Frame frame = new Frame(size);
        gallery.Render(frame.Canvas);
        Screen screen = new Screen(frame);

        screen.Text.ShouldContain("Cell geometry specimen");
        screen.Text.ShouldContain("orphan");
        screen.Text.ShouldContain("Cells: unavailable");
    }

    /// <summary>Verifies the pointer probe distinguishes unavailable cells from a fabricated origin.</summary>
    [Fact]
    public async Task Input_WhenPixelMetricsAreMissing_ShowsUnavailableCellsAsync()
    {
        await using FakeTerminal terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(120, 80)));
        using Gallery gallery = new Gallery();
        await using Application application = new Application(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel });
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(IndexOf(gallery, "Text")),
            TestContext.Current.CancellationToken);

        using Frame frame = new Frame(application.Size);
        await application.Dispatcher.InvokeAsync(
            () => gallery.Render(frame.Canvas),
            TestContext.Current.CancellationToken);
        Screen screen = new Screen(frame);

        screen.Text.ShouldContain("Pixels: unavailable");
        screen.Text.ShouldContain("Cells: unavailable");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static int IndexOf(Gallery gallery, string page)
    {
        ArgumentNullException.ThrowIfNull(gallery);
        ArgumentException.ThrowIfNullOrWhiteSpace(page);
        var index = gallery.Pages.Select(static value => value.Name).ToList().IndexOf(page);
        return index >= 0 ? index : throw new InvalidOperationException($"The {page} page is not registered.");
    }
}
