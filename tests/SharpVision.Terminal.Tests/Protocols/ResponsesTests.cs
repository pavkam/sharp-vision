// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies typed capability and status response decoding.
/// </summary>
public sealed class ResponsesTests
{
    /// <summary>
    /// Verifies primary and secondary device attribute replies.
    /// </summary>
    [Fact]
    public void TryCsi_WhenDeviceAttributesAreValid_ReturnsTypedValues()
    {
        XtermResponses.TryCsi("?1;2"u8, [], (byte) 'c', out var primary).ShouldBeTrue();
        XtermResponses.TryCsi(">41;410;0"u8, [], (byte) 'c', out var secondary).ShouldBeTrue();

        primary.Kind.ShouldBe(ResponseKind.PrimaryAttributes);
        primary.Values.ToArray().ShouldBe([1, 2]);
        secondary.Kind.ShouldBe(ResponseKind.SecondaryAttributes);
        secondary.Values.ToArray().ShouldBe([41, 410, 0]);
    }

    /// <summary>
    /// Verifies cursor position and DEC private mode responses.
    /// </summary>
    [Theory]
    [InlineData("?5522;0", false)]
    [InlineData("?5522;4", false)]
    [InlineData("?5522;1", true)]
    [InlineData("?5522;2", true)]
    [InlineData("?5522;3", true)]
    public void TryCsi_WhenPrivateModeResponseIsValid_MapsSupport(
        string parameters,
        bool supported)
    {
        var bytes = Encoding.ASCII.GetBytes(parameters);

        XtermResponses.TryCsi(bytes, "$"u8, (byte) 'y', out var response).ShouldBeTrue();

        response.Kind.ShouldBe(ResponseKind.PrivateMode);
        response.Values.ToArray().ShouldBe(
            [5522, int.Parse(parameters[6..], CultureInfo.InvariantCulture)]);
        response.Supported.ShouldBe(supported);
    }

    /// <summary>
    /// Verifies a cursor position report retains row and column.
    /// </summary>
    [Fact]
    public void TryCsi_WhenCursorPositionIsValid_ReturnsCoordinates()
    {
        XtermResponses.TryCsi("12;4"u8, [], (byte) 'R', out var response).ShouldBeTrue();

        response.Kind.ShouldBe(ResponseKind.CursorPosition);
        response.Values.ToArray().ShouldBe([12, 4]);
    }

    /// <summary>
    /// Verifies default foreground and background RGB replies.
    /// </summary>
    [Theory]
    [InlineData("10;rgb:ffff/0000/aaaa", ResponseKind.ForegroundColor)]
    [InlineData("11;rgb:0000/ffff/1111", ResponseKind.BackgroundColor)]
    public void TryOsc_WhenColorReplyIsValid_ReturnsRgb16(
        string value,
        ResponseKind expected)
    {
        XtermResponses.TryOsc(Encoding.ASCII.GetBytes(value), out var response)
            .ShouldBeTrue();

        response.Kind.ShouldBe(expected);
        response.Index.ShouldBeNull();
        response.Red.ShouldBe(expected == ResponseKind.ForegroundColor ? ushort.MaxValue : ushort.MinValue);
    }

    /// <summary>Verifies OSC 4 owns the palette index and normalized 16-bit RGB value.</summary>
    [Fact]
    public void TryOsc_WhenPaletteReplyIsValid_ReturnsOwnedNormalizedColor()
    {
        var input = "4;15;rgb:f/80/123"u8.ToArray();

        XtermResponses.TryOsc(input, out var response).ShouldBeTrue();
        input.AsSpan().Clear();

        response.Kind.ShouldBe(ResponseKind.PaletteColor);
        response.Index.ShouldBe(15);
        response.Red.ShouldBe(ushort.MaxValue);
        response.Green.ShouldBe((ushort) 0x8080);
        response.Blue.ShouldBe((ushort) 0x1231);
    }

    /// <summary>Verifies window-operation reports validate dimensions before typed construction.</summary>
    [Theory]
    [InlineData("4;1080;1920", ResponseKind.WindowPixels, 1920, 1080)]
    [InlineData("6;20;10", ResponseKind.CellPixels, 10, 20)]
    [InlineData("8;40;120", ResponseKind.WindowCells, 120, 40)]
    public void TryCsi_WhenMetricsReportIsValid_ReturnsTypedSize(
        string parameters,
        ResponseKind expected,
        int width,
        int height)
    {
        XtermResponses.TryMetricsCsi(
            Encoding.ASCII.GetBytes(parameters),
            [],
            (byte) 't',
            out var response).ShouldBeTrue();

        response.Kind.ShouldBe(expected);
        response.Size.ShouldBe(new Size(width, height));
    }

    /// <summary>Verifies invalid colors and dimensions are rejected without constructing typed values.</summary>
    [Theory]
    [InlineData("4;256;rgb:ffff/0000/0000")]
    [InlineData("4;4294967296;rgb:ffff/0000/0000")]
    [InlineData("4;999999999999999999999999999999999999;rgb:ffff/0000/0000")]
    [InlineData("4;0;rgb:10000/0000/0000")]
    [InlineData("10;rgb:/0000/0000")]
    [InlineData("11;rgb:gggg/0000/0000")]
    public void TryOsc_WhenColorReplyIsOutOfRange_ReturnsFalse(string value) =>
        XtermResponses.TryOsc(Encoding.ASCII.GetBytes(value), out _).ShouldBeFalse();

    /// <summary>Verifies non-positive, oversized, and unrelated metric reports are rejected.</summary>
    [Theory]
    [InlineData("4;0;10")]
    [InlineData("6;10;0")]
    [InlineData("8;65536;80")]
    [InlineData("9;40;120")]
    public void TryMetricsCsi_WhenDimensionsAreInvalid_ReturnsFalse(string parameters)
    {
        XtermResponses.TryMetricsCsi(
            Encoding.ASCII.GetBytes(parameters),
            [],
            (byte) 't',
            out _).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies malformed or unrelated replies are non-throwing misses.
    /// </summary>
    [Theory]
    [InlineData("?x", "", 'c')]
    [InlineData("12", "", 'R')]
    [InlineData("?5522;9", "$", 'y')]
    public void TryCsi_WhenReplyIsMalformed_ReturnsFalse(
        string parameters,
        string intermediates,
        char final)
    {
        XtermResponses.TryCsi(
            Encoding.ASCII.GetBytes(parameters),
            Encoding.ASCII.GetBytes(intermediates),
            (byte) final,
            out _).ShouldBeFalse();
    }
}
