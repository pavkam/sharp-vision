// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies the drop-down chevron has one application-wide answer.
///
/// <para><c>ComboBox</c>, <c>DateInput</c>, and <c>DateTimeInput</c> each declared their own
/// <c>DropDownGlyph</c> property and <c>ResetDropDownGlyph()</c> method - three near-identical
/// blocks, each defaulting to the same hardcoded <c>ControlGlyphs.Disclosure.DropDown</c> entry,
/// none of them reachable from a theme. So the one visual detail that most obviously wants a single
/// answer - a terminal without dependable arrow coverage needs the chevron replaced everywhere at
/// once - was the one that had to be set on every instance individually.</para>
///
/// <para>It now lives on <c>InputStyle</c>, the well-known style all three already resolve, so a
/// theme's <c>styles.input</c> section answers for all of them together. These tests cover all
/// three controls rather than one, because the defect was precisely that the three could drift.</para>
/// </summary>
public sealed class DropDownGlyphThemingTests
{
    /// <summary>Verifies the shared style resolves an authored chevron.</summary>
    [Fact]
    public void Parse_WhenInputSectionAuthorsTheChevron_ResolvesIt()
    {
        var json = ThemeJson.Create(inputExtra: """, "dropDownGlyph": "v" """);

        var style = ThemeCatalog.Parse(json).GetStyleSet(InputStyle.Default).Normal;

        style.DropDownGlyph.ShouldBe(new Rune('v'));
    }

    /// <summary>The counter-case: an unauthored theme keeps the code-owned chevron.</summary>
    [Fact]
    public void Parse_WhenInputSectionOmitsTheChevron_KeepsTheCodeOwnedGlyph()
    {
        var style = ThemeCatalog.Parse(ThemeJson.Create()).GetStyleSet(InputStyle.Default).Normal;

        style.DropDownGlyph.ShouldBe(new Rune('▼'));
    }

    /// <summary>Verifies a themed chevron reaches ComboBox's rendered cells.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsTheChevron_ComboBoxDrawsItAsync()
    {
        var combo = new ComboBox
        {
            Items = ["Alpha"],
            SelectedIndex = 0,
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            combo,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => surface.Application.Theme = AuthoredTheme(), "author chevron");

        surface.ShouldRender("""
                             ┏━━━━━━━━┓
                             ┃Alpha  v┃
                             ┗━━━━━━━━┛
                             """);
    }

    /// <summary>Verifies the same theme reaches DateInput, which had its own copy of the property.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsTheChevron_DateInputDrawsItAsync()
    {
        var input = new DateInput { Width = Length.Cells(16), Height = Length.Cells(3) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(16, 3),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => surface.Application.Theme = AuthoredTheme(), "author chevron");

        Row(surface, 1, 16).ShouldContain("v");
        Row(surface, 1, 16).ShouldNotContain("▼");
    }

    /// <summary>Verifies the same theme reaches DateTimeInput, the third copy.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsTheChevron_DateTimeInputDrawsItAsync()
    {
        var input = new DateTimeInput { Width = Length.Cells(24), Height = Length.Cells(3) };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(24, 3),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => surface.Application.Theme = AuthoredTheme(), "author chevron");

        Row(surface, 1, 24).ShouldContain("v");
        Row(surface, 1, 24).ShouldNotContain("▼");
    }

    /// <summary>Verifies each input-derived leaf style forwards the fallback's chevron instead of
    /// discarding it.
    ///
    /// <para>All four hardcoded the code-owned value in their base constructor call, so their
    /// <c>Complete(input, state)</c> dropped whatever the fallback resolved. The member is
    /// inherited into each leaf's own theme key, which meant
    /// <c>styles.button.normal.dropDownGlyph</c> and its three siblings were accepted by the parser
    /// and did nothing - a blessed-but-unread section, which is the failure the section-granularity
    /// key rules exist to prevent. Inheritance is how a member leaks into a key that ignores it.</para>
    /// </summary>
    [Fact]
    public void LeafInputStyles_WhenTheInputSectionAuthorsTheChevron_ForwardIt()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(inputExtra: """, "dropDownGlyph": "v" """));
        var expected = new Rune('v');

        ButtonStyle.Definition.Resolve(null, theme).DropDownGlyph.ShouldBe(expected);
        CheckBoxStyle.Definition.Resolve(null, theme).DropDownGlyph.ShouldBe(expected);
        RadioButtonStyle.Definition.Resolve(null, theme).DropDownGlyph.ShouldBe(expected);
        CalendarStyle.Definition.Resolve(null, theme).DropDownGlyph.ShouldBe(expected);
    }

    // ComponentSurface exposes cells, not rows; the chevron's exact column depends on the field
    // layout, and this test is about which glyph is drawn rather than where.
    private static string Row(ComponentSurface surface, int y, int width)
    {
        var builder = new StringBuilder(width);

        for (var x = 0; x < width; x++)
        {
            _ = builder.Append(surface.Cell(new Point(x, y)).Text);
        }

        return builder.ToString();
    }

    private static Theme AuthoredTheme() =>
        ThemeCatalog.Parse(ThemeJson.Create(inputExtra: """, "dropDownGlyph": "v" """));
}
