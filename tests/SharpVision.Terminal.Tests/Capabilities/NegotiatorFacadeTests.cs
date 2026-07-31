// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

using SharpVision.Terminal.Discovery.Queries;

/// <summary>Verifies the public negotiator compatibility facade.</summary>
public sealed class NegotiatorFacadeTests
{
    /// <summary>Verifies public construction and incomplete-lifecycle validation remain available on the facade.</summary>
    [Fact]
    public void Negotiator_WhenUnstarted_PreservesPublicValidation()
    {
        // Arrange
        var negotiator = new Negotiator(new NegotiationOptions(new Dictionary<string, string?>()));
        var response = new Response(ResponseKind.PrimaryAttributes, [1, 2]);

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
        var response = new Response(ResponseKind.PrimaryAttributes, [1, 2]);

        // Act
        strategy.TryStart(strategyBytes, null, null).ShouldBeTrue();
        facade.Start(facadeBytes);
        var strategyMatch = strategy.Accept(in response);
        var facadeMatch = facade.Accept(in response);
        var strategyCompleted = strategy.Complete();
        var facadeCompleted = facade.Complete();

        // Assert
        strategyBytes.WrittenSpan.ToArray().ShouldBe(facadeBytes.WrittenSpan.ToArray());
        strategy.IsStarted.ShouldBe(facade.IsStarted);
        strategy.IsComplete.ShouldBe(facade.IsComplete);
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
