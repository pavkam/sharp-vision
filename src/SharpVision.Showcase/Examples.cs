using System.Text;

using SharpVision.Controls;
using SharpVision.Fonts;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;
using SharpVision.Text;

using ControlCanvas = SharpVision.Controls.Canvas;
using ControlDock = SharpVision.Controls.Dock;
using ControlGrid = SharpVision.Controls.Grid;
using ControlList = SharpVision.Controls.List;
using ControlOverlay = SharpVision.Controls.Overlay;
using ControlRun = SharpVision.Controls.Run;
using ControlStack = SharpVision.Controls.Stack;
using ControlText = SharpVision.Controls.Text;

namespace SharpVision.Showcase;

/// <summary>Builds fresh interactive and display examples exclusively from public control APIs.</summary>
internal static class Examples
{
    #region Display controls

    /// <summary>Creates every built-in border glyph family around compact content.</summary>
    internal static Control Border()
    {
        var examples = Vertical();
        AddBorder(examples, "Light", Glyphs.Light);
        AddBorder(examples, "Heavy", Glyphs.Heavy);
        AddBorder(examples, "Paired", Glyphs.Paired);
        AddBorder(examples, "Rounded", Glyphs.Rounded);
        AddBorder(examples, "ASCII fallback", Glyphs.Ascii);
        AddBorder(examples, "Solid block", Glyphs.Solid);
        AddBorder(examples, "Light shade", Glyphs.LightShade);
        AddBorder(examples, "Medium shade", Glyphs.MediumShade);
        AddBorder(examples, "Dark shade", Glyphs.DarkShade);
        return examples;
    }

    /// <summary>Creates an editable FIGlet preview with a scrollable dropdown of audited catalog fonts.</summary>
    internal static Control FigletText()
    {
        var catalog = FigletCatalog.Default;
        var text = new TextInput
        {
            Width = Length.Cells(30),
            Text = "SharpVision",
            Style = Palette.Editor(),
        };
        var fontLabel = new ControlText("Font: Standard ▼");
        var fontButton = new Button { Content = fontLabel };
        var fontNames = catalog.Names.ToArray();
        var fontList = new ControlList
        {
            Width = Length.Cells(30),
            Height = Length.Cells(8),
            Items = fontNames,
            SelectedIndex = Array.IndexOf(fontNames, "Standard"),
            Visibility = Visibility.Collapsed,
        };
        var preview = new FigletText(catalog.Load("Standard"))
        {
            Content = text.Text,
            Foreground = Palette.Accent,
        };
        var status = new ControlText("Type text, then choose a font from the dropdown.")
        {
            Foreground = Palette.Muted,
        };
        text.TextChanged += (_, eventArgs) => preview.Content = eventArgs.Text;
        fontButton.Click += (_, _) =>
            fontList.Visibility = fontList.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        fontList.ItemInvoked += (_, eventArgs) =>
        {
            if (eventArgs.Item is not string name)
            {
                return;
            }

            // Load only the selected audited font; the archive is never expanded wholesale.
            preview.Font = catalog.Load(name);
            fontLabel.Content = $"Font: {name} ▼";
            status.Content = $"Previewing {name}. Choose another font to compare it.";
            fontList.Visibility = Visibility.Collapsed;
        };
        var examples = Vertical();
        examples.Children.Add(text);
        examples.Children.Add(fontButton);
        examples.Children.Add(fontList);
        examples.Children.Add(status);
        examples.Children.Add(preview);
        return examples;
    }

    /// <summary>Creates styled, linked, wrapped Unicode inline content.</summary>
    internal static Control RichText()
    {
        var richText = new RichText
        {
            Wrapping = Wrapping.Word,
            TextAlignment = Alignment.Start,
        };
        richText.Inlines.Add(new ControlRun("Rich ")
        {
            Attributes = Attributes.Bold,
            Foreground = Palette.Success,
        });
        richText.Inlines.Add(new ControlRun("terminal text") { Attributes = Attributes.Italic });
        richText.Inlines.Add(new LineBreak());
        richText.Inlines.Add(new ControlRun("Unicode: café · 你好 · 👩‍💻 · "));
        richText.Inlines.Add(new Hyperlink("project source", "https://github.com/pavkam"));
        return Card(richText, Glyphs.Rounded);
    }

