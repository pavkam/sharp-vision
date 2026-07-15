// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;




/// <summary>Verifies the Text page geometry specimens render and explain the active contract.</summary>
public sealed class CellGeometryTests
{
    /// <summary>Verifies the geometry specimen renders replacement cells and descriptive copy.</summary>
    [Fact]
    public void Render_WhenTextPageIsSelected_ShowsGeometrySpecimen()
    {
        using Gallery gallery = new();
        gallery.Select(IndexOf(gallery, "Text"));
        var size = new Size(120, 80);
        new Engine().Layout(gallery, size);
        using Frame frame = new(size);
        gallery.Render(frame.Canvas);
        var screen = new Screen(frame);

        screen.Text.ShouldContain("Cell geometry specimen");
        screen.Text.ShouldContain("orphan");
        screen.Text.ShouldContain("Cells: unavailable");
    }

    /// <summary>Verifies the pointer probe distinguishes unavailable cells from a fabricated origin.</summary>
    [Fact]
    public async Task Input_WhenPixelMetricsAreMissing_ShowsUnavailableCellsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(120, 80)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel });
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => gallery.Select(IndexOf(gallery, "Text")),
            TestContext.Current.CancellationToken);

        using Frame frame = new(application.Size);
        await application.Dispatcher.InvokeAsync(
            () => gallery.Render(frame.Canvas),
            TestContext.Current.CancellationToken);
        var screen = new Screen(frame);

        screen.Text.ShouldContain("Pixels: unavailable");
        screen.Text.ShouldContain("Cells: unavailable");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static int IndexOf(Gallery gallery, string page)
    {
        ArgumentNullException.ThrowIfNull(gallery);
        ArgumentException.ThrowIfNullOrWhiteSpace(page);
        var index = gallery.Pages.Select(static value => value).ToList().IndexOf(page);
        return index >= 0 ? index : throw new InvalidOperationException($"The {page} page is not registered.");
    }
}
