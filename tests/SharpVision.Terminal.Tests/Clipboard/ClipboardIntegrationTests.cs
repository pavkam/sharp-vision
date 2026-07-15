// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Clipboard;




/// <summary>
/// Verifies complete typed-writer, parser, packet, and transaction paths.
/// </summary>
public sealed class ClipboardIntegrationTests
{
    /// <summary>
    /// Verifies typed read requests and every response fragmentation complete.
    /// </summary>
    [Fact]
    public void Read_WhenResponseIsFragmented_CompletesAtEverySplit()
    {
        var requestBytes = new ArrayBufferWriter<byte>();
        KittyWriter.Read(
            new Writer(requestBytes),
            "application/octet-stream"u8,
            id: "req-1"u8);
        var request = ParsePackets(requestBytes.WrittenSpan).ShouldHaveSingleItem();

        request.Operation.ShouldBe(KittyOperation.Read);
        request.Id.ShouldBe("req-1");

        var responseBytes = new ArrayBufferWriter<byte>();
        var writer = new Writer(responseBytes);
        writer.Osc(5522, "type=read:status=OK:id=req-1"u8);
        writer.Osc(
            5522,
            "type=read:status=DATA:mime=YXBwbGljYXRpb24vb2N0ZXQtc3RyZWFt:id=req-1;AAEC/w=="u8);
        writer.Osc(5522, "type=read:status=DONE:id=req-1"u8);
        var response = responseBytes.WrittenSpan.ToArray();

        for (var split = 0; split <= response.Length; split++)
        {
            using var transaction = KittyTransaction.Read(id: "req-1");
            using Parser parser = new();
            var sink = new TransactionSink(transaction);

            parser.Parse(response.AsSpan(0, split), ref sink);
            parser.Parse(response.AsSpan(split), ref sink);

            transaction.State.ShouldBe(
                KittyTransactionState.Completed,
                $"Response failed at split {split}.");
            var result = transaction.Result.ShouldNotBeNull();
            result.Items.ShouldHaveSingleItem().Data.ToArray().ShouldBe([0, 1, 2, 255]);
            result.Dispose();
        }
    }

    /// <summary>
    /// Verifies permission failure and a later independent valid transaction.
    /// </summary>
    [Fact]
    public void Read_WhenPermissionFails_DoesNotPoisonNextTransaction()
    {
        using var denied = KittyTransaction.Read();
        denied.Accept(KittyPacket.Parse("5522;type=read:status=EPERM"u8)).ShouldBe(
            KittyAcceptResult.Failed);

        using var malformed = KittyTransaction.Read();
        malformed.Accept(KittyPacket.Parse("5522;type=read;***"u8)).ShouldBe(
            KittyAcceptResult.Failed);

        using var next = KittyTransaction.Read();
        _ = next.Accept(KittyPacket.Parse("5522;type=read:status=OK"u8));
        _ = next.Accept(KittyPacket.Parse("5522;type=read:status=DONE"u8));

        denied.Failure.ShouldBe(KittyReplyStatus.Denied);
        malformed.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.InvalidBase64);
        next.State.ShouldBe(KittyTransactionState.Completed);
    }

    /// <summary>
    /// Verifies alias requests survive typed encoding and streaming parsing.
    /// </summary>
    [Fact]
    public void WriteAlias_WhenParsed_PreservesTargetAndAliases()
    {
        var bytes = new ArrayBufferWriter<byte>();

        KittyWriter.WriteAlias(
            new Writer(bytes),
            "text/plain"u8,
            "text/plain text/utf8"u8);

        var packet = ParsePackets(bytes.WrittenSpan).ShouldHaveSingleItem();
        packet.Operation.ShouldBe(KittyOperation.WriteAlias);
        packet.Mime.ToArray().ShouldBe("text/plain"u8.ToArray());
        packet.Data.ToArray().ShouldBe("text/plain text/utf8"u8.ToArray());
    }

    private static KittyPacket[] ParsePackets(ReadOnlySpan<byte> input)
    {
        using Parser parser = new();
        var sink = new RecordingSink();
        parser.Parse(input, ref sink);

        return
        [
            .. sink.Observations.Select(
                static observation => KittyPacket.Parse(observation.First)),
        ];
    }

}