    /// <summary>Creates composite and block-glyph Turbo Vision shadows.</summary>
    internal static Control Shadow()
    {
        var examples = Vertical();
        examples.Children.Add(new ControlText("Composite stage")
        {
            Foreground = Palette.Success,
            Attributes = Attributes.Bold,
        });
        examples.Children.Add(ShadowStage(new Shadow
        {
            Child = DemoCard("Composite", Glyphs.Rounded),
            Foreground = Palette.Muted,
            Background = Palette.Canvas,
            Offset = new Point(2, 1),
        }));
        examples.Children.Add(new ControlText("Block glyph stage")
        {
            Foreground = Palette.Accent,
            Attributes = Attributes.Bold,
        });
        examples.Children.Add(ShadowStage(new Shadow
        {
            Child = DemoCard("Block glyph", Glyphs.Paired),
            Mode = ShadowMode.BlockGlyph,
            Glyph = new Rune('░'),
            Foreground = Palette.Muted,
            Background = Palette.Canvas,
            Offset = new Point(2, 1),
        }));
        return examples;
    }

    /// <summary>Creates wrapped, trimmed, aligned, styled, and wide-grapheme text variants.</summary>
    internal static Control Text()
    {
        var examples = Vertical();
        examples.Children.Add(new ControlText("Plain Unicode: café · 你好 · 👩‍💻"));
        examples.Children.Add(new ControlText("Word wrapping keeps complete grapheme clusters together.")
        {
            Width = Length.Cells(24),
            Wrapping = Wrapping.Word,
        });
        examples.Children.Add(new ControlText("Centered and trimmed terminal text")
        {
            Width = Length.Cells(20),
            TextAlignment = Alignment.Center,
            Trimming = Trimming.GraphemeEllipsis,
            Foreground = Palette.Warning,
            Attributes = Attributes.Bold,
        });
        return examples;
    }

    #endregion

    #region Interactive controls

    /// <summary>Creates enabled, disabled, default, cancel, and live-click button variants.</summary>
    internal static Control Button()
    {
        var examples = Vertical();
        var status = new ControlText("Activation log: waiting");
        var active = new Button { Content = new ControlText("Click or press Enter") };
        active.Click += (_, eventArgs) =>
            status.Content = $"Activation log: {eventArgs.Cause}";
        examples.Children.Add(active);
        examples.Children.Add(new Button
        {
            Content = new ControlText("Disabled"),
            IsEnabled = false,
        });
        examples.Children.Add(new Button
        {
            Content = new ControlText("Default action"),
            IsDefault = true,
        });
        examples.Children.Add(new Button
        {
            Content = new ControlText("Cancel action"),
            IsCancel = true,
        });
        examples.Children.Add(status);
        return examples;
    }

    /// <summary>Creates two-state, three-state, custom-mark, and disabled check boxes.</summary>
    internal static Control CheckBox()
    {
        var examples = Vertical();
        examples.Children.Add(new CheckBox { Content = new ControlText("Unchecked") });
        examples.Children.Add(new CheckBox
        {
            Content = new ControlText("Checked"),
            IsChecked = true,
        });
        examples.Children.Add(new CheckBox
        {
            Content = new ControlText("Indeterminate"),
            IsThreeState = true,
            IsChecked = null,
        });
        examples.Children.Add(new CheckBox
        {
            Content = new ControlText("Disabled checked"),
            IsChecked = true,
            IsEnabled = false,
        });
        return examples;
    }

    /// <summary>Creates a scrollable selectable item list with one disabled comparison.</summary>
    internal static Control List()
    {
        var examples = Horizontal();
        examples.Children.Add(new ControlList
        {
            Width = Length.Cells(18),
            Height = Length.Cells(6),
            Items = new object?[]
            {
                "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta",
            },
            SelectedIndex = 1,
        });
        examples.Children.Add(new ControlList
        {
            Width = Length.Cells(18),
            Height = Length.Cells(4),
            IsEnabled = false,
            Items = new object?[] { "Disabled", "Still visible", "Not selectable" },
        });
        return examples;
    }

    /// <summary>Creates one mutually exclusive named radio group with a disabled option.</summary>
    internal static Control RadioButton()
    {
        var examples = Vertical();
        examples.Children.Add(new RadioButton
        {
            Content = new ControlText("Fast"),
            GroupName = "quality",
            IsChecked = true,
        });
        examples.Children.Add(new RadioButton
        {
            Content = new ControlText("Balanced"),
            GroupName = "quality",
        });
        examples.Children.Add(new RadioButton
        {
            Content = new ControlText("Unavailable"),
            GroupName = "quality",
            IsEnabled = false,
        });
        return examples;
    }

