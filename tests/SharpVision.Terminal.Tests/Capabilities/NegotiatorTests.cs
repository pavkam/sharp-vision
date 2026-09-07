// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

using SharpVision.Terminal.Discovery.Queries;

/// <summary>Verifies the public negotiator compatibility facade.</summary>
public sealed class NegotiatorTests
{
    /// <summary>Verifies Screen queries split into local CSI and wrapped outer CSI without string work.</summary>
    [Fact]
    public void Start_WhenScreenRouteIsActive_OmitsStringFamiliesWithoutDeadlineWork()
    {
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Screen], TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All, paneVisible: true, MultiplexingOperation.CapabilityQueries);
        var route = new MultiplexerRoute(policy);
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "screen-256color" },
            overrides: null, limits: QueryLimits.Default, multiplexing: policy);
        var negotiator = new Negotiator(options, new ManualTimeProvider());
        var destination = new ArrayBufferWriter<byte>();

        negotiator.Start(destination, localCells: null, localPixels: null, route);

        destination.WrittenSpan.ToArray().ShouldBe(Encoding.ASCII.GetBytes(
            "\u001b[?u\u001b[?2026$p\u001b[?2027$p\u001b[?1004$p\u001b[?2004$p" +
            "\u001b[?1006$p\u001b[?1016$p\u001b[14t\u001b[16t\u001b[18t\u001b[6n" +
            "\u001bP\u001b[>c\u001b[?5522$p\u001b[c\u001b\\"));

        var keyboard = new XtermCapabilitiesResponse(ResponseKind.Keyboard, [3]);
        negotiator.Accept(in keyboard).ShouldBe(QueryMatch.Matched);

        foreach (var mode in new[] { 1016, 1006, 2004, 1004, 2026, 2027, 5522 })
        {
            var response = new XtermCapabilitiesResponse(ResponseKind.PrivateMode, [mode, 1], isSupported: true);
            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        }

        foreach (var parameters in new[] { "8;40;120"u8.ToArray(), "6;20;10"u8.ToArray(), "4;800;1200"u8.ToArray() })
        {
            XtermResponses.TryMetricsCsi(parameters, [], (byte) 't', out var response).ShouldBeTrue();
            negotiator.Accept(in response).ShouldBe(QueryMatch.Matched);
        }

        var secondary = new XtermCapabilitiesResponse(ResponseKind.SecondaryAttributes, [41, 410, 0]);
        negotiator.Accept(in secondary).ShouldBe(QueryMatch.Matched);
        var primary = new XtermCapabilitiesResponse(ResponseKind.PrimaryAttributes, [1, 2]);
        negotiator.Accept(in primary).ShouldBe(QueryMatch.Matched);
        var cursor = new XtermCapabilitiesResponse(ResponseKind.CursorPosition, [24, 80]);
        negotiator.Accept(in cursor).ShouldBe(QueryMatch.Matched);

        negotiator.Completed.ShouldBeTrue();
        negotiator.Results.PaletteColor.ShouldBeNull();
        negotiator.Results.ForegroundColor.ShouldBeNull();
        negotiator.Results.BackgroundColor.ShouldBeNull();
        negotiator.Results.CapabilityString.ShouldBeNull();
        negotiator.Results.XtermKeyboard.ShouldBeNull();
    }

    /// <summary>Verifies public construction and incomplete-lifecycle validation remain available on the facade.</summary>
    [Fact]
    public void Negotiator_WhenUnstarted_PreservesPublicValidation()
    {
        // Arrange
        var negotiator = new Negotiator(new NegotiationOptions(new Dictionary<string, string?>()));
        var response = new XtermCapabilitiesResponse(ResponseKind.PrimaryAttributes, [1, 2]);

        // Act / Assert
        _ = Should.Throw<ArgumentNullException>(() => new Negotiator(null!));
        _ = Should.Throw<ArgumentNullException>(() => negotiator.Start(null!));
        _ = Should.Throw<InvalidOperationException>(() => _ = negotiator.Capabilities);
        _ = Should.Throw<InvalidOperationException>(() => _ = negotiator.Results);
        _ = Should.Throw<InvalidOperationException>(() => negotiator.Accept(in response));
        _ = Should.Throw<InvalidOperationException>(() => negotiator.Expire());
    }

    /// <summary>Verifies the facade preserves strategy bytes, classification, publication, and reference semantics.</summary>
    [Fact]
    public void Negotiator_WhenDelegating_MatchesActiveQueryDiscoveryStrategy()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var options = new NegotiationOptions(
            new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" },
            limits: QueryLimits.Default with { MaxConcurrentQueries = 2 });
        var strategy = new ActiveQueryDiscoveryStrategy(options, clock);
        var facade = new Negotiator(options, clock);
        var strategyBytes = new ArrayBufferWriter<byte>();
        var facadeBytes = new ArrayBufferWriter<byte>();
        var response = new XtermCapabilitiesResponse(ResponseKind.PrimaryAttributes, [1, 2]);

        // Act
        strategy.TryStart(strategyBytes, null, null).ShouldBeTrue();
        facade.Start(facadeBytes);
        var strategyMatch = strategy.Accept(in response);
        var facadeMatch = facade.Accept(in response);
        var strategyCompleted = strategy.Complete();
        var facadeCompleted = facade.Complete();

        // Assert
        strategyBytes.WrittenSpan.ToArray().ShouldBe(facadeBytes.WrittenSpan.ToArray());
        strategy.Started.ShouldBe(facade.Started);
        strategy.Completed.ShouldBe(facade.Completed);
        strategy.Deadline.ShouldBe(facade.Deadline);
        strategyMatch.ShouldBe(facadeMatch);
        strategyCompleted.ShouldBe(facadeCompleted);
        strategy.LastDiagnostic.ShouldBe(facade.LastDiagnostic);
        strategy.Results.ShouldBe(facade.Results);
        strategy.Capabilities.ShouldBe(facade.Capabilities);
        strategy.Results.ShouldBeSameAs(strategy.Results);
        strategy.Capabilities.ShouldBeSameAs(strategy.Capabilities);
        facade.Results.ShouldBeSameAs(facade.Results);
        facade.Capabilities.ShouldBeSameAs(facade.Capabilities);
        strategy.Complete().ShouldBeFalse();
        facade.Complete().ShouldBeFalse();
    }
}
