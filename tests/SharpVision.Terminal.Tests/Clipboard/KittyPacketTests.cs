using SharpVision.Terminal.Clipboard;
using SharpVision.Terminal.Protocols;

using Shouldly;

namespace SharpVision.Terminal.Tests.Clipboard;

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
        var packet = KittyPacket.Parse(
            "5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=req-1;AAEC"u8);

        packet.IsValid.ShouldBeTrue();
        packet.Operation.ShouldBe(KittyOperation.Read);
        packet.ReplyStatus.ShouldBe(KittyReplyStatus.Data);
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
    [InlineData("OK", KittyReplyStatus.Ok)]
    [InlineData("DATA", KittyReplyStatus.Data)]
    [InlineData("DONE", KittyReplyStatus.Done)]
    [InlineData("EIO", KittyReplyStatus.Io)]
    [InlineData("EINVAL", KittyReplyStatus.Invalid)]
    [InlineData("ENOSYS", KittyReplyStatus.Unavailable)]
    [InlineData("EPERM", KittyReplyStatus.Denied)]
    [InlineData("EBUSY", KittyReplyStatus.Busy)]
    public void Parse_WhenStatusIsKnown_ReturnsTypedStatus(
        string wire,
        KittyReplyStatus expected)
    {
        var input = System.Text.Encoding.ASCII.GetBytes($"5522;type=write:status={wire}");

        var packet = KittyPacket.Parse(input);

        packet.IsValid.ShouldBeTrue();
        packet.ReplyStatus.ShouldBe(expected);
    }

    /// <summary>
    /// Verifies Base64 password and name metadata without diagnostic disclosure.
    /// </summary>
    [Fact]
    public void Parse_WhenCredentialsAreValid_DecodesButRedactsText()
    {
        var packet = KittyPacket.Parse(
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
        var packet = KittyPacket.Parse("5522;type=read:future=secret;Lg=="u8);

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
        var packet = KittyPacket.Parse(
            "5522;type=wdata:mime=dGV4dC9wbGFpbg==;AAEC"u8,
            decodePayload: false);

        packet.IsValid.ShouldBeTrue();
        packet.HasPayload.ShouldBeTrue();
        packet.Data.Length.ShouldBe(0);
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
        var packet = KittyPacket.Parse(System.Text.Encoding.ASCII.GetBytes(input));

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

        var packet = KittyPacket.Parse("5522;type=read:id=abc"u8, limits);

        packet.IsValid.ShouldBeFalse();
        packet.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.InvalidMetadata);
    }
}