    /// <summary>Creates horizontal and vertical ranges with visible proportional thumbs.</summary>
    internal static Control ScrollBar()
    {
        var examples = Vertical();
        var horizontal = new ScrollBar
        {
            Width = Length.Cells(28),
            Orientation = Orientation.Horizontal,
            Maximum = 100,
            ViewportSize = 25,
            Value = 35,
            DecrementGlyph = new Rune('◀'),
            IncrementGlyph = new Rune('▶'),
            TrackGlyph = new Rune('─'),
            ThumbGlyph = new Rune('█'),
        };
        var status = new ControlText($"Thumb value: {horizontal.Value}")
        {
            Foreground = Palette.Muted,
        };
        horizontal.ValueChanged += (_, eventArgs) =>
            status.Content = $"Thumb value: {eventArgs.Value}";
        examples.Children.Add(new ControlText("Drag the solid thumb, or use the track and arrow buttons.")
        {
            Foreground = Palette.Accent,
        });
        examples.Children.Add(horizontal);
        examples.Children.Add(status);
        examples.Children.Add(new ScrollBar
        {
            Height = Length.Cells(8),
            Maximum = 40,
            ViewportSize = 10,
            Value = 12,
            DecrementGlyph = new Rune('▲'),
            IncrementGlyph = new Rune('▼'),
            TrackGlyph = new Rune('│'),
            ThumbGlyph = new Rune('█'),
        });
        return examples;
    }

    /// <summary>Creates editable, read-only, password, limited, and multiline text inputs.</summary>
    internal static Control TextInput()
    {
        var examples = Vertical();
        examples.Children.Add(new TextInput
        {
            Width = Length.Cells(28),
            Text = "Edit me: café 👩‍💻",
            Style = Palette.Editor(),
        });
        examples.Children.Add(new TextInput
        {
            Width = Length.Cells(28),
            Text = "Read-only value",
            IsReadOnly = true,
            Style = Palette.Editor(),
        });
        examples.Children.Add(new TextInput
        {
            Width = Length.Cells(28),
            Text = "secret",
            PasswordCharacter = new Rune('•'),
            Style = Palette.Editor(),
        });
        examples.Children.Add(new TextInput
        {
            Width = Length.Cells(28),
            Text = "12 chars max",
            MaxLength = 12,
            Style = Palette.Editor(),
        });
        examples.Children.Add(new TextInput
        {
            Width = Length.Cells(28),
            Height = Length.Cells(3),
            AcceptsReturn = true,
            AcceptsTab = true,
            Text = "Multiline\ninput",
            Style = Palette.Editor(),
        });
        return examples;
    }

    #endregion

    #region Layout controls

    /// <summary>Creates fixed, percentage, constrained, layered, clipped, and primitive Canvas specimens.</summary>
    internal static Control Canvas()
    {
        var examples = Vertical();
        var fixedPlacement = CanvasStage();
        var fixedLabel = DemoCard("fixed 2,1", Glyphs.Light);
        ControlCanvas.SetLeft(fixedLabel, Length.Cells(2));
        ControlCanvas.SetTop(fixedLabel, Length.Cells(1));
        fixedPlacement.Children.Add(fixedLabel);
        examples.Children.Add(CanvasSection(
            "Fixed placement",
            "Cell offsets place this bordered child two cells from the left and one from the top.",
            fixedPlacement));

        var percentagePlacement = CanvasStage();
        var percentLabel = DemoCard("50%,50%", Glyphs.Heavy);
        ControlCanvas.SetLeft(percentLabel, Length.Percent(50));
        ControlCanvas.SetTop(percentLabel, Length.Percent(50));
        percentagePlacement.Children.Add(percentLabel);
        examples.Children.Add(CanvasSection(
            "Percentage placement",
            "Percentage offsets resolve against the final Canvas box, so this specimen moves when the available width changes.",
            percentagePlacement));

        var constrained = CanvasStage();
        var edgeLabel = DemoCard("Right 2 / Bottom 1", Glyphs.Paired);
        ControlCanvas.SetRight(edgeLabel, Length.Cells(2));
        ControlCanvas.SetBottom(edgeLabel, Length.Cells(1));
        constrained.Children.Add(edgeLabel);
        var sizedLabel = DemoCard("40% wide", Glyphs.Rounded);
        sizedLabel.Width = Length.Percent(40);
        ControlCanvas.SetLeft(sizedLabel, Length.Cells(1));
        ControlCanvas.SetTop(sizedLabel, Length.Cells(1));
        constrained.Children.Add(sizedLabel);
        examples.Children.Add(CanvasSection(
            "Edge constraints",
            "Right and bottom offsets anchor one child, while a second child requests a percentage width from the same canvas.",
            constrained));

        var layered = CanvasStage();
        var back = DemoCard("Back", Glyphs.Light);
        ControlCanvas.SetLeft(back, Length.Cells(2));
        ControlCanvas.SetTop(back, Length.Cells(1));
        layered.Children.Add(back);
        var front = DemoCard("Front", Glyphs.Heavy);
        ControlCanvas.SetLeft(front, Length.Cells(6));
        ControlCanvas.SetTop(front, Length.Cells(2));
        layered.Children.Add(front);
        var clipped = DemoCard("clipped", Glyphs.Ascii);
        ControlCanvas.SetLeft(clipped, Length.Cells(29));
        ControlCanvas.SetTop(clipped, Length.Cells(5));
        layered.Children.Add(clipped);
        examples.Children.Add(CanvasSection(
            "Layering and clipping",
            "Later children paint above earlier ones; the final child deliberately crosses the edge and is clipped by the canvas.",
            layered));

        examples.Children.Add(CanvasSection(
            "Drawing primitives",
            "Canvas drawing APIs add box, line, shade, and quadrant glyphs without creating a control per terminal cell.",
            new CanvasSample()));
        return examples;
    }

