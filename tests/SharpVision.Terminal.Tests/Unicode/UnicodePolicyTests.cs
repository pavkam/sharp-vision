// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Unicode;

/// <summary>Verifies the pinned default policy and constructor validation for <see cref="UnicodePolicy"/>.</summary>
public sealed class UnicodePolicyTests
{
    /// <summary>Verifies the default policy pins explicit Unicode geometry choices.</summary>
    [Fact]
    public void Default_WhenRead_UsesPinnedNarrowReplacementPolicy()
    {
        UnicodePolicy.Default.UnicodeVersion.ShouldBe(UnicodeInfo.Version);
        UnicodePolicy.Default.AmbiguousWidth.ShouldBe(Ambiguous.Narrow);
        UnicodePolicy.Default.OrphanPresentation.ShouldBe(Presentation.Replacement);
    }

    /// <summary>Verifies policy validation rejects unknown values before assignment.</summary>
    [Fact]
    public void Constructor_WhenPolicyValueIsUnknown_Throws()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new UnicodePolicy((Ambiguous) 99));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new UnicodePolicy(Ambiguous.Narrow, (Presentation) 99));
    }
}
