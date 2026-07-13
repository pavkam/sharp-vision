// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

using Shouldly;

using CapabilitySupport = Terminal.Capabilities.Support;

/// <summary>
/// Verifies immutable conservative capability profiles.
/// </summary>
public sealed class CapabilitiesTests
{
    /// <summary>Verifies public capability values reject undefined evidence and invalid tokens.</summary>
    [Fact]
    public void Constructor_WhenCapabilityValueIsInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Feature((CapabilitySupport) int.MaxValue, Origin.Default));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Feature(CapabilitySupport.Unknown, (Origin) int.MaxValue));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new QueryToken(0));
    }

    /// <summary>
    /// Verifies optional protocols are never enabled by built-in defaults.
    /// </summary>
    [Fact]
    public void Conservative_WhenRead_EnablesNoOptionalFeature()
    {
        Capabilities capabilities = Capabilities.Conservative;

        capabilities.OptionalFeatures.ShouldAllBe(
            static feature => feature.State != CapabilitySupport.Supported);
        capabilities.ColorDepth.ShouldBe(ColorDepth.Basic16);
        capabilities.ColorOrigin.ShouldBe(Origin.Default);
    }

    /// <summary>
    /// Verifies deriving a profile cannot mutate the published original.
    /// </summary>
    [Fact]
    public void With_WhenProfileIsDerived_DoesNotMutateOriginal()
    {
        Capabilities original = Capabilities.Conservative;

        Capabilities derived = original with
        {
            Osc52 = new Feature(CapabilitySupport.Supported, Origin.Override),
        };

        original.Osc52.State.ShouldBe(CapabilitySupport.Unknown);
        derived.Osc52.State.ShouldBe(CapabilitySupport.Supported);
    }
}
