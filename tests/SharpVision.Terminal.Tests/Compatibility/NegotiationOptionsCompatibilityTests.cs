// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Compatibility;

using SharpVision.Terminal.Capabilities;

/// <summary>Freezes the original public startup-negotiation constructor ABI.</summary>
public sealed class NegotiationOptionsCompatibilityTests
{
    /// <summary>Verifies consumer source and reflection retain the exact shipped three-parameter constructor.</summary>
    [Fact]
    public void Constructor_WhenUsedByExistingConsumer_PreservesSourceRuntimeAndClrSignature()
    {
        IReadOnlyDictionary<string, string?> environment =
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color" };
        var overrides = new Settings { ColorDepth = ColorDepth.Indexed256 };
        var limits = Limits.Default with { MaxConcurrentQueries = 3 };

        var sourceCompatible = new NegotiationOptions(environment, overrides, limits);
        var constructor = typeof(NegotiationOptions).GetConstructor(
        [
            typeof(IReadOnlyDictionary<string, string?>),
            typeof(Settings),
            typeof(Limits)
        ]);
        var exactConstructor = constructor.ShouldNotBeNull();
        var runtimeCompatible = exactConstructor.Invoke([environment, overrides, limits]);

        sourceCompatible.Environment["TERM"].ShouldBe("xterm-256color");
        sourceCompatible.Overrides.ShouldBeSameAs(overrides);
        sourceCompatible.Limits.ShouldBeSameAs(limits);
        var typedRuntime = runtimeCompatible.ShouldBeOfType<NegotiationOptions>();
        typedRuntime.Limits.ShouldBeSameAs(limits);
    }
}
