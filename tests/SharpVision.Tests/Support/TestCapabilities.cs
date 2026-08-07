// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides capability profiles for exercising protocol-dependent input semantics.</summary>
internal static class TestCapabilities
{
    /// <summary>Gets a profile whose Kitty keyboard evidence is authoritative, so Space uses the
    /// hold-then-release-completes model instead of the press-only immediate activation every
    /// legacy terminal gets.</summary>
    internal static TerminalCapabilities WithKeyReleases { get; } = TerminalCapabilities.Conservative with
    {
        KittyKeyboard = new Feature(CapabilitySupport.Supported, Origin.Query)
    };
}
