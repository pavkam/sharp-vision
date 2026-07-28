// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

/// <summary>Freezes public terminal response construction, compatibility, and enum values.</summary>
public sealed class TerminalProtocolCompatibilityTests
{
    /// <summary>Verifies typed DCS replies remain observable to a legacy sink at every read split.</summary>
    /// <param name="sequence">The complete typed DCS response bytes.</param>
    [Theory]
    [InlineData("\u001bP1$r>4;2m\u001b\\")]
    [InlineData("\u001bP1+r524742=3234\u001b\\")]
    public void ProtocolRouter_WhenLegacySinkReceivesTypedDcs_ReportsUnsupportedAtEverySplit(
        string sequence)
    {
        var bytes = Encoding.ASCII.GetBytes(sequence);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var legacy = new LegacyProtocolSink();
            using ProtocolRouter router = new(legacy);

            router.Route(bytes.AsSpan(0, split));
            router.Route(bytes.AsSpan(split));

            var diagnostic = legacy.Diagnostics.ShouldHaveSingleItem(
                $"The observable fallback differed at split {split}.");
            diagnostic.Code.ShouldBe(DiagnosticCode.Unsupported);
            diagnostic.Kind.ShouldBe(SequenceKind.Dcs);
            diagnostic.Offset.ShouldBe(0);
            diagnostic.DiscardedBytes.ShouldBe(0);
            legacy.Responses.ShouldBeEmpty();
        }
    }

    /// <summary>Verifies routed OSC 10/11 replies reach an original sink at every read split.</summary>
    /// <param name="sequence">The complete OSC response bytes.</param>
    /// <param name="kind">The expected dynamic-color family.</param>
    [Theory]
    [InlineData("\u001b]10;rgb:0001/0002/0003\u001b\\", ResponseKind.ForegroundColor)]
    [InlineData("\u001b]11;rgb:0001/0002/0003\u001b\\", ResponseKind.BackgroundColor)]
    public void ProtocolRouter_WhenLegacySinkReceivesDefaultColor_AdaptsAtEverySplit(
        string sequence,
        ResponseKind kind)
    {
        var bytes = Encoding.ASCII.GetBytes(sequence);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var legacy = new LegacyProtocolSink();
            using ProtocolRouter router = new(legacy);

            router.Route(bytes.AsSpan(0, split));
            router.Route(bytes.AsSpan(split));

            var response = legacy.Responses.ShouldHaveSingleItem(
                $"The adapted reply differed at split {split}.");
            response.Kind.ShouldBe(kind);
            response.Values.ToArray().ShouldBe([1, 2, 3]);
        }
    }

    /// <summary>Verifies OSC 10/11 typed callbacks retain the legacy RGB value order.</summary>
    /// <param name="kind">The default-color response family.</param>
    [Theory]
    [InlineData(ResponseKind.ForegroundColor)]
    [InlineData(ResponseKind.BackgroundColor)]
    public void IProtocolSink_WhenDefaultColorUsesTypedCallback_PreservesLegacyRgbOrder(
        ResponseKind kind)
    {
        // Arrange
        var legacy = new LegacyProtocolSink();
        IProtocolSink sink = legacy;
        var color = new PaletteResponse(kind, index: null, red: 1, green: 2, blue: 3);

        // Act
        sink.Response(in color);

        // Assert
        var response = legacy.Responses.ShouldHaveSingleItem();
        response.Kind.ShouldBe(kind);
        response.Values.ToArray().ShouldBe([1, 2, 3]);
    }

    /// <summary>Verifies the new indexed-palette callback adapts index then normalized RGB.</summary>
    [Fact]
    public void IProtocolSink_WhenPaletteUsesTypedCallback_AdaptsIndexAndRgbInOrder()
    {
        // Arrange
        var legacy = new LegacyProtocolSink();
        IProtocolSink sink = legacy;
        var palette = new PaletteResponse(
            ResponseKind.PaletteColor,
            index: 15,
            red: 1,
            green: 2,
            blue: 3);

        // Act
        sink.Response(in palette);

        // Assert
        var response = legacy.Responses.ShouldHaveSingleItem();
        response.Kind.ShouldBe(ResponseKind.PaletteColor);
        response.Values.ToArray().ShouldBe([15, 1, 2, 3]);
    }

    /// <summary>Verifies each new metrics callback adapts width then height.</summary>
    /// <param name="kind">The metrics response family.</param>
    [Theory]
    [InlineData(ResponseKind.WindowPixels)]
    [InlineData(ResponseKind.CellPixels)]
    [InlineData(ResponseKind.WindowCells)]
    public void IProtocolSink_WhenMetricsUsesTypedCallback_AdaptsWidthAndHeightInOrder(
        ResponseKind kind)
    {
        // Arrange
        var legacy = new LegacyProtocolSink();
        IProtocolSink sink = legacy;
        var metrics = new MetricsResponse(kind, new Size(8, 16));

        // Act
        sink.Response(in metrics);

        // Assert
        var response = legacy.Responses.ShouldHaveSingleItem();
        response.Kind.ShouldBe(kind);
        response.Values.ToArray().ShouldBe([8, 16]);
    }

    /// <summary>Verifies public response values have a valid empty default and validated construction.</summary>
    [Fact]
    public void ResponseValues_WhenConstructedByConsumer_ValidateFamiliesAndBounds()
    {
        // Arrange / Act
        var emptyPalette = default(PaletteResponse);
        var emptyMetrics = default(MetricsResponse);
        var emptyStatus = default(StatusResponse);
        var palette = new PaletteResponse(
            ResponseKind.PaletteColor,
            index: 255,
            red: ushort.MaxValue,
            green: 2,
            blue: 3);
        var foreground = new PaletteResponse(
            ResponseKind.ForegroundColor,
            index: null,
            red: 1,
            green: 2,
            blue: 3);
        var metrics = new MetricsResponse(ResponseKind.WindowPixels, new Size(65535, 65535));
        var status = new StatusResponse(StatusName.ModifyOtherKeys, isValid: true, ">4;2m"u8);
        var extensionStatus = new StatusResponse(StatusName.Unknown, isValid: true, "?999h"u8);
        var failure = new StatusResponse(StatusName.Unknown, isValid: false, []);

        // Assert
        emptyPalette.IsEmpty.ShouldBeTrue();
        emptyPalette.Kind.ShouldBe(ResponseKind.None);
        emptyPalette.Index.ShouldBeNull();
        emptyMetrics.IsEmpty.ShouldBeTrue();
        emptyStatus.IsEmpty.ShouldBeTrue();
        emptyMetrics.Kind.ShouldBe(ResponseKind.None);
        palette.Index.ShouldBe(255);
        foreground.Index.ShouldBeNull();
        metrics.Size.ShouldBe(new Size(65535, 65535));
        status.Name.ShouldBe(StatusName.ModifyOtherKeys);
        extensionStatus.Name.ShouldBe(StatusName.Unknown);
        extensionStatus.IsValid.ShouldBeTrue();
        failure.IsValid.ShouldBeFalse();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => new PaletteResponse(
            ResponseKind.None, null, 0, 0, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new PaletteResponse(
            ResponseKind.PaletteColor, 256, 0, 0, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new PaletteResponse(
            ResponseKind.BackgroundColor, 0, 0, 0, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new MetricsResponse(
            ResponseKind.WindowCells, default));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new MetricsResponse(
            ResponseKind.ForegroundColor, new Size(1, 1)));
        _ = Should.Throw<ArgumentException>(() => new StatusResponse(
            StatusName.Unknown, isValid: true, "0m"u8));
        _ = Should.Throw<ArgumentException>(() => new StatusResponse(
            StatusName.Rendition, isValid: true, ">4;2m"u8));
        _ = Should.Throw<ArgumentException>(() => new StatusResponse(
            StatusName.ModifyOtherKeys, isValid: true, ">40;2m"u8));
        _ = Should.Throw<ArgumentException>(() => new StatusResponse(
            StatusName.Unknown, isValid: true, []));
        _ = Should.Throw<ArgumentException>(() => new StatusResponse(
            StatusName.ModifyOtherKeys, isValid: false, []));
        _ = Should.Throw<ArgumentException>(() => new StatusResponse(
            StatusName.Unknown, isValid: false, "0m"u8));
    }

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
        new Queries { PaletteColor = palette, ForegroundColor = foreground, WindowPixels = pixels }
            .WindowPixels.ShouldBe(pixels);

        _ = Should.Throw<ArgumentException>(() => new PaletteResponseEventArgs(default));
        _ = Should.Throw<ArgumentException>(() => new MetricsResponseEventArgs(default));
        _ = Should.Throw<ArgumentException>(() => new StatusResponseEventArgs(default));
        _ = Should.Throw<ArgumentNullException>(() => new CapabilityResponseEventArgs(null!));
        _ = Should.Throw<ArgumentException>(() => new Queries { PaletteColor = default(PaletteResponse) });
        _ = Should.Throw<ArgumentException>(() => new Queries { PaletteColor = foreground });
        _ = Should.Throw<ArgumentException>(() => new Queries { WindowPixels = cells });
        _ = Should.Throw<ArgumentException>(() => new Queries { CellPixels = default(MetricsResponse) });
    }

    /// <summary>Verifies every public response and query family has a frozen ordinal.</summary>
    [Fact]
    public void ProtocolKinds_WhenReadByConsumer_PreserveShippedNumericValues()
    {
        ResponseKind[] responses =
        [
            ResponseKind.None,
            ResponseKind.PrimaryAttributes,
            ResponseKind.SecondaryAttributes,
            ResponseKind.CursorPosition,
            ResponseKind.PrivateMode,
            ResponseKind.ForegroundColor,
            ResponseKind.BackgroundColor,
            ResponseKind.Keyboard,
            ResponseKind.PaletteColor,
            ResponseKind.WindowPixels,
            ResponseKind.CellPixels,
            ResponseKind.WindowCells,
            ResponseKind.ModifyOtherKeys
        ];
        QueryKind[] queries =
        [
            QueryKind.PrimaryAttributes,
            QueryKind.SecondaryAttributes,
            QueryKind.CursorPosition,
            QueryKind.PrivateMode,
            QueryKind.ForegroundColor,
            QueryKind.BackgroundColor,
            QueryKind.KittyClipboard,
            QueryKind.Keyboard,
            QueryKind.PaletteColor,
            QueryKind.WindowPixels,
            QueryKind.CellPixels,
            QueryKind.WindowCells,
            QueryKind.StatusString,
            QueryKind.CapabilityString,
            QueryKind.ModifyOtherKeys
        ];

        for (var expected = 0; expected < responses.Length; expected++)
        {
            ((int) responses[expected]).ShouldBe(expected, responses[expected].ToString());
        }

        for (var expected = 0; expected < queries.Length; expected++)
        {
            ((int) queries[expected]).ShouldBe(expected, queries[expected].ToString());
        }
    }
}
