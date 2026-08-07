// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Clipboard;

using Kitty.Clipboard;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Clipboard;

using ClipboardPacket = Kitty.Clipboard.KittyClipboardPacket;

/// <summary>
/// Verifies bounded Kitty clipboard transaction state machines.
/// </summary>
public sealed class KittyTransactionTests
{
    /// <summary>
    /// Verifies accepted read data is combined by MIME type and published at DONE.
    /// </summary>
    [Fact]
    public void Accept_WhenReadPacketsAreOrdered_CompletesWithMimeData()
    {
        using var transaction = KittyClipboardTransaction.Read(id: "req-1");

        transaction.Accept(Packet("5522;type=read:status=OK:id=req-1")).ShouldBe(
            KittyClipboardAcceptResult.Accepted);
        transaction.Accept(
                Packet("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=req-1;aGVs"))
            .ShouldBe(KittyClipboardAcceptResult.Accepted);
        transaction.Accept(
                Packet("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=req-1;bG8="))
            .ShouldBe(KittyClipboardAcceptResult.Accepted);
        transaction.Accept(Packet("5522;type=read:status=DONE:id=req-1")).ShouldBe(
            KittyClipboardAcceptResult.Completed);
        transaction.Accept(
                Packet("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=req-1;eA=="))
            .ShouldBe(KittyClipboardAcceptResult.Ignored);

        transaction.State.ShouldBe(KittyClipboardTransactionState.Completed);
        var result = transaction.Result.ShouldNotBeNull();
        result.Items.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            item => item.Mime.ShouldBe("text/plain"),
            item => item.Data.ToArray().ShouldBe("hello"u8.ToArray()));
        result.Dispose();
    }

    /// <summary>
    /// Verifies MIME-list reads may carry DATA without a MIME metadata field.
    /// </summary>
    [Fact]
    public void Accept_WhenMimeListResponseIsOrdered_CompletesWithListData()
    {
        using var transaction = KittyClipboardTransaction.Read(listOnly: true);
        _ = transaction.Accept(Packet("5522;type=read:status=OK"));
        _ = transaction.Accept(Packet("5522;type=read:status=DATA;dGV4dC9wbGFpbg=="));

        transaction.Accept(Packet("5522;type=read:status=DONE")).ShouldBe(
            KittyClipboardAcceptResult.Completed);

        var result = transaction.Result.ShouldNotBeNull();
        result.Items.ShouldHaveSingleItem().Data.ToArray().ShouldBe("text/plain"u8.ToArray());
        result.Dispose();
    }

    /// <summary>
    /// Verifies write completion accepts the single documented DONE response.
    /// </summary>
    [Fact]
    public void Accept_WhenWriteReturnsDone_Completes()
    {
        using var transaction = KittyClipboardTransaction.Write(id: "write-1");

        var result = transaction.Accept(
            Packet("5522;type=write:status=DONE:id=write-1"));

        result.ShouldBe(KittyClipboardAcceptResult.Completed);
        transaction.State.ShouldBe(KittyClipboardTransactionState.Completed);
        _ = transaction.Result.ShouldNotBeNull();
    }

    /// <summary>
    /// Verifies every protocol error moves a matching transaction to failed.
    /// </summary>
    /// <param name="status">The wire error status.</param>
    [Theory]
    [InlineData("EIO")]
    [InlineData("EINVAL")]
    [InlineData("ENOSYS")]
    [InlineData("EPERM")]
    [InlineData("EBUSY")]
    public void Accept_WhenTerminalReturnsError_Fails(string status)
    {
        using var transaction = KittyClipboardTransaction.Read();
        var packet = Packet($"5522;type=read:status={status}");

        transaction.Accept(packet).ShouldBe(KittyClipboardAcceptResult.Failed);

        transaction.State.ShouldBe(KittyClipboardTransactionState.Failed);
        transaction.Failure.ShouldNotBe(KittyClipboardReplyStatus.None);
    }

    /// <summary>
    /// Verifies invalid state order fails only the active transaction.
    /// </summary>
    /// <param name="first">The first packet.</param>
    /// <param name="second">The optional second packet.</param>
    /// <param name="third">The optional third packet.</param>
    /// <param name="fourth">The optional fourth packet.</param>
    [Theory]
    [InlineData("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==;YQ==", null, null, null)]
    [InlineData("5522;type=read:status=OK", "5522;type=read:status=OK", null, null)]
    [InlineData(
        "5522;type=read:status=OK",
        "5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==;YQ==",
        "5522;type=read:status=DATA:mime=aW1hZ2UvcG5n;Yg==",
        "5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==;Yw==")]
    public void Accept_WhenPacketOrderIsInvalid_Fails(
        string first,
        string? second,
        string? third,
        string? fourth)
    {
        using var transaction = KittyClipboardTransaction.Read();

        foreach (var packet in new[] { first, second, third, fourth })
        {
            if (packet is not null)
            {
                _ = transaction.Accept(Packet(packet));
            }
        }

        transaction.State.ShouldBe(KittyClipboardTransactionState.Failed);
        transaction.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.UnexpectedPacket);
    }

    /// <summary>
    /// Verifies packets for another correlation ID are ignored without mutation.
    /// </summary>
    [Theory]
    [InlineData("5522;type=read:status=OK")]
    [InlineData("5522;type=read:status=OK:id=other")]
    public void Accept_WhenCorrelationDoesNotMatch_IgnoresPacket(string wire)
    {
        using var transaction = KittyClipboardTransaction.Read(id: "req-1");

        transaction.Accept(Packet(wire)).ShouldBe(KittyClipboardAcceptResult.Ignored);

        transaction.State.ShouldBe(KittyClipboardTransactionState.Created);
    }

    /// <summary>
    /// Verifies malformed packets and data limits fail with redacted diagnostics.
    /// </summary>
    [Fact]
    public void Accept_WhenPacketOrDataIsInvalid_FailsAndClearsBuffers()
    {
        var limits = TransferLimits.Default with { MaxClipboardBytes = 1 };
        using var malformed = KittyClipboardTransaction.Read(limits);
        using var oversized = KittyClipboardTransaction.Read(limits);

        malformed.Accept(ClipboardPacket.Parse("5522;type=read;***"u8)).ShouldBe(
            KittyClipboardAcceptResult.Failed);
        _ = oversized.Accept(Packet("5522;type=read:status=OK"));
        oversized.Accept(
                Packet("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==;YWI="))
            .ShouldBe(KittyClipboardAcceptResult.Failed);

        malformed.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.InvalidBase64);
        oversized.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
        malformed.ToString().ShouldNotContain("***");
    }

    /// <summary>
    /// Verifies an invalid packet whose recovered id belongs to another
    /// correlation does not fail an ID-bound transaction.
    /// </summary>
    [Fact]
    public void Accept_WhenInvalidPacketCarriesDifferentId_IgnoresPacket()
    {
        using var transaction = KittyClipboardTransaction.Read(id: "req-1");

        // id= parses before the later unknown-type failure, so the packet is
        // attributed to "other" and must not be treated as ours.
        transaction.Accept(ClipboardPacket.Parse("5522;id=other:type=***"u8)).ShouldBe(
            KittyClipboardAcceptResult.Ignored);

        transaction.State.ShouldBe(KittyClipboardTransactionState.Created);
    }

    /// <summary>
    /// Verifies an invalid packet whose recovered id matches the bound
    /// transaction still fails it.
    /// </summary>
    [Fact]
    public void Accept_WhenInvalidPacketCarriesMatchingId_Fails()
    {
        using var transaction = KittyClipboardTransaction.Read(id: "req-1");

        transaction.Accept(ClipboardPacket.Parse("5522;id=req-1:type=***"u8)).ShouldBe(
            KittyClipboardAcceptResult.Failed);

        transaction.State.ShouldBe(KittyClipboardTransactionState.Failed);
    }

    /// <summary>
    /// Verifies an invalid packet with no attributable id (the failure occurs
    /// before any id field is parsed) is ignored by an ID-bound transaction
    /// rather than treated as a match.
    /// </summary>
    [Fact]
    public void Accept_WhenInvalidPacketHasNoAttributableId_IgnoresIdBoundTransaction()
    {
        using var transaction = KittyClipboardTransaction.Read(id: "req-1");

        // Fails at the "5522;" prefix check, before any metadata is parsed.
        transaction.Accept(ClipboardPacket.Parse("not-kitty"u8)).ShouldBe(
            KittyClipboardAcceptResult.Ignored);

        transaction.State.ShouldBe(KittyClipboardTransactionState.Created);
    }

    /// <summary>
    /// Verifies unrelated malformed traffic interleaved with a matching
    /// correlation stream does not disturb the transaction in progress.
    /// </summary>
    [Fact]
    public void Accept_WhenUnrelatedMalformedTrafficInterleaves_CompletesMatchingStream()
    {
        using var transaction = KittyClipboardTransaction.Read(id: "req-1");

        transaction.Accept(Packet("5522;type=read:status=OK:id=req-1")).ShouldBe(
            KittyClipboardAcceptResult.Accepted);
        transaction.Accept(ClipboardPacket.Parse("5522;id=other:type=***"u8)).ShouldBe(
            KittyClipboardAcceptResult.Ignored);
        transaction.Accept(Packet("5522;type=read:status=DONE:id=req-1")).ShouldBe(
            KittyClipboardAcceptResult.Completed);

        transaction.State.ShouldBe(KittyClipboardTransactionState.Completed);
        transaction.Result!.Dispose();
    }

    /// <summary>
    /// Verifies a terminal cannot bypass the protocol's per-packet chunk bound.
    /// </summary>
    [Fact]
    public void Accept_WhenDataChunkExceeds4096Bytes_Fails()
    {
        var encoded = Convert.ToBase64String(new byte[4_097]);
        using var transaction = KittyClipboardTransaction.Read();
        _ = transaction.Accept(Packet("5522;type=read:status=OK"));

        var result = transaction.Accept(Packet(
            $"5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==;{encoded}"));

        result.ShouldBe(KittyClipboardAcceptResult.Failed);
        transaction.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
    }

    /// <summary>
    /// Verifies explicit cancellation is terminal and late packets are ignored.
    /// </summary>
    [Fact]
    public void Cancel_WhenActive_CancelsAndIgnoresLatePackets()
    {
        using var transaction = KittyClipboardTransaction.Read();

        transaction.Cancel();

        transaction.State.ShouldBe(KittyClipboardTransactionState.Cancelled);
        transaction.Accept(Packet("5522;type=read:status=OK")).ShouldBe(
            KittyClipboardAcceptResult.Ignored);
    }

    /// <summary>
    /// Verifies timeout uses the injected clock without wall-clock waiting.
    /// </summary>
    [Fact]
    public void CheckTimeout_WhenDeadlinePasses_TimesOut()
    {
        var clock = new ManualTimeProvider();
        var queryLimits = QueryLimits.Default with { QueryTimeout = TimeSpan.FromSeconds(2) };
        using var transaction = KittyClipboardTransaction.Read(timeProvider: clock, queryLimits: queryLimits);

        clock.Advance(TimeSpan.FromSeconds(2));

        transaction.CheckTimeout().ShouldBeTrue();
        transaction.State.ShouldBe(KittyClipboardTransactionState.TimedOut);
        transaction.Accept(Packet("5522;type=read:status=OK")).ShouldBe(
            KittyClipboardAcceptResult.Ignored);
    }

    /// <summary>
    /// Verifies disposing an owned result clears its publicly observable bytes.
    /// </summary>
    [Fact]
    public void Dispose_WhenResultOwnsData_ClearsData()
    {
        using var transaction = KittyClipboardTransaction.Read();
        _ = transaction.Accept(Packet("5522;type=read:status=OK"));
        _ = transaction.Accept(
            Packet("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==;c2VjcmV0"));
        _ = transaction.Accept(Packet("5522;type=read:status=DONE"));
        var result = transaction.Result.ShouldNotBeNull();
        var data = result.Items.ShouldHaveSingleItem().Data;

        result.Dispose();

        data.ToArray().ShouldBe(new byte[6]);
    }

    /// <summary>
    /// Verifies a caller cannot replace an owned item through the public read-only view and make
    /// disposal skip the transferred clipboard buffer.
    /// </summary>
    [Fact]
    public void Dispose_WhenItemsViewIsMutated_ClearsTransferredData()
    {
        using var transaction = KittyClipboardTransaction.Read();
        _ = transaction.Accept(Packet("5522;type=read:status=OK"));
        _ = transaction.Accept(
            Packet("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==;c2VjcmV0"));
        _ = transaction.Accept(Packet("5522;type=read:status=DONE"));
        var result = transaction.Result.ShouldNotBeNull();
        var item = result.Items.ShouldHaveSingleItem();

        if (result.Items is KittyClipboardMimeData[] mutableItems)
        {
            mutableItems[0] = new KittyClipboardMimeData("application/decoy", [1]);
        }

        result.Dispose();

        item.Data.ToArray().ShouldBe(new byte[6]);
    }

    private static ClipboardPacket Packet(string wire) =>
        ClipboardPacket.Parse(Encoding.ASCII.GetBytes(wire));
}