    /// <summary>Creates all four physical dock edges and a remaining-space fill child.</summary>
    internal static Control Dock()
    {
        var dock = new ControlDock
        {
            Width = Length.Cells(38),
            Height = Length.Cells(9),
            LastChildFills = true,
            Spacing = 1,
        };
        var left = Card(new ControlText("Left"), Glyphs.Light);
        left.Width = Length.Cells(7);
        ControlDock.SetSide(left, Side.Left);
        dock.Children.Add(left);
        var top = Card(new ControlText("Top"), Glyphs.Heavy);
        top.Height = Length.Cells(2);
        ControlDock.SetSide(top, Side.Top);
        dock.Children.Add(top);
        var right = Card(new ControlText("Right"), Glyphs.Paired);
        right.Width = Length.Cells(8);
        ControlDock.SetSide(right, Side.Right);
        dock.Children.Add(right);
        var bottom = Card(new ControlText("Bottom"), Glyphs.Ascii);
        bottom.Height = Length.Cells(2);
        ControlDock.SetSide(bottom, Side.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(Card(new ControlText("Fill"), Glyphs.Rounded));
        return dock;
    }

    /// <summary>Creates fixed, automatic, percentage, proportional, and spanning grid content.</summary>
    internal static Control Grid()
    {
        var grid = new ControlGrid
        {
            Width = Length.Cells(40),
            Height = Length.Cells(9),
            RowSpacing = 1,
            ColumnSpacing = 1,
        };
        grid.Rows.Add(Track.Cells(2));
        grid.Rows.Add(Track.Auto());
        grid.Rows.Add(Track.Star(1));
        grid.Columns.Add(Track.Cells(8));
        grid.Columns.Add(Track.Percent(35));
        grid.Columns.Add(Track.Star(1));
        AddGrid(grid, "Fixed", 0, 0);
        AddGrid(grid, "35%", 0, 1);
        AddGrid(grid, "Star", 0, 2);
        var spanning = Card(new ControlText("ColumnSpan = 2"), Glyphs.Rounded);
        ControlGrid.SetRow(spanning, 1);
        ControlGrid.SetColumn(spanning, 0);
        ControlGrid.SetColumnSpan(spanning, 2);
        grid.Children.Add(spanning);
        AddGrid(grid, "Auto / Star", 2, 2);
        return grid;
    }

    /// <summary>Creates layered labels with stable positive and negative z-order.</summary>
    internal static Control Overlay()
    {
        var overlay = new ControlOverlay
        {
            Width = Length.Cells(32),
            Height = Length.Cells(7),
            ClipToBounds = true,
        };
        var back = new ControlText("Background layer")
        {
            Background = Palette.Highlight,
            Padding = new Thickness(1),
        };
        ControlOverlay.SetZIndex(back, -1);
        overlay.Children.Add(back);
        var middle = Card(new ControlText("Middle layer"), Glyphs.Heavy);
        middle.Margin = new Thickness(4, 2, 4, 2);
        overlay.Children.Add(middle);
        var front = new ControlText("Front layer")
        {
            Foreground = Palette.Warning,
            Attributes = Attributes.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        ControlOverlay.SetZIndex(front, 10);
        overlay.Children.Add(front);
        return overlay;
    }

    /// <summary>Creates content large enough to require both automatic scrollbars.</summary>
    internal static Control ScrollView()
    {
        var content = Vertical();

        for (var index = 1; index <= 14; index++)
        {
            content.Children.Add(new ControlText(
                $"Scrollable row {index:00} · wide content beyond the viewport"));
        }

        return new ScrollView
        {
            Width = Length.Cells(34),
            Height = Length.Cells(8),
            Content = content,
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    /// <summary>Creates horizontal, vertical, reversed, fixed, percentage, and proportional stack variants.</summary>
    internal static Control Stack()
    {
        var examples = Vertical();
        var horizontal = Horizontal();
        var fixedCard = Card(new ControlText("Fixed 10"), Glyphs.Light);
        fixedCard.Width = Length.Cells(10);
        horizontal.Children.Add(fixedCard);
        var percentCard = Card(new ControlText("35%"), Glyphs.Heavy);
        percentCard.Width = Length.Percent(35);
        horizontal.Children.Add(percentCard);
        var starCard = Card(new ControlText("1*"), Glyphs.Paired);
        starCard.Width = Length.Star(1);
        horizontal.Children.Add(starCard);
        horizontal.Width = Length.Cells(40);
        examples.Children.Add(horizontal);
        var reversed = Horizontal();
        reversed.Reverse = true;
        reversed.Children.Add(new ControlText("First"));
        reversed.Children.Add(new ControlText("Second"));
        reversed.Children.Add(new ControlText("Third"));
        examples.Children.Add(reversed);
        return examples;
    }

    #endregion

    #region Shared composition

    private static ControlStack Vertical() => new() { Spacing = 1 };

    private static ControlStack Horizontal() => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 2,
    };

    private static Border Card(Control child, Glyphs glyphs) => new()
    {
        Child = child,
        BorderThickness = new Thickness(1),
        Glyphs = glyphs,
        Padding = new Thickness(1, 0),
    };

    private static Border DemoCard(string content, Glyphs glyphs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new Border
        {
            Child = new ControlText(content),
            BorderThickness = new Thickness(1),
            Glyphs = glyphs,
            BorderColor = Palette.Accent,
            Background = Palette.Surface,
            Padding = new Thickness(1, 0),
        };
    }

    private static ControlCanvas CanvasStage() => new()
    {
        Width = Length.Cells(36),
        Height = Length.Cells(7),
        ClipToBounds = true,
    };

    private static ControlStack CanvasSection(string heading, string description, Control sample)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(sample);
        var section = Vertical();
        var text = new RichText { Wrapping = Wrapping.Word };
        text.Inlines.Add(new ControlRun(heading)
        {
            Foreground = Palette.Warning,
            Attributes = Attributes.Bold,
        });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new ControlRun(description) { Foreground = Palette.Muted });
        section.Children.Add(text);
        section.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Light,
            BorderColor = Palette.Border,
            Background = Palette.Panel,
            Child = sample,
        });
        return section;
    }

    private static Border ShadowStage(Shadow shadow)
    {
        ArgumentNullException.ThrowIfNull(shadow);
        var stage = new ControlCanvas
        {
            Width = Length.Cells(28),
            Height = Length.Cells(5),
            ClipToBounds = true,
        };
        ControlCanvas.SetLeft(shadow, Length.Cells(2));
        ControlCanvas.SetTop(shadow, Length.Cells(1));
        stage.Children.Add(shadow);
        return new Border
        {
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Light,
            BorderColor = Palette.Border,
            Background = Palette.Panel,
            Child = stage,
        };
    }

    private static void AddBorder(ControlStack examples, string name, Glyphs glyphs) =>
        examples.Children.Add(Card(new ControlText(name), glyphs));

    private static void AddGrid(ControlGrid grid, string text, int row, int column)
    {
        var child = Card(new ControlText(text), Glyphs.Light);
        ControlGrid.SetRow(child, row);
        ControlGrid.SetColumn(child, column);
        grid.Children.Add(child);
    }

    #endregion
}
