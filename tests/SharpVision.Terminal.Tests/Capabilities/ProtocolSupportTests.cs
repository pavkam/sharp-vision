// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>Verifies protocol/feature pair validation and exposure.</summary>
public sealed class ProtocolSupportTests
{
    /// <summary>Verifies the constructor exposes both protocol and feature.</summary>
    [Fact]
    public void Constructor_WhenGivenProtocolAndFeature_ExposesBoth()
    {
        Feature feature = new(Support.Supported, Origin.Query);
        ProtocolSupport pair = new(TerminalProtocol.Sixel, feature);

        pair.Protocol.ShouldBe(TerminalProtocol.Sixel);
        pair.Feature.ShouldBe(feature);
    }
}
