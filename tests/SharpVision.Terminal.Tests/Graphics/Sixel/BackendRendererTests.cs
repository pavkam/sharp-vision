// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Sixel;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;

/// <summary>Proves sixel stale-pixel repair through the real renderer transaction.</summary>
public sealed class BackendRendererTests
{
    /// <summary>Verifies movement rewrites complete cells before re-emitting the target sixel.</summary>
    [Fact]
    public async Task RenderAsync_WhenPlacementMoves_ClearsCellsBeforeTargetSixelAsync()
    {
        using var renderer = new Renderer(new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false));
        await using var transport = new FakeTransport();
        var image = Red();
        using var first = Frame(image, new Rect(0, 0, 1, 1));
        using var moved = Frame(image, new Rect(1, 0, 1, 1));
        var profile = Profile();
        _ = await renderer.RenderAsync(
            first,
            transport,
            profile,
            new CellMetrics(1, 6),
            TestContext.Current.CancellationToken);
        transport.Writes.Clear();

        var result = await renderer.RenderAsync(
            moved,
            transport,
            profile,
            new CellMetrics(1, 6),
            TestContext.Current.CancellationToken);

        var bytes = transport.Writes.ShouldHaveSingleItem();
        var cells = bytes.AsSpan().IndexOf("ab"u8);
        var sixel = bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8);
        result.Full.ShouldBeTrue();
        cells.ShouldBeGreaterThanOrEqualTo(0);
        sixel.ShouldBeGreaterThan(cells);
    }

    /// <summary>Verifies measured geometry changes reconstruct the sixel and disappearance clears it.</summary>
    [Fact]
    public async Task RenderAsync_WhenMetricsChangeOrDisappear_ForcesFullCellRepairAsync()
    {
        using var renderer = new Renderer(new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false));
        await using var transport = new FakeTransport();
        using var frame = Frame(Red(), new Rect(0, 0, 1, 1));
        var profile = Profile();
        _ = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            new CellMetrics(1, 6),
            TestContext.Current.CancellationToken);
        transport.Writes.Clear();

        var changed = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            new CellMetrics(new Size(2, 1), new Size(3, 7)),
            TestContext.Current.CancellationToken);
        var changedBytes = transport.Writes.ShouldHaveSingleItem();
        transport.Writes.Clear();
        var missing = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            cellMetrics: null,
            TestContext.Current.CancellationToken);
        var missingBytes = transport.Writes.ShouldHaveSingleItem();

        changed.Full.ShouldBeTrue();
        changedBytes.AsSpan().IndexOf("\"1;1;2;7"u8).ShouldBeGreaterThanOrEqualTo(0);
        missing.Full.ShouldBeTrue();
        missingBytes.AsSpan().IndexOf("\u001bP"u8).ShouldBe(-1);
        missingBytes.AsSpan().IndexOf("ab"u8).ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>Verifies partial transport failure invalidates and reconstructs all sixel state.</summary>
    [Fact]
    public async Task RenderAsync_WhenTransportFails_RetryReconstructsCellsAndSixelAsync()
    {
        using var renderer = new Renderer(new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false));
        await using var transport = new FakeTransport();
        var image = Red();
        using var first = Frame(image, new Rect(0, 0, 1, 1));
        using var moved = Frame(image, new Rect(1, 0, 1, 1));
        var profile = Profile();
        var metrics = new CellMetrics(1, 6);
        _ = await renderer.RenderAsync(
            first,
            transport,
            profile,
            metrics,
            TestContext.Current.CancellationToken);
        transport.QueueFailure(new IOException("partial sixel batch"), prefixBytes: 1);
        _ = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            moved,
            transport,
            profile,
            metrics,
            TestContext.Current.CancellationToken));
        transport.Writes.Clear();

        var recovered = await renderer.RenderAsync(
            moved,
            transport,
            profile,
            metrics,
            TestContext.Current.CancellationToken);
        var bytes = transport.Writes.ShouldHaveSingleItem();

        recovered.Full.ShouldBeTrue();
        bytes.AsSpan().IndexOf("ab"u8).ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>Verifies an RGBA placement on an iTerm-only backend reports a format diagnostic
    /// instead of failing silently, since iTerm never accepts raw RGBA sources.</summary>
    [Fact]
    public async Task RenderAsync_WhenRgbaPlacementHasNoItermPath_ReportsFormatDiagnosticAsync()
    {
        using var renderer = new Renderer(new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true));
        await using var transport = new FakeTransport();
        var image = Red();
        using var frame = Frame(image, new Rect(0, 0, 1, 1));
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            ItermImages = new Feature(CapabilitySupport.Supported, Origin.Override)
        });

        _ = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            new CellMetrics(1, 6),
            TestContext.Current.CancellationToken);

        var diagnostic = renderer.LastGraphicsDiagnostics.ShouldHaveSingleItem();
        diagnostic.ImageIdentity.ShouldBe(image.Identity);
        diagnostic.Reason.ShouldBe(GraphicsPlacementSkipReason.FormatNotEncodable);
    }

    /// <summary>Verifies a PNG this decoder cannot decode drops its placement silently on a
    /// sixel-only backend rather than throwing out of the render, since its format is in
    /// principle sixel-encodable and so does not reach the format diagnostic.</summary>
    [Fact]
    public async Task RenderAsync_WhenPngCannotBeDecoded_DropsPlacementWithoutDiagnosticAsync()
    {
        using var renderer = new Renderer(new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false));
        await using var transport = new FakeTransport();
        using var frame = Frame(Png(), new Rect(0, 0, 1, 1));
        var profile = Profile();

        _ = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            new CellMetrics(1, 6),
            TestContext.Current.CancellationToken);

        renderer.LastGraphicsDiagnostics.ShouldBeEmpty();
        var bytes = transport.Writes.ShouldHaveSingleItem();
        bytes.AsSpan().IndexOf("P0;1;0q"u8).ShouldBe(-1);
    }

    /// <summary>Verifies an ordinary encodable placement reports no diagnostics.</summary>
    [Fact]
    public async Task RenderAsync_WhenPlacementEncodesNormally_ReportsNoDiagnosticsAsync()
    {
        using var renderer = new Renderer(new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false));
        await using var transport = new FakeTransport();
        using var frame = Frame(Red(), new Rect(0, 0, 1, 1));
        var profile = Profile();

        _ = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            new CellMetrics(1, 6),
            TestContext.Current.CancellationToken);

        renderer.LastGraphicsDiagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies a placement that would otherwise encode fine reports ProtocolNotAuthorized -
    /// rather than being mislabeled FormatNotEncodable - when the profile deauthorizes every
    /// protocol the backend was constructed to use (see #233).
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenProfileDeauthorizesEveryEnabledProtocol_ReportsProtocolNotAuthorizedAsync()
    {
        using var renderer = new Renderer(new NonRetainedGraphicsBackend(enableSixel: true, enableIterm: false));
        await using var transport = new FakeTransport();
        var image = Red();
        using var frame = Frame(image, new Rect(0, 0, 1, 1));
        var deauthorized = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            Sixel = new Feature(CapabilitySupport.Unsupported, Origin.Override)
        });

        _ = await renderer.RenderAsync(
            frame,
            transport,
            deauthorized,
            new CellMetrics(1, 6),
            TestContext.Current.CancellationToken);

        var diagnostic = renderer.LastGraphicsDiagnostics.ShouldHaveSingleItem();
        diagnostic.ImageIdentity.ShouldBe(image.Identity);
        diagnostic.Reason.ShouldBe(GraphicsPlacementSkipReason.ProtocolNotAuthorized);
    }

    private static GraphicsImage Red() => GraphicsImage.FromRgba(
        new Size(1, 1),
        [255, 0, 0, 255]);

    private static GraphicsImage Png() => GraphicsImage.FromPng([
        137, 80, 78, 71, 13, 10, 26, 10,
        0, 0, 0, 13, 73, 72, 68, 82,
        0, 0, 0, 1, 0, 0, 0, 1,
        8, 6, 0, 0, 0,
        0, 0, 0, 0,
        0, 0, 0, 0, 73, 68, 65, 84,
        0, 0, 0, 0,
        0, 0, 0, 0, 73, 69, 78, 68,
        0, 0, 0, 0
    ]);

    private static TerminalProfile Profile() => TerminalProfile.CreateAnsi(
        TerminalCapabilities.Conservative with
        {
            Sixel = new Feature(CapabilitySupport.Supported, Origin.Override)
        });

    private static Frame Frame(GraphicsImage image, Rect destination)
    {
        var frame = new Frame(new Size(2, 1));
        _ = frame.Canvas.Draw("ab", default);
        frame.Canvas.DrawImage(image, destination, PlacementMode.Stretch);
        return frame;
    }
}
