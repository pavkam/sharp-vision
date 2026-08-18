// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>
/// Verifies immutable conservative <see cref="TerminalCapabilities"/> profiles: the named
/// protocol-discovery facade, general profile invariants, and the pinned Unicode width policy.
/// </summary>
public sealed class TerminalCapabilitiesTests
{
    #region Protocol-discovery facade

    /// <summary>Verifies <c>Support</c> returns the same evidence as the matching property.</summary>
    [Fact]
    public void Support_WhenSixelUnknownOnConservative_ReturnsSameStateAsProperty()
    {
        var capabilities = TerminalCapabilities.Conservative;

        capabilities.Support(TerminalProtocol.Sixel).State.ShouldBe(capabilities.Sixel.State);
    }

    /// <summary>Verifies <c>Features</c> lists every protocol exactly once.</summary>
    [Fact]
    public void Features_WhenEnumerated_ListsEveryProtocolExactlyOnce()
    {
        var features = TerminalCapabilities.Conservative.Features;

        var protocolCount = Enum.GetValues<TerminalProtocol>().Length;
        features.Count.ShouldBe(protocolCount);
        features.Select(f => f.Protocol).Distinct().Count().ShouldBe(protocolCount);
    }

    /// <summary>Verifies <c>Support</c> rejects an undefined protocol.</summary>
    [Fact]
    public void Support_WhenProtocolUnknown_Throws()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            TerminalCapabilities.Conservative.Support((TerminalProtocol) 999));
    }

    #endregion

    #region Profile invariants

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

    #endregion

    #region Unicode width policy

    /// <summary>
    /// Verifies the conservative profile reports pinned narrow-width behavior.
    /// </summary>
    [Fact]
    public void Conservative_WhenRead_ReportsPinnedUnicodePolicy()
    {
        var capabilities = TerminalCapabilities.Conservative;

        capabilities.UnicodeVersion.ShouldBe(UnicodeInfo.Version);
        capabilities.AmbiguousWidth.ShouldBe(Ambiguous.Narrow);
    }

    #endregion
}
