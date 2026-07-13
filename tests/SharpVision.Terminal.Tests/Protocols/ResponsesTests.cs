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
        Responses.TryCsi("?1;2"u8, [], (byte) 'c', out Response primary).ShouldBeTrue();
        Responses.TryCsi(">41;410;0"u8, [], (byte) 'c', out Response secondary).ShouldBeTrue();

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
    public void TryCsi_WhenPrivateModeResponseIsValid_MapsSupport(
        string parameters,
        bool supported)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(parameters);

        Responses.TryCsi(bytes, "$"u8, (byte) 'y', out Response response).ShouldBeTrue();

        response.Kind.ShouldBe(ResponseKind.PrivateMode);
        response.Values.ToArray().ShouldBe(
            [5522, int.Parse(parameters[6..], System.Globalization.CultureInfo.InvariantCulture)]);
        response.IsSupported.ShouldBe(supported);
    }

    /// <summary>
    /// Verifies a cursor position report retains row and column.
    /// </summary>
    [Fact]
    public void TryCsi_WhenCursorPositionIsValid_ReturnsCoordinates()
    {
        Responses.TryCsi("12;4"u8, [], (byte) 'R', out Response response).ShouldBeTrue();

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
        Responses.TryOsc(Encoding.ASCII.GetBytes(value), out Response response)
            .ShouldBeTrue();

        response.Kind.ShouldBe(expected);
        response.Values.Length.ShouldBe(3);
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
        Responses.TryCsi(
            Encoding.ASCII.GetBytes(parameters),
            Encoding.ASCII.GetBytes(intermediates),
            (byte) final,
            out _).ShouldBeFalse();
    }
}
