// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the immutable reserved-column metrics <see cref="ControlBase.MeasureAffixes"/> returns.</summary>
public sealed class AffixMetricsTests
{
    /// <summary>Verifies both constructor arguments round-trip to their identically named properties.</summary>
    [Fact]
    public void Constructor_WhenColumnsAreNonNegative_PreservesStartAndEndCells()
    {
        var metrics = new AffixMetrics(startCells: 3, endCells: 5);

        metrics.StartCells.ShouldBe(3);
        metrics.EndCells.ShouldBe(5);
    }

    /// <summary>Verifies the default value reports zero reserved columns on both edges, matching an
    /// unset affix pair.</summary>
    [Fact]
    public void Default_WhenUnset_ReportsZeroOnBothEdges()
    {
        var metrics = default(AffixMetrics);

        metrics.StartCells.ShouldBe(0);
        metrics.EndCells.ShouldBe(0);
    }

    /// <summary>Verifies a negative start or end column count throws before construction.</summary>
    [Fact]
    public void Constructor_WhenAColumnCountIsNegative_ThrowsBeforeConstruction()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new AffixMetrics(startCells: -1, endCells: 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new AffixMetrics(startCells: 0, endCells: -1));
    }
}
