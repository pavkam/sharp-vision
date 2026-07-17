// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies representative controls render the active semantic glyph palette.</summary>
public sealed class ThemeGlyphRenderingTests
{
    /// <summary>Verifies the contrasting light theme supplies ASCII progress, disclosure, and selection cells.</summary>
    [Fact]
    public void Render_WhenWhiteThemeIsApplied_UsesAsciiGlyphPalette()
    {
        // Arrange, act, and assert
        Render(new ProgressBar { Value = 0.5 }, new Size(4, 1), Themes.White).ShouldBe("##..");
        Render(
            new ComboBox { Items = ["A"], Width = Length.Cells(4) },
            new Size(4, 1),
            Themes.White).ShouldBe("A  v");
        Render(
            new Expander { Header = "X", IsExpanded = false },
            new Size(4, 1),
            Themes.White).ShouldBe("> X ");
        Render(
            new RadioButton { IsChecked = true },
            new Size(1, 1),
            Themes.White).ShouldBe("x");
        Render(
            new Separator { Width = Length.Cells(4) },
            new Size(4, 1),
            Themes.White).ShouldBe("----");
    }

    /// <summary>Verifies explicit local values win over the active theme until reset.</summary>
    [Fact]
    public void Render_WhenLocalProgressGlyphsAreSet_OverridesThemeUntilReset()
    {
        // Arrange
        var progress = new ProgressBar
        {
            FillGlyph = new Rune('!'),
            TrackGlyph = new Rune('_'),
            Value = 0.5,
        };

        // Act and assert
        Render(progress, new Size(4, 1), Themes.White).ShouldBe("!!__");
        progress.ResetGlyphs();
        Render(progress, new Size(4, 1), Themes.White).ShouldBe("##..");
    }

    private static string Render(Control control, Size size, Theme theme)
    {
        control.SetTheme(theme);
        new Engine().Layout(control, size);
        using Frame frame = new(size);
        control.Render(frame.Canvas);
        var value = new StringBuilder(size.Width * size.Height);

        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var grapheme = FrameOracle.Get(frame, new Point(x, y));
                _ = value.Append(grapheme.Length == 0 ? " " : grapheme);
            }
        }

        return value.ToString();
    }
}
