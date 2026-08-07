// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

using SharpVision.Tests.Styling;

/// <summary>Verifies a check mark reads the same in a tree row as in a standalone CheckBox, which
/// is what <c>CheckMark</c>'s own documentation promises.
///
/// <para>The two resolved from different places. <c>CheckBox</c> is a
/// <c>Pressable&lt;CheckBoxStyle&gt;</c>, so its glyphs come from the theme's <c>checkBox</c>
/// section overlaid on the code-owned default; tree rows fell back to the static
/// <c>CheckMark.Brackets</c>, built once from a <c>static</c> field completed from
/// <c>InputStyle.Default</c> with no <c>Theme</c> anywhere in the chain. So a theme authoring
/// <c>styles.checkBox.normal.glyphs</c> - the documented, advertised way to restyle check marks -
/// moved every CheckBox and left every tree row behind, silently, on the same screen.</para>
///
/// <para>Latent for the fifteen bundled themes, which never author <c>checkBox</c>. Live for
/// exactly the user-authored themes that key exists to serve.</para>
/// </summary>
public sealed class TreeViewCheckMarkThemingTests
{
    /// <summary>The regression this file exists to pin: both sides agree under a theme that
    /// restyles the mark family.</summary>
    [Fact]
    public async Task ActualCheckMark_WhenThemeAuthorsCheckBoxGlyphs_MatchesTheCheckBoxAsync()
    {
        var checkBox = new CheckBox();
        var item = new TreeViewItem { Header = "Row", Checkable = true };
        var tree = new TreeView { Items = { item } };
        var root = new Stack { Children = { checkBox, tree } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => surface.Application.Theme = TickTheme(), "author tick glyphs");

        var expected = checkBox.ActualStyle.Glyphs;
        item.ActualCheckMark.Glyphs.ShouldBe(
            expected,
            "a tree row and a CheckBox must render the same themed mark family");
        tree.ActualCheckMark.Glyphs.ShouldBe(expected);
    }

    /// <summary>Verifies the mark style travels too, not only the glyph trio - a themed one-cell
    /// family and a three-cell bracket family occupy different widths.</summary>
    [Fact]
    public async Task ActualCheckMark_WhenThemeAuthorsMarkStyle_MatchesTheCheckBoxAsync()
    {
        var checkBox = new CheckBox();
        var item = new TreeViewItem { Header = "Row", Checkable = true };
        var tree = new TreeView { Items = { item } };
        var root = new Stack { Children = { checkBox, tree } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => surface.Application.Theme = TickTheme(), "author tick mark style");

        item.ActualCheckMark.MarkStyle.ShouldBe(checkBox.ActualStyle.MarkStyle);
    }

    /// <summary>Verifies replacing the theme re-resolves the fallback rather than latching the
    /// family observed at attachment.</summary>
    [Fact]
    public async Task ActualCheckMark_WhenThemeIsReplaced_FollowsTheNewFamilyAsync()
    {
        var item = new TreeViewItem { Header = "Row", Checkable = true };
        var tree = new TreeView { Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        var before = item.ActualCheckMark.Glyphs;

        await surface.UpdateAsync(() => surface.Application.Theme = TickTheme(), "swap to tick glyphs");

        item.ActualCheckMark.Glyphs.ShouldNotBe(before);
        item.ActualCheckMark.Glyphs.Checked.ShouldBe(new Rune('X'));
    }

    /// <summary>The counter-case that keeps the change honest: an explicit per-item override still
    /// wins over the theme, so this did not turn a local override into a suggestion.</summary>
    [Fact]
    public async Task ActualCheckMark_WhenItemOverridesTheMark_KeepsTheOverrideUnderAThemeAsync()
    {
        var item = new TreeViewItem
        {
            Header = "Row",
            Checkable = true,
            CheckMark = CheckMark.Brackets
        };
        var tree = new TreeView { Items = { item } };
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => surface.Application.Theme = TickTheme(), "author tick glyphs");

        item.ActualCheckMark.ShouldBe(CheckMark.Brackets);
    }

    /// <summary>Verifies the unthemed default is unchanged, so the fifteen bundled themes - none of
    /// which author <c>checkBox</c> - render exactly as before.</summary>
    [Fact]
    public void ActualCheckMark_WhenNoThemeAuthorsCheckBox_KeepsTheCodeOwnedFamily()
    {
        using var tree = new TreeView();

        tree.ActualCheckMark.Glyphs.ShouldBe(CheckMark.Brackets.Glyphs);
        tree.ActualCheckMark.MarkStyle.ShouldBe(CheckMark.Brackets.MarkStyle);
    }

    // Differs from the code-owned brackets family in both the mark style and the glyph trio.
    // Authoring markStyle alone would leave Glyphs on the code-owned brackets, which would make the
    // glyph assertions above pass vacuously.
    private static Theme TickTheme() =>
        ThemeCatalog.Parse(
            ThemeJson.Create(
                extraStyles:
                """, "checkBox": { "normal": { "markStyle": "tick", "glyphs": { "unchecked": ".", "checked": "X", "indeterminate": "-" } } } """));
}
