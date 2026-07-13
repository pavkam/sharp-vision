// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

using Shouldly;

using TerminalCapabilities = Terminal.Capabilities.Capabilities;

/// <summary>Verifies color-depth profile validation and conservative defaults.</summary>
public sealed class ColorDepthTests
{
    /// <summary>Verifies the conservative profile permits only classic 16-color output.</summary>
    [Fact]
    public void Conservative_WhenRead_UsesBasic16ColorDepth() =>
        TerminalCapabilities.Conservative.ColorDepth.ShouldBe(ColorDepth.Basic16);

    /// <summary>Verifies an unknown depth fails before profile construction completes.</summary>
    [Fact]
    public void ColorDepth_WhenValueIsUnknown_ThrowsDuringInitialization()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new TerminalCapabilities { ColorDepth = (ColorDepth) 999 });
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Settings { ColorDepth = (ColorDepth) 999 });
    }
}
