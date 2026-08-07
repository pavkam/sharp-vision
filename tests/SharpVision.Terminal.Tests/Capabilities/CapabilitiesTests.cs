// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

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

    /// <summary>Verifies a profile rejects an undefined color-evidence origin.</summary>
    [Fact]
    public void ColorOrigin_WhenUndefined_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new TerminalCapabilities { ColorOrigin = (Origin) int.MaxValue });
    }

    /// <summary>
    /// Verifies optional protocols are never enabled by built-in defaults.
    /// </summary>
    [Fact]
    public void Conservative_WhenRead_EnablesNoOptionalFeature()
    {
        var capabilities = TerminalCapabilities.Conservative;

        capabilities.Features.ShouldAllBe(static entry => entry.Feature.State != CapabilitySupport.Supported);
        capabilities.ColorDepth.ShouldBe(ColorDepth.Basic16);
        capabilities.ColorOrigin.ShouldBe(Origin.Default);
    }

    /// <summary>
    /// Verifies deriving a profile cannot mutate the published original.
    /// </summary>
    [Fact]
    public void With_WhenProfileIsDerived_DoesNotMutateOriginal()
    {
        var original = TerminalCapabilities.Conservative;

        var derived = original with { Osc52 = new Feature(CapabilitySupport.Supported, Origin.Override) };

        original.Osc52.State.ShouldBe(CapabilitySupport.Unknown);
        derived.Osc52.State.ShouldBe(CapabilitySupport.Supported);
    }

    /// <summary>
    /// Verifies the key-release behavioral capability mirrors Kitty keyboard support without
    /// requiring a caller to know which protocol provides it.
    /// </summary>
    [Fact]
    public void KeyReleaseEvents_WhenRead_MirrorsKittyKeyboard()
    {
        var unsupported = TerminalCapabilities.Conservative;

        unsupported.KeyReleaseEvents.State.ShouldBe(CapabilitySupport.Unknown);

        var supported = unsupported with
        {
            KittyKeyboard = new Feature(CapabilitySupport.Supported, Origin.Query)
        };

        supported.KeyReleaseEvents.Authoritative.ShouldBeTrue();
        supported.KeyReleaseEvents.ShouldBe(supported.KittyKeyboard);
    }
}
