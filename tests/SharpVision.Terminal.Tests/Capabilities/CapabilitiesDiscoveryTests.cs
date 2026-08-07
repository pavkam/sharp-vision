// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>
/// Verifies the named protocol-discovery facade on <see cref="TerminalCapabilities"/>.
/// </summary>
public sealed class CapabilitiesDiscoveryTests
{
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
}
