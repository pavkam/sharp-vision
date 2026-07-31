// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Clipboard;

using Kitty.Clipboard;

/// <summary>
/// Verifies Kitty OSC 5522 metadata and payload parsing.
/// </summary>
public sealed class KittyPacketTests
{
    /// <summary>
    /// Verifies a correlated data reply decodes typed metadata and binary data.
    /// </summary>
    [Fact]
    public void Parse_WhenDataReplyIsValid_ReturnsTypedPacket()
    {
        var packet = Packet.Parse(
            "5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=req-1;AAEC"u8);

        packet.IsValid.ShouldBeTrue();
        packet.Operation.ShouldBe(Operation.Read);
        packet.ReplyStatus.ShouldBe(ReplyStatus.Data);
        packet.Selection.ShouldBe(Selection.Clipboard);
        packet.Id.ShouldBe("req-1");
        packet.Mime.ToArray().ShouldBe("text/plain"u8.ToArray());
        packet.Data.ToArray().ShouldBe([0, 1, 2]);
        packet.HasPayload.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies every defined reply status is typed.
    /// </summary>
    /// <param name="wire">The wire status.</param>
    /// <param name="expected">The typed status.</param>
    [Theory]
    [InlineData("OK", ReplyStatus.Ok)]
    [InlineData("DATA", ReplyStatus.Data)]
    [InlineData("DONE", ReplyStatus.Done)]
    [InlineData("EIO", ReplyStatus.Io)]
    [InlineData("EINVAL", ReplyStatus.Invalid)]
    [InlineData("ENOSYS", ReplyStatus.Unavailable)]
    [InlineData("EPERM", ReplyStatus.Denied)]
    [InlineData("EBUSY", ReplyStatus.Busy)]
    public void Parse_WhenStatusIsKnown_ReturnsTypedStatus(
        string wire,
        ReplyStatus expected)
    {
        var input = Encoding.ASCII.GetBytes($"5522;type=write:status={wire}");

        var packet = Packet.Parse(input);

        packet.IsValid.ShouldBeTrue();
        packet.ReplyStatus.ShouldBe(expected);
    }

    /// <summary>
    /// Verifies Base64 password and name metadata without diagnostic disclosure.
    /// </summary>
    [Fact]
    public void Parse_WhenCredentialsAreValid_DecodesButRedactsText()
    {
        var packet = Packet.Parse(
            "5522;type=read:pw=cGFzc3dvcmQ=:name=ZnJpZW5kbHk=;Lg=="u8);

        packet.IsValid.ShouldBeTrue();
        packet.Password.ToArray().ShouldBe("password"u8.ToArray());
        packet.Name.ToArray().ShouldBe("friendly"u8.ToArray());
        packet.ToString().ShouldNotContain("password");
        packet.ToString().ShouldNotContain("friendly");
        packet.ToString().ShouldNotContain("Lg==");
    }

    /// <summary>
    /// Verifies unknown metadata keys remain observable by name only.
    /// </summary>
    [Fact]
    public void Parse_WhenMetadataKeyIsUnknown_PreservesKeyName()
    {
        var packet = Packet.Parse("5522;type=read:future=secret;Lg=="u8);

        packet.IsValid.ShouldBeTrue();
        packet.UnknownKeys.ShouldBe(["future"]);
        packet.ToString().ShouldContain("future");
        packet.ToString().ShouldNotContain("secret");
    }

    /// <summary>
    /// Verifies callers may validate payload grammar without decoding its data.
    /// </summary>
    [Fact]
    public void Parse_WhenPayloadDecodeIsDisabled_PreservesPresenceOnly()
    {
        var packet = Packet.Parse(
            "5522;type=wdata:mime=dGV4dC9wbGFpbg==;AAEC"u8,
            decodePayload: false);

        packet.IsValid.ShouldBeTrue();
        packet.HasPayload.ShouldBeTrue();
        packet.Data.Length.ShouldBe(0);
    }

    /// <summary>
    /// Verifies a payload with nonzero unused Base64 pad bits is rejected even
    /// when decoding is disabled, matching the decoding-enabled result.
    /// </summary>
    [Fact]
    public void Parse_WhenPayloadHasNonZeroPadBitsAndDecodeIsDisabled_ReturnsInvalidBase64()
    {
        var packet = Packet.Parse("5522;type=wdata;AR=="u8, decodePayload: false);

        packet.IsValid.ShouldBeFalse();
        packet.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.InvalidBase64);
    }

