// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Covers three unrelated contracts that each promised something the code did not do: the
/// theme loader's embedded-versus-external rules, a border side's independence in a degenerate
/// dimension, and a dialog style's responsibility for the member it forwards to a retained
/// child.</summary>
public sealed class LoaderAndChromeContractTests
{
    /// <summary>Verifies a one-column control with only its right edge enabled actually paints it.
    ///
    /// <para>The degenerate-dimension guards exist so a one-cell dimension is not painted twice, but
    /// fired even when the edge they guard against was disabled - so the only enabled edge was
    /// skipped and nothing was drawn, while <c>BorderInset</c> still reserved the column. The space
    /// was paid for and left blank.</para>
    /// </summary>
    [Fact]
    public async Task Render_WhenOnlyTheRightEdgeIsEnabledInAOneColumnBox_PaintsItAsync()
    {
        await using var surface = await MountBorderedAsync(BorderSide.Right, new Size(1, 3));

        Cells(surface, 1, 3).ShouldNotBe("   ", "the reserved column must be painted");
    }

    /// <summary>The same for a one-row control whose only horizontal edge is the bottom one.</summary>
    [Fact]
    public async Task Render_WhenOnlyTheBottomEdgeIsEnabledInAOneRowBox_PaintsItAsync()
    {
        await using var surface = await MountBorderedAsync(BorderSide.Bottom, new Size(4, 1));

        Cells(surface, 4, 1).ShouldNotBe("    ");
    }

    /// <summary>The report's case B: three sides on a one-row rect rendered entirely blank, because
    /// the top edge was inactive, the bottom was gated out, and both verticals computed an empty
    /// span.</summary>
    [Fact]
    public async Task Render_WhenThreeSidesMeetInAOneRowBox_PaintsTheRowAsync()
    {
        await using var surface = await MountBorderedAsync(
            BorderSide.Left | BorderSide.Right | BorderSide.Bottom,
            new Size(6, 1));

        Cells(surface, 6, 1).ShouldNotBe("      ");
    }

    /// <summary>The counter-case the guards exist for: a one-row box with both horizontal edges
    /// enabled must not paint the row twice. The top edge owns it.</summary>
    [Fact]
    public async Task Render_WhenBothHorizontalEdgesShareAOneRowBox_DrawsTheTopEdgeOnlyAsync()
    {
        await using var surface = await MountBorderedAsync(
            BorderSide.Top | BorderSide.Bottom,
            new Size(4, 1));

        Cells(surface, 4, 1).ShouldBe(new string(BorderGlyphStyle.Heavy.Top.ToString()[0], 4));
    }

    /// <summary>The same counter-case for the vertical pair in a one-column box.</summary>
    [Fact]
    public async Task Render_WhenBothVerticalEdgesShareAOneColumnBox_DrawsTheLeftEdgeOnlyAsync()
    {
        await using var surface = await MountBorderedAsync(
            BorderSide.Left | BorderSide.Right,
            new Size(1, 3));

        Cells(surface, 1, 3).ShouldBe(new string(BorderGlyphStyle.Heavy.Left.ToString()[0], 3));
    }

    /// <summary>Verifies an external document missing <c>colorScheme</c> is accepted by every
    /// external entry point, which is the documented lenient rule.</summary>
    [Fact]
    public void Parse_WhenExternalDocumentOmitsColorScheme_DefaultsToDark() =>
        ThemeCatalog.Parse(ThemeJson.Create().Replace("\"colorScheme\": \"dark\",", string.Empty, StringComparison.Ordinal))
            .ColorScheme.ShouldBe(ColorScheme.Dark);

    /// <summary>Verifies blank identity metadata is treated as missing on the external path rather
    /// than reaching <c>Theme</c>'s constructor.
    ///
    /// <para>Substituting for null alone let <c>"slug": ""</c> through, which threw an undeclared
    /// <c>ArgumentException</c> naming a <c>paramName</c> that means nothing to a caller who passed
    /// one json string - and which a caller catching the documented <c>InvalidDataException</c> did
    /// not catch either.</para>
    /// </summary>
    [Fact]
    public void Parse_WhenExternalDocumentHasBlankSlug_SubstitutesTheDefault() =>
        ThemeCatalog.Parse(ThemeJson.Create().Replace("\"slug\": \"t\",", "\"slug\": \"\",", StringComparison.Ordinal))
            .Slug.ShouldBe("custom");

