using SharpVision.Terminal.Unicode;

using Shouldly;

namespace SharpVision.Terminal.Tests.Unicode;

/// <summary>Verifies width and safe presentation classification for one cluster.</summary>
public sealed class ClusterTests
{
    /// <summary>Verifies base-less component families require one replacement cell.</summary>
    /// <param name="value">The single base-less grapheme cluster.</param>
    [Theory]
    [InlineData("\u0301")]
    [InlineData("\u0903")]
    [InlineData("\u0600")]
    [InlineData("\u200d")]
    [InlineData("\ufe0e")]
    [InlineData("\ufe0f")]
    [InlineData("🏽")]
    [InlineData("\U000e0067")]
    [InlineData("\u0301\u200d")]
    public void AnalyzeCluster_WhenClusterHasNoBase_RequiresReplacement(string value)
    {
        // Act
        var cluster = Width.AnalyzeCluster(value.AsSpan(), Ambiguous.Narrow);

        // Assert
        cluster.Width.ShouldBe(CellWidth.Narrow);
        cluster.RequiresReplacement.ShouldBeTrue();
    }

    /// <summary>Verifies valid bases preserve their source presentation.</summary>
    /// <param name="value">The valid base-bearing cluster.</param>
    /// <param name="expected">The expected cell width.</param>
    [Theory]
    [InlineData("a", CellWidth.Narrow)]
    [InlineData("e\u0301", CellWidth.Narrow)]
    [InlineData("界", CellWidth.Wide)]
    [InlineData("👩‍💻", CellWidth.Wide)]
    [InlineData("\ue000", CellWidth.Narrow)]
    public void AnalyzeCluster_WhenClusterHasBase_PreservesPresentation(
        string value,
        CellWidth expected)
    {
        // Act
        var cluster = Width.AnalyzeCluster(value.AsSpan(), Ambiguous.Narrow);

        // Assert
        cluster.Width.ShouldBe(expected);
        cluster.RequiresReplacement.ShouldBeFalse();
    }

    /// <summary>Verifies invalid UTF-16 receives the existing replacement presentation.</summary>
    [Fact]
    public void AnalyzeCluster_WhenUtf16IsInvalid_RequiresReplacement()
    {
        // Act
        var cluster = Width.AnalyzeCluster("\ud800".AsSpan(), Ambiguous.Narrow, hasInvalidData: true);

        // Assert
        cluster.Width.ShouldBe(CellWidth.Narrow);
        cluster.RequiresReplacement.ShouldBeTrue();
    }

    /// <summary>Verifies controls remain contextual rather than replacement cells.</summary>
    [Fact]
    public void AnalyzeCluster_WhenClusterIsControl_RemainsContextual()
    {
        // Act
        var cluster = Width.AnalyzeCluster("\n".AsSpan(), Ambiguous.Narrow);

        // Assert
        cluster.Width.ShouldBe(CellWidth.Control);
        cluster.RequiresReplacement.ShouldBeFalse();
    }

    /// <summary>Verifies the default policy pins explicit Unicode geometry choices.</summary>
    [Fact]
    public void Default_WhenRead_UsesPinnedNarrowReplacementPolicy()
    {
        Policy.Default.UnicodeVersion.ShouldBe(Info.Version);
        Policy.Default.AmbiguousWidth.ShouldBe(Ambiguous.Narrow);
        Policy.Default.OrphanPresentation.ShouldBe(Presentation.Replacement);
    }

    /// <summary>Verifies policy validation rejects unknown values before assignment.</summary>
    [Fact]
    public void Constructor_WhenPolicyValueIsUnknown_Throws()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new Policy((Ambiguous) 99, Presentation.Replacement));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new Policy(Ambiguous.Narrow, (Presentation) 99));
    }
}
