// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using System.Buffers;
using System.Text;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;

using Shouldly;

using CapabilitySupport = Terminal.Capabilities.Support;

/// <summary>Verifies bounded startup query encoding and profile publication.</summary>
public sealed class NegotiatorTests
{
    /// <summary>Verifies DA closes an unanswered ordered Kitty probe.</summary>
    [Fact]
    public void Accept_WhenDaPrecedesKeyboard_PublishesUnsupportedKeyboard()
    {
        // Arrange
        Limits limits = Limits.Default with { MaxConcurrentQueries = 2 };
        Negotiator negotiator = new Negotiator(new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" },
            limits: limits));
        negotiator.Start(new ArrayBufferWriter<byte>());
        Response attributes = Response("?1;2"u8, [], (byte) 'c');

        // Act
        negotiator.Accept(in attributes).ShouldBe(QueryMatch.Matched);

        // Assert
        negotiator.IsComplete.ShouldBeTrue();
        Capabilities published = negotiator.Capabilities;
        published.KittyKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Query));
        Response keyboard = Response("?3"u8, [], (byte) 'u');
        negotiator.Accept(in keyboard).ShouldBe(QueryMatch.Late);
        negotiator.Capabilities.ShouldBeSameAs(published);
    }

    /// <summary>Verifies mode duplicates and unrelated reports cannot consume work.</summary>
    [Fact]
    public void Accept_WhenModeIsRepeatedOrUnknown_ClassifiesWithoutMutation()
    {
        // Arrange
        Negotiator negotiator = new Negotiator(
            new NegotiationOptions(new Dictionary<string, string?>()));
        negotiator.Start(new ArrayBufferWriter<byte>());
        Response synchronized = PrivateMode(2026, state: 1);
        Response unknown = PrivateMode(25, state: 1);

        // Act / Assert
        negotiator.Accept(in synchronized).ShouldBe(QueryMatch.Matched);
        negotiator.Accept(in synchronized).ShouldBe(QueryMatch.Duplicate);
        negotiator.LastDiagnostic!.Value.Code.ShouldBe(
            DiagnosticCode.DuplicateResponse);
        negotiator.Accept(in unknown).ShouldBe(QueryMatch.Unknown);
        negotiator.IsComplete.ShouldBeFalse();
    }

    /// <summary>Verifies one deadline publishes fallback and rejects later mutation.</summary>
    [Fact]
    public void Expire_WhenDeadlineElapses_PublishesOnceAndClassifiesLateReply()
    {
        // Arrange
        ManualTimeProvider clock = new ManualTimeProvider();
        Limits limits = Limits.Default with
        {
            QueryTimeout = TimeSpan.FromSeconds(1),
        };
        NegotiationOptions options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" },
            new Settings { SynchronizedOutput = false },
            limits);
        Negotiator negotiator = new Negotiator(options, clock);
        negotiator.Start(new ArrayBufferWriter<byte>());

        // Act / Assert
        negotiator.Expire().ShouldBeFalse();
        negotiator.IsComplete.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(1));
        negotiator.Expire().ShouldBeTrue();
        Capabilities published = negotiator.Capabilities;
        published.KittyKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Tentative, Origin.Environment));
        published.SynchronizedOutput.ShouldBe(
            new Feature(CapabilitySupport.Unsupported, Origin.Override));
        negotiator.Expire().ShouldBeFalse();
        negotiator.Capabilities.ShouldBeSameAs(published);

        Response late = PrivateMode(2026, state: 1);
        negotiator.Accept(in late).ShouldBe(QueryMatch.Late);
        negotiator.Capabilities.ShouldBeSameAs(published);
    }

    /// <summary>Verifies out-of-order replies publish one query-origin profile.</summary>
    [Fact]
    public void Accept_WhenRepliesArriveOutOfOrder_PublishesCompleteProfile()
    {
        // Arrange
        Negotiator negotiator = new Negotiator(
            new NegotiationOptions(new Dictionary<string, string?>()));
        negotiator.Start(new ArrayBufferWriter<byte>());

        // Act / Assert
        int[] modes = [1016, 1006, 2004, 1004, 2026];

        foreach (var mode in modes)
        {
            Response response = PrivateMode(mode, state: 1);
            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        }

        Response keyboard = Response("?3"u8, [], (byte) 'u');
        negotiator.Accept(in keyboard).ShouldBe(QueryMatch.Matched);
        Response attributes = Response("?1;2"u8, [], (byte) 'c');
        negotiator.Accept(in attributes).ShouldBe(QueryMatch.Matched);

        negotiator.IsComplete.ShouldBeTrue();
        Capabilities capabilities = negotiator.Capabilities;
        capabilities.SynchronizedOutput.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.FocusReporting.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.BracketedPaste.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.CellMouse.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.PixelMouse.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
        capabilities.KittyKeyboard.ShouldBe(
            new Feature(CapabilitySupport.Supported, Origin.Query));
    }

    /// <summary>Verifies the configured query limit truncates by fixed priority.</summary>
    /// <param name="capacity">The maximum concurrent query count.</param>
    /// <param name="expected">The exact expected startup bytes.</param>
    [Theory]
    [InlineData(1, "\u001b[c")]
    [InlineData(2, "\u001b[?u\u001b[c")]
    [InlineData(3, "\u001b[?u\u001b[c\u001b[?2026$p")]
    [InlineData(4, "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p")]
    [InlineData(5, "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p\u001b[?2004$p")]
    [InlineData(6, "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p\u001b[?2004$p\u001b[?1006$p")]
    [InlineData(7, "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p")]
    public void Start_WhenCapacityVaries_TruncatesByPriority(
        int capacity,
        string expected)
    {
        // Arrange
        Limits limits = Limits.Default with
        {
            MaxConcurrentQueries = capacity,
        };
        NegotiationOptions options = new NegotiationOptions(
            new Dictionary<string, string?>(),
            limits: limits);
        Negotiator negotiator = new Negotiator(options, new ManualTimeProvider());
        ArrayBufferWriter<byte> output = new ArrayBufferWriter<byte>();

        // Act
        negotiator.Start(output);

        // Assert
        Encoding.ASCII.GetString(output.WrittenSpan).ShouldBe(expected);
    }

    /// <summary>Verifies the default batch is safe, exact, and ordered.</summary>
    [Fact]
    public void Start_WhenDefaultCapacityIsAvailable_WritesSafeQueriesInOrder()
    {
        // Arrange
        NegotiationOptions options = new NegotiationOptions(
            new Dictionary<string, string?>());
        Negotiator negotiator = new Negotiator(options, new ManualTimeProvider());
        ArrayBufferWriter<byte> output = new ArrayBufferWriter<byte>();

        // Act
        negotiator.Start(output);

        // Assert
        Encoding.ASCII.GetString(output.WrittenSpan).ShouldBe(
            "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p" +
            "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p");
        negotiator.IsComplete.ShouldBeFalse();
    }

    /// <summary>Verifies invalid calls are rejected without ambiguous state.</summary>
    [Fact]
    public void Start_WhenStateIsInvalid_ThrowsDeterministically()
    {
        // Arrange
        Negotiator negotiator = new Negotiator(
            new NegotiationOptions(new Dictionary<string, string?>()));

        // Act / Assert
        _ = Should.Throw<ArgumentNullException>(() => negotiator.Start(null!));
        negotiator.IsStarted.ShouldBeFalse();
        _ = Should.Throw<InvalidOperationException>(() => _ = negotiator.Capabilities);
        Response response = PrivateMode(2026, state: 1);
        _ = Should.Throw<InvalidOperationException>(() => negotiator.Accept(in response));
        _ = Should.Throw<InvalidOperationException>(() => negotiator.Expire());
        ArrayBufferWriter<byte> output = new ArrayBufferWriter<byte>();
        negotiator.Start(output);
        _ = Should.Throw<InvalidOperationException>(() => negotiator.Start(output));
    }

    private static Response PrivateMode(int mode, int state)
    {
        var parameters = Encoding.ASCII.GetBytes($"?{mode};{state}");
        return Response(parameters, "$"u8, (byte) 'y');
    }

    private static Response Response(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final)
    {
        Responses.TryCsi(
            parameters,
            intermediates,
            final,
            out Response response).ShouldBeTrue();
        return response;
    }
}