    /// <summary>Verifies the missing-<c>styles</c> error names the requirement that exists. It used
    /// to say every semantic style section must be defined exactly once, sending an author off to
    /// enumerate six keys when an empty object would have sufficed.</summary>
    [Fact]
    public void Parse_WhenStylesIsAbsent_NamesTheRealRequirement()
    {
        var json = ThemeJson.Create();
        var withoutStyles = json[..json.IndexOf("\"styles\"", StringComparison.Ordinal)].TrimEnd().TrimEnd(',') + " }";

        Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(withoutStyles))
            .Message.ShouldContain("'styles'");
    }

    /// <summary>The counter-case that error was wrong about: an empty <c>styles</c> object is
    /// perfectly valid, exactly as themes.md states.</summary>
    [Fact]
    public void Parse_WhenStylesIsEmpty_LoadsSuccessfully()
    {
        var json = ThemeJson.Create();
        var start = json.IndexOf("\"styles\"", StringComparison.Ordinal);
        var emptied = json[..start] + "\"styles\": { } }";

        ThemeCatalog.Parse(emptied).Slug.ShouldBe("t");
    }

    /// <summary>Verifies a catalog entry now carries the declared order it was sorted by. It was
    /// read into a local sort map and dropped, so a consumer could observe the ordering but never
    /// the value producing it.</summary>
    [Fact]
    public void Entries_WhenRead_ExposeTheirDeclaredOrder()
    {
        var entries = ThemeCatalog.Entries;

        entries.ShouldNotBeEmpty();
        entries.Select(entry => entry.Order).Distinct().Count()
            .ShouldBeGreaterThan(1, "the bundled catalog declares more than one order");
    }

    /// <summary>Verifies a MessageBox style change that touches only the forwarded message face
    /// schedules the pass that re-applies it. Both application sites run from MeasureOverride, so a
    /// member Compare omits is never re-applied at all.</summary>
    [Fact]
    public void Compare_WhenOnlyTheMessageFaceChanges_RequestsMeasure()
    {
        var previous = MessageBoxStyle.Default;
        var current = previous with
        {
            MessageFace = previous.MessageFace with { Foreground = SemanticColor.Error }
        };

        Impact(previous, current).ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>The counter-case: an unchanged style still reports no work.</summary>
    [Fact]
    public void Compare_WhenTheMessageBoxStyleIsUnchanged_RequestsNothing() =>
        Impact(MessageBoxStyle.Default, MessageBoxStyle.Default).ShouldBe(InvalidationImpact.None);

    /// <summary>Verifies the two file-dialog styles do the same for the border they forward to their
    /// retained file list. Both carry the identical member, so fixing one would have left a matched
    /// pair disagreeing.</summary>
    [Fact]
    public void Compare_WhenOnlyTheFileListBorderChanges_RequestsMeasure()
    {
        var picker = FilePickerDialogStyle.Default;
        var save = SaveFileDialogStyle.Default;
        var moved = new Border(
            BorderSide.All,
            BorderGlyphStyle.Heavy,
            SemanticColor.Accent,
            Color.Transparent,
            TerminalAttributes.None);

        FilePickerDialogStyle.Definition.Compare(picker, null, picker with { FileListBorder = moved }, null)
            .ShouldBe(InvalidationImpact.Measure);
        SaveFileDialogStyle.Definition.Compare(save, null, save with { FileListBorder = moved }, null)
            .ShouldBe(InvalidationImpact.Measure);
    }

    private static InvalidationImpact Impact(MessageBoxStyle previous, MessageBoxStyle current) =>
        MessageBoxStyle.Definition.Compare(previous, null, current, null);

    private static async Task<ComponentSurface> MountBorderedAsync(BorderSide sides, Size size)
    {
        var probe = new ChromeProbe
        {
            Border = new Border(
                sides,
                BorderGlyphStyle.Heavy,
                SemanticColor.ControlBorder,
                Color.Transparent,
                TerminalAttributes.None),
            Width = Length.Cells(size.Width),
            Height = Length.Cells(size.Height)
        };

        return await ComponentSurface.MountAsync(probe, size, TestContext.Current.CancellationToken);
    }

    private static string Cells(ComponentSurface surface, int width, int height)
    {
        var builder = new StringBuilder(width * height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                _ = builder.Append(surface.Cell(new Point(x, y)).Text);
            }
        }

        return builder.ToString();
    }
}
