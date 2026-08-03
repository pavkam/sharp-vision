// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Kitty;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Clipboard;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Kitty.Graphics;

/// <summary>Proves strict owned Kitty APC reply parsing and correlation.</summary>
public sealed class ResponseTests
{
    /// <summary>Verifies an official successful query reply parses at every transport split.</summary>
    [Fact]
    public void Parse_WhenSuccessfulQueryIsFragmented_ProducesSameTypedResponseAtEverySplit()
    {
        var wire = "\u001b_Gi=31;OK\u001b\\"u8.ToArray();

        for (var split = 0; split <= wire.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(wire.AsSpan(0, split));
            router.Route(wire.AsSpan(split));

            var response = sink.KittyGraphicsResponses.ShouldHaveSingleItem();
            response.IsValid.ShouldBeTrue($"split {split}");
            response.ImageId.ShouldBe(31U);
            response.PlacementId.ShouldBe(0U);
            response.IsSuccess.ShouldBeTrue();
            response.Message.ShouldBe("OK");
        }
    }

    /// <summary>Verifies placement-correlated terminal errors remain typed and redaction-safe.</summary>
    [Fact]
    public void Parse_WhenPlacementFails_OwnsIdentifiersAndPrintableError()
    {
        var response = KittyGraphicsResponse.Parse("Gi=7,p=9;ENOENT:image missing"u8);

        response.IsValid.ShouldBeTrue();
        response.ImageId.ShouldBe(7U);
        response.PlacementId.ShouldBe(9U);
        response.IsSuccess.ShouldBeFalse();
        response.Message.ShouldBe("ENOENT:image missing");
        response.ToString().ShouldNotContain("image missing");
    }

    /// <summary>Verifies unknown, duplicate, malformed, and oversized replies are rejected.</summary>
    [Theory]
    [InlineData("Gi=1,x=2;OK")]
    [InlineData("Gi=1,i=2;OK")]
    [InlineData("Gi=0;OK")]
    [InlineData("Gp=2;OK")]
    [InlineData("Gi=abc;OK")]
    [InlineData("Gi=+1;OK")]
    [InlineData("Gi=-1;OK")]
    [InlineData("Gi=031;OK")]
    [InlineData("Gi=4294967296;OK")]
    [InlineData("Gi=;OK")]
    [InlineData("Gi=1;")]
    [InlineData("Gi=1;O\nK")]
    [InlineData("i=1;OK")]
    public void Parse_WhenReplyGrammarIsInvalid_ReturnsRedactedDiagnostic(string value)
    {
        var response = KittyGraphicsResponse.Parse(Encoding.ASCII.GetBytes(value));

        response.IsValid.ShouldBeFalse();
        _ = response.Diagnostic.ShouldNotBeNull();
        response.ToString().ShouldNotContain(value);
    }

    /// <summary>Verifies a one-character printable terminal error is grammar-valid.</summary>
    [Fact]
    public void Parse_WhenMessageHasOnePrintableCharacter_AcceptsResponse()
    {
        var response = KittyGraphicsResponse.Parse("Gi=1;E"u8);

        response.IsValid.ShouldBeTrue();
        response.IsSuccess.ShouldBeFalse();
        response.Message.ShouldBe("E");
    }

    /// <summary>Verifies enabled C1 APC framing reaches the same strict reply parser.</summary>
    [Fact]
    public void Parse_WhenEightBitApcIsEnabled_ProducesGraphicsPayload()
    {
        var limits = ParserLimits.Default with { AcceptEightBitControls = true };
        var options = InputOptions.Default with { ParserLimits = limits };
        byte[] wire = [0x9f, (byte) 'G', (byte) 'i', (byte) '=', (byte) '3', (byte) ';', (byte) 'O', (byte) 'K', 0x9c];

        for (var split = 0; split <= wire.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink, options);
            router.Route(wire.AsSpan(0, split));
            router.Route(wire.AsSpan(split));

            var response = sink.KittyGraphicsResponses.ShouldHaveSingleItem();
            response.IsValid.ShouldBeTrue($"split {split}");
            response.ImageId.ShouldBe(3U);
        }
    }

    /// <summary>Verifies configured response bounds reject excessive metadata and messages.</summary>
    [Fact]
    public void Parse_WhenReplyExceedsBounds_ReturnsStringLimit()
    {
        var limits = TransferLimits.Default with { MaxMetadataBytes = 8 };

        var response = KittyGraphicsResponse.Parse("Gi=123456;OK"u8, limits);

        response.IsValid.ShouldBeFalse();
        response.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
    }

    /// <summary>Verifies one correlation cannot complete twice or consume another identifier.</summary>
    [Fact]
    public void Accept_WhenCorrelationIsReused_ReportsDuplicateWithoutReplacingResult()
    {
        var transaction = new Transaction(31);
        var matching = KittyGraphicsResponse.Parse("Gi=31;OK"u8);
        var other = KittyGraphicsResponse.Parse("Gi=32;OK"u8);

        transaction.Accept(other).ShouldBe(QueryMatch.Unknown);
        transaction.Accept(matching).ShouldBe(QueryMatch.Matched);
        transaction.Accept(matching).ShouldBe(QueryMatch.Duplicate);

        transaction.Response.ShouldBeSameAs(matching);
    }

    /// <summary>Verifies malformed APC recovery leaves the next graphics response observable.</summary>
    [Fact]
    public void Parse_WhenMalformedReplyPrecedesValidReply_RecoversAtNextApc()
    {
        var wire = "\u001b_Gi=1,i=2;OK\u001b\\\u001b_Gi=3;OK\u001b\\"u8.ToArray();

        for (var split = 0; split <= wire.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(wire.AsSpan(0, split));
            router.Route(wire.AsSpan(split));

            sink.KittyGraphicsResponses.Count.ShouldBe(2, $"split {split}");
            sink.KittyGraphicsResponses[0].IsValid.ShouldBeFalse();
            sink.KittyGraphicsResponses[1].ShouldSatisfyAllConditions(
                response => response.IsValid.ShouldBeTrue(),
                response => response.ImageId.ShouldBe(3U));
        }
    }

    /// <summary>Verifies printable error payloads parse identically at every transport split.</summary>
    [Fact]
    public void Parse_WhenPrintableErrorIsFragmented_ProducesSameTypedResponseAtEverySplit()
    {
        var wire = "\u001b_Gi=7,p=9;ENOENT:image missing\u001b\\"u8.ToArray();

        for (var split = 0; split <= wire.Length; split++)
        {
            var sink = new RecordingProtocolSink();
            using var router = new ProtocolRouter(sink);
            router.Route(wire.AsSpan(0, split));
            router.Route(wire.AsSpan(split));

            var response = sink.KittyGraphicsResponses.ShouldHaveSingleItem();
            response.IsValid.ShouldBeTrue($"split {split}");
            response.ImageId.ShouldBe(7U);
            response.PlacementId.ShouldBe(9U);
            response.Message.ShouldBe("ENOENT:image missing");
        }
    }
}
