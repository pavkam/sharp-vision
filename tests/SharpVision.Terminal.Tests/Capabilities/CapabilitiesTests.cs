using SharpVision.Terminal.Capabilities;

using Shouldly;

using CapabilitySupport = SharpVision.Terminal.Capabilities.Support;

namespace SharpVision.Terminal.Tests.Capabilities;

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
        var capabilities = Terminal.Capabilities.Capabilities.Conservative;

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
        var original = Terminal.Capabilities.Capabilities.Conservative;

        var derived = original with
        {
            Osc52 = new Feature(CapabilitySupport.Supported, Origin.Override),
        };

        original.Osc52.State.ShouldBe(CapabilitySupport.Unknown);
        derived.Osc52.State.ShouldBe(CapabilitySupport.Supported);
    }
}
