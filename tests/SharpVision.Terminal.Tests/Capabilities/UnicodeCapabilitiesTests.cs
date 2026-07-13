// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Unicode;

using Shouldly;

/// <summary>
/// Verifies Unicode width policy is explicit in immutable capabilities.
/// </summary>
public sealed class UnicodeCapabilitiesTests
{
    /// <summary>
    /// Verifies the conservative profile reports pinned narrow-width behavior.
    /// </summary>
    [Fact]
    public void Conservative_WhenRead_ReportsPinnedUnicodePolicy()
    {
        Capabilities capabilities = Capabilities.Conservative;

        capabilities.UnicodeVersion.ShouldBe(Info.Version);
        capabilities.AmbiguousWidth.ShouldBe(Ambiguous.Narrow);
    }

    /// <summary>
    /// Verifies callers may explicitly select wide ambiguous characters.
    /// </summary>
    [Fact]
    public void Detect_WhenAmbiguousWidthIsOverridden_AppliesOverrideLast()
    {
        Capabilities capabilities = Detector.Detect(
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color" },
            overrides: new Settings { AmbiguousWidth = Ambiguous.Wide });

        capabilities.AmbiguousWidth.ShouldBe(Ambiguous.Wide);
    }
}
