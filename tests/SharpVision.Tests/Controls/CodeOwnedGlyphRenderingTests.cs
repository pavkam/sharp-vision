// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies representative controls render code-owned glyph defaults.</summary>
public sealed class CodeOwnedGlyphRenderingTests
{
    /// <summary>Verifies theme changes do not change control geometry or drawing characters.</summary>
    [Fact]
    public void Render_WhenThemeChanges_KeepsCodeOwnedGlyphs()
    {
        Render(new ProgressBar { Value = 0.5 }, new Size(4, 1), ThemeCatalog.White).ShouldBe("██░░");
        Render(new ProgressBar { Value = 0.5 }, new Size(4, 1), ThemeCatalog.Dark).ShouldBe("██░░");
        Render(
            new ComboBox { Items = ["A"], Width = Length.Cells(4) },
            new Size(4, 1), ThemeCatalog.Parse(ThemeJson.Create(inputSides: "\"none\""))).ShouldBe("A  ▼");
        Render(
            new Expander { HeaderText = "X", IsExpanded = false },
            new Size(4, 1), ThemeCatalog.White).ShouldBe("▶ X ");
        Render(
            new RadioButton { IsChecked = true, Style = RadioButtonStyle.Glyph },
            new Size(1, 1),
            ThemeCatalog.White).ShouldBe("◉");
        Render(new Separator { Width = Length.Cells(4) }, new Size(4, 1), ThemeCatalog.White).ShouldBe("────");
    }

    /// <summary>Verifies explicit local values win until reset restores code-owned defaults.</summary>
    [Fact]
    public void Render_WhenLocalProgressGlyphsAreSet_OverridesUntilReset()
    {
        var baseline = ProgressBarStyle.Default;
        var progress = new ProgressBar
        {
            Style = new ProgressBarStyle(
                baseline.Face,
                baseline.Border,
                baseline.Shadow,
                baseline.FillColor,
                baseline.TrackColor,
                baseline.IndeterminateColor,
                new ProgressBarGlyphs(new Rune('!'), new Rune('_'), baseline.Glyphs.Indeterminate)),
            Value = 0.5
        };

        Render(progress, new Size(4, 1), ThemeCatalog.White).ShouldBe("!!__");
        progress.Style = null;
        Render(progress, new Size(4, 1), ThemeCatalog.White).ShouldBe("██░░");
    }

    private static string Render(ControlBase control, Size size, Theme theme)
    {
        control.SetTheme(theme);
        new LayoutEngine().Layout(control, size);
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