    /// <summary>
    /// Verifies a payload that decodes past the configured clipboard limit is
    /// rejected even when decoding is disabled, matching the decoding-enabled
    /// result.
    /// </summary>
    [Fact]
    public void Parse_WhenPayloadExceedsClipboardLimitAndDecodeIsDisabled_ReturnsInvalid()
    {
        var limits = Limits.Default with { MaxClipboardBytes = 1 };

        var packet = Packet.Parse(
            "5522;type=wdata;AAEC"u8,
            limits,
            decodePayload: false);

        packet.IsValid.ShouldBeFalse();
        packet.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.InvalidBase64);
    }

    /// <summary>
    /// Verifies a payload at exactly the clipboard limit boundary remains
    /// valid regardless of whether decoding is enabled.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Parse_WhenPayloadIsAtClipboardLimitBoundary_RemainsValidInBothModes(bool decodePayload)
    {
        var limits = Limits.Default with { MaxClipboardBytes = 3 };

        var packet = Packet.Parse("5522;type=wdata;AAEC"u8, limits, decodePayload);

        packet.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies validity never depends on whether the payload is materialized.
    /// </summary>
    /// <param name="input">A representative valid or malformed packet.</param>
    [Theory]
    [InlineData("5522;type=wdata;AAEC")]
    [InlineData("5522;type=wdata;AR==")]
    [InlineData("5522;type=wdata;***=")]
    [InlineData("5522;type=wdata;")]
    public void Parse_WhenPayloadIsArbitrary_ValidityIsIndependentOfDecodePayload(string input)
    {
        var bytes = Encoding.ASCII.GetBytes(input);

        var decoded = Packet.Parse(bytes, decodePayload: true);
        var validatedOnly = Packet.Parse(bytes, decodePayload: false);

        validatedOnly.IsValid.ShouldBe(decoded.IsValid);
    }

    /// <summary>
    /// Verifies malformed wire input returns a redacted diagnostic result.
    /// </summary>
    /// <param name="input">The malformed packet.</param>
    /// <param name="code">The expected diagnostic category.</param>
    [Theory]
    [InlineData("5512;type=read", DiagnosticCode.InvalidMetadata)]
    [InlineData("5522;status=OK", DiagnosticCode.InvalidMetadata)]
    [InlineData("5522;type=read:type=write", DiagnosticCode.InvalidMetadata)]
    [InlineData("5522;type=read:id=bad!", DiagnosticCode.InvalidMetadata)]
    [InlineData("5522;type=read:mime=***", DiagnosticCode.InvalidBase64)]
    [InlineData("5522;type=read:name=/w==", DiagnosticCode.InvalidMetadata)]
    [InlineData("5522;type=read;***", DiagnosticCode.InvalidBase64)]
    public void Parse_WhenPacketIsMalformed_ReturnsDiagnostic(string input, DiagnosticCode code)
    {
        var packet = Packet.Parse(Encoding.ASCII.GetBytes(input));

        packet.IsValid.ShouldBeFalse();
        packet.Diagnostic!.Value.Code.ShouldBe(code);
        packet.Data.Length.ShouldBe(0);
        packet.ToString().ShouldNotContain(input);
    }

    /// <summary>
    /// Verifies the configured metadata bound is applied before field parsing.
    /// </summary>
    [Fact]
    public void Parse_WhenMetadataExceedsLimit_ReturnsInvalidMetadata()
    {
        var limits = Limits.Default with { MaxMetadataBytes = 8 };

        var packet = Packet.Parse("5522;type=read:id=abc"u8, limits);

        packet.IsValid.ShouldBeFalse();
        packet.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.InvalidMetadata);
    }
}
