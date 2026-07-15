// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Unicode;



/// <summary>
/// Verifies terminal cell width for curated Unicode cluster families.
/// </summary>
public sealed class WidthTests
{
    /// <summary>
    /// Verifies basic narrow, wide, and emoji clusters use terminal-cell units.
    /// </summary>
    [Fact]
    public void Measure_WhenTextMixesAsciiCjkAndEmoji_ReturnsCellTotal()
    {
        var result = Width.Measure("A界👩‍💻".AsSpan(), Ambiguous.Narrow);

        result.Cells.ShouldBe(5);
        result.Graphemes.ShouldBe(3);
        result.Controls.ShouldBe(0);
    }

    /// <summary>
    /// Verifies ambiguous width changes only through explicit policy.
    /// </summary>
    [Fact]
    public void Measure_WhenScalarIsAmbiguous_UsesExplicitPolicy()
    {
        Width.Measure("·".AsSpan(), Ambiguous.Narrow).Cells.ShouldBe(1);
        Width.Measure("·".AsSpan(), Ambiguous.Wide).Cells.ShouldBe(2);
    }

    /// <summary>
    /// Verifies canonical equivalents have equal width without normalization.
    /// </summary>
    [Theory]
    [InlineData(Ambiguous.Narrow)]
    [InlineData(Ambiguous.Wide)]
    public void Measure_WhenTextIsCanonicallyEquivalent_ReturnsEqualWidth(Ambiguous ambiguous)
    {
        Width.Measure("é".AsSpan(), ambiguous).Cells.ShouldBe(
            Width.Measure("e\u0301".AsSpan(), ambiguous).Cells);
    }

    /// <summary>
    /// Verifies emoji/text variation selectors override default presentation.
    /// </summary>
    [Fact]
    public void Measure_WhenVariationSelectorChangesPresentation_UsesRequestedWidth()
    {
        Width.Measure("☀︎".AsSpan(), Ambiguous.Narrow).Cells.ShouldBe(1);
        Width.Measure("☀️".AsSpan(), Ambiguous.Narrow).Cells.ShouldBe(2);
    }

    /// <summary>
    /// Verifies terminal-recognized emoji sequences occupy one wide cluster.
    /// </summary>
    /// <param name="value">The emoji cluster.</param>
    [Theory]
    [InlineData("1️⃣")]
    [InlineData("🇵🇹")]
    [InlineData("👩🏽")]
    [InlineData("👩‍💻")]
    [InlineData("🏴󠁧󠁢󠁥󠁮󠁧󠁿")]
    public void Measure_WhenClusterIsEmojiSequence_ReturnsTwoCells(string value) =>
        Width.Measure(value.AsSpan(), Ambiguous.Narrow).Cells.ShouldBe(2);

    /// <summary>
    /// Verifies private-use and unassigned scalars default conservatively narrow.
    /// </summary>
    [Fact]
    public void Measure_WhenScalarHasNoPortableGlyphWidth_DefaultsNarrow()
    {
        Width.Measure("\ue000".AsSpan(), Ambiguous.Wide).Cells.ShouldBe(1);
        Width.Measure("\u0378".AsSpan(), Ambiguous.Wide).Cells.ShouldBe(1);
    }

    /// <summary>
    /// Verifies an orphan mark and invalid UTF-16 remain repairable cells.
    /// </summary>
    [Fact]
    public void Measure_WhenClusterHasNoPrintableBase_ReturnsOneCell()
    {
        Width.Measure("\u0301".AsSpan(), Ambiguous.Wide).Cells.ShouldBe(1);
        Width.Measure("\ud800".AsSpan(), Ambiguous.Wide).Cells.ShouldBe(1);
    }

    /// <summary>
    /// Verifies unknown policy values are rejected before enumeration.
    /// </summary>
    [Fact]
    public void Measure_WhenAmbiguousPolicyIsUnknown_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Width.Measure("x".AsSpan(), (Ambiguous) 99));
    }
}
