// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Iterm;

using System.Buffers.Binary;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;

/// <summary>Proves iTerm2 stale-pixel repair through real renderer transactions.</summary>
public sealed class BackendRendererTests
{
    /// <summary>Verifies partial transport failure invalidates and fully reconstructs cells and PNG.</summary>
    [Fact]
    public async Task RenderAsync_WhenTransportFails_RetryReconstructsCellsAndMultipartImageAsync()
    {
        using var renderer = new Renderer(new NonRetainedGraphicsBackend(enableSixel: false, enableIterm: true));
        await using var transport = new FakeTransport();
        var image = Png();
        using var first = Frame(image, new Rect(0, 0, 1, 1));
        using var moved = Frame(image, new Rect(1, 0, 1, 1));
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            ItermImages = new Feature(CapabilitySupport.Supported, Origin.Override)
        });
        _ = await renderer.RenderAsync(
            first,
            transport,
            profile,
            cellMetrics: null,
            TestContext.Current.CancellationToken);
        transport.QueueFailure(new IOException("partial iTerm2 batch"), prefixBytes: 1);
        _ = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            moved,
            transport,
            profile,
            cellMetrics: null,
            TestContext.Current.CancellationToken));
        transport.Writes.Clear();

        var recovered = await renderer.RenderAsync(
            moved,
            transport,
            profile,
            cellMetrics: null,
            TestContext.Current.CancellationToken);
        var bytes = transport.Writes.ShouldHaveSingleItem();

        recovered.Full.ShouldBeTrue();
        bytes.AsSpan().IndexOf("ab"u8).ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().IndexOf("\u001b]1337;MultipartFile="u8).ShouldBeGreaterThanOrEqualTo(0);
    }

    private static Frame Frame(GraphicsImage image, Rect destination)
    {
        var frame = new Frame(new Size(2, 1));
        _ = frame.Canvas.Draw("ab", default);
        frame.Canvas.DrawImage(image, destination, PlacementMode.Contain);
        return frame;
    }

    private static GraphicsImage Png()
    {
        var payload = new byte[57];
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        signature.CopyTo(payload);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8), 13);
        "IHDR"u8.CopyTo(payload.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(20), 1);
        payload[24] = 8;
        payload[25] = 6;
        "IDAT"u8.CopyTo(payload.AsSpan(37));
        "IEND"u8.CopyTo(payload.AsSpan(49));
        return GraphicsImage.FromPng(payload);
    }
}
