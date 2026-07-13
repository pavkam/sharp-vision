// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Clipboard;

using SharpVision.Terminal.Clipboard;
using SharpVision.Terminal.Protocols;

using Shouldly;

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
        using KittyTransaction transaction = KittyTransaction.Read(id: "req-1");

        transaction.Accept(Packet("5522;type=read:status=OK:id=req-1")).ShouldBe(
            KittyAcceptResult.Accepted);
        transaction.Accept(
            Packet("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=req-1;aGVs"))
            .ShouldBe(KittyAcceptResult.Accepted);
        transaction.Accept(
            Packet("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=req-1;bG8="))
            .ShouldBe(KittyAcceptResult.Accepted);
        transaction.Accept(Packet("5522;type=read:status=DONE:id=req-1")).ShouldBe(
            KittyAcceptResult.Completed);
        transaction.Accept(
            Packet("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=req-1;eA=="))
            .ShouldBe(KittyAcceptResult.Ignored);

        transaction.State.ShouldBe(KittyTransactionState.Completed);
        KittyResult result = transaction.Result.ShouldNotBeNull();
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
        using KittyTransaction transaction = KittyTransaction.Read(listOnly: true);
        _ = transaction.Accept(Packet("5522;type=read:status=OK"));
        _ = transaction.Accept(Packet("5522;type=read:status=DATA;dGV4dC9wbGFpbg=="));

        transaction.Accept(Packet("5522;type=read:status=DONE")).ShouldBe(
            KittyAcceptResult.Completed);

        KittyResult result = transaction.Result.ShouldNotBeNull();
        result.Items.ShouldHaveSingleItem().Data.ToArray().ShouldBe("text/plain"u8.ToArray());
        result.Dispose();
    }

    /// <summary>
    /// Verifies write completion accepts the single documented DONE response.
    /// </summary>
    [Fact]
    public void Accept_WhenWriteReturnsDone_Completes()
    {
        using KittyTransaction transaction = KittyTransaction.Write(id: "write-1");

        KittyAcceptResult result = transaction.Accept(
            Packet("5522;type=write:status=DONE:id=write-1"));

        result.ShouldBe(KittyAcceptResult.Completed);
        transaction.State.ShouldBe(KittyTransactionState.Completed);
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
        using KittyTransaction transaction = KittyTransaction.Read();
        KittyPacket packet = Packet($"5522;type=read:status={status}");

        transaction.Accept(packet).ShouldBe(KittyAcceptResult.Failed);

        transaction.State.ShouldBe(KittyTransactionState.Failed);
        transaction.Failure.ShouldNotBe(KittyReplyStatus.None);
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
        using KittyTransaction transaction = KittyTransaction.Read();

        foreach (var packet in new[] { first, second, third, fourth })
        {
            if (packet is not null)
            {
                _ = transaction.Accept(Packet(packet));
            }
        }

        transaction.State.ShouldBe(KittyTransactionState.Failed);
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
        using KittyTransaction transaction = KittyTransaction.Read(id: "req-1");

        transaction.Accept(Packet(wire)).ShouldBe(KittyAcceptResult.Ignored);

        transaction.State.ShouldBe(KittyTransactionState.Created);
    }

    /// <summary>
    /// Verifies malformed packets and data limits fail with redacted diagnostics.
    /// </summary>
    [Fact]
    public void Accept_WhenPacketOrDataIsInvalid_FailsAndClearsBuffers()
    {
        Limits limits = Limits.Default with { MaxClipboardBytes = 1 };
        using KittyTransaction malformed = KittyTransaction.Read(limits);
        using KittyTransaction oversized = KittyTransaction.Read(limits);

        malformed.Accept(KittyPacket.Parse("5522;type=read;***"u8)).ShouldBe(
            KittyAcceptResult.Failed);
        _ = oversized.Accept(Packet("5522;type=read:status=OK"));
        oversized.Accept(
            Packet("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==;YWI="))
            .ShouldBe(KittyAcceptResult.Failed);

        malformed.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.InvalidBase64);
        oversized.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
        malformed.ToString().ShouldNotContain("***");
    }

    /// <summary>
    /// Verifies a terminal cannot bypass the protocol's per-packet chunk bound.
    /// </summary>
    [Fact]
    public void Accept_WhenDataChunkExceeds4096Bytes_Fails()
    {
        var encoded = Convert.ToBase64String(new byte[4_097]);
        using KittyTransaction transaction = KittyTransaction.Read();
        _ = transaction.Accept(Packet("5522;type=read:status=OK"));

        KittyAcceptResult result = transaction.Accept(Packet(
            $"5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==;{encoded}"));

        result.ShouldBe(KittyAcceptResult.Failed);
        transaction.Diagnostic!.Value.Code.ShouldBe(DiagnosticCode.StringLimit);
    }

    /// <summary>
    /// Verifies explicit cancellation is terminal and late packets are ignored.
    /// </summary>
    [Fact]
    public void Cancel_WhenActive_CancelsAndIgnoresLatePackets()
    {
        using KittyTransaction transaction = KittyTransaction.Read();

        transaction.Cancel();

        transaction.State.ShouldBe(KittyTransactionState.Cancelled);
        transaction.Accept(Packet("5522;type=read:status=OK")).ShouldBe(
            KittyAcceptResult.Ignored);
    }

    /// <summary>
    /// Verifies timeout uses the injected clock without wall-clock waiting.
    /// </summary>
    [Fact]
    public void CheckTimeout_WhenDeadlinePasses_TimesOut()
    {
        ManualTimeProvider clock = new ManualTimeProvider();
        Limits limits = Limits.Default with { QueryTimeout = TimeSpan.FromSeconds(2) };
        using KittyTransaction transaction = KittyTransaction.Read(limits, timeProvider: clock);

        clock.Advance(TimeSpan.FromSeconds(2));

        transaction.CheckTimeout().ShouldBeTrue();
        transaction.State.ShouldBe(KittyTransactionState.TimedOut);
        transaction.Accept(Packet("5522;type=read:status=OK")).ShouldBe(
            KittyAcceptResult.Ignored);
    }

    /// <summary>
    /// Verifies disposing an owned result clears its publicly observable bytes.
    /// </summary>
    [Fact]
    public void Dispose_WhenResultOwnsData_ClearsData()
    {
        using KittyTransaction transaction = KittyTransaction.Read();
        _ = transaction.Accept(Packet("5522;type=read:status=OK"));
        _ = transaction.Accept(
            Packet("5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==;c2VjcmV0"));
        _ = transaction.Accept(Packet("5522;type=read:status=DONE"));
        KittyResult result = transaction.Result.ShouldNotBeNull();
        ReadOnlyMemory<byte> data = result.Items.ShouldHaveSingleItem().Data;

        result.Dispose();

        data.ToArray().ShouldBe(new byte[6]);
    }

    private static KittyPacket Packet(string wire) =>
        KittyPacket.Parse(Encoding.ASCII.GetBytes(wire));

}
