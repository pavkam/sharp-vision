// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Freezes public event-args and query-result container construction and validation
/// across the core/terminal boundary: <see cref="PaletteResponseEventArgs"/>,
/// <see cref="MetricsResponseEventArgs"/>, <see cref="StatusResponseEventArgs"/>, and
/// <see cref="CapabilityResponseEventArgs"/> (declared in core <c>SharpVision</c>) each wrap a
/// terminal-declared response type and share its validation contract.</summary>
public sealed class ResponseContainerCompatibilityTests
{
    /// <summary>Verifies event and query containers reject empty and wrong-family public values.</summary>
    [Fact]
    public void ResponseContainers_WhenConsumerSuppliesInvalidValue_RejectBeforeStateChanges()
    {
        var palette = new PaletteResponse(ResponseKind.PaletteColor, 0, 1, 2, 3);
        var foreground = new PaletteResponse(ResponseKind.ForegroundColor, null, 1, 2, 3);
        var pixels = new MetricsResponse(ResponseKind.WindowPixels, new Size(80, 24));
        var cells = new MetricsResponse(ResponseKind.WindowCells, new Size(80, 24));
        var status = new StatusResponse(StatusName.ModifyOtherKeys, isValid: true, ">4;2m"u8);
        XtermGetCap.TryParse(
            "1"u8,
            "+"u8,
            (byte) 'r',
            "524742=3234"u8,
            limits: null,
            out var capability).ShouldBeTrue();

        new PaletteResponseEventArgs(palette).Response.ShouldBe(palette);
        new MetricsResponseEventArgs(pixels).Response.ShouldBe(pixels);
        new StatusResponseEventArgs(status).Response.ShouldBe(status);
        new CapabilityResponseEventArgs(capability!).Response.ShouldBeSameAs(capability);
        new QueryResults { PaletteColor = palette, ForegroundColor = foreground, WindowPixels = pixels }
            .WindowPixels.ShouldBe(pixels);

        _ = Should.Throw<ArgumentException>(() => new PaletteResponseEventArgs(default));
        _ = Should.Throw<ArgumentException>(() => new MetricsResponseEventArgs(default));
        _ = Should.Throw<ArgumentException>(() => new StatusResponseEventArgs(default));
        _ = Should.Throw<ArgumentNullException>(() => new CapabilityResponseEventArgs(null!));
        _ = Should.Throw<ArgumentException>(() => new QueryResults { PaletteColor = default(PaletteResponse) });
        _ = Should.Throw<ArgumentException>(() => new QueryResults { PaletteColor = foreground });
        _ = Should.Throw<ArgumentException>(() => new QueryResults { WindowPixels = cells });
        _ = Should.Throw<ArgumentException>(() => new QueryResults { CellPixels = default(MetricsResponse) });
    }
}
