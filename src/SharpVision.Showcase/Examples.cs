using System.Text;

using SharpVision.Controls;
using SharpVision.Fonts;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
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
        var fontNames = catalog.Names.ToArray();
        var picker = new ComboBox
        {
            Width = Length.Cells(30),
            Items = fontNames,
            SelectedIndex = Array.IndexOf(fontNames, "Standard"),
            DropDownHeight = 8,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarChrome = ScrollBarStyle.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            Style = Palette.Interactive(),
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
        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedIndex < 0 || picker.Items[picker.SelectedIndex] is not string name)
            {
                return;
            }

            // Load only the selected audited font; the archive is never expanded wholesale.
            preview.Font = catalog.Load(name);
            status.Content = $"Previewing {name}. Choose another font to compare it.";
        };
        var examples = Vertical();
        examples.Children.Add(text);
        examples.Children.Add(picker);
        examples.Children.Add(status);
        examples.Children.Add(preview);
        return examples;
    }

    /// <summary>Creates styled, linked, wrapped Unicode inline content.</summary>
    internal static Control RichText()
    {
        var examples = Vertical();
        var introductory = new RichText
        {
            Wrapping = Wrapping.Word,
            TextAlignment = Alignment.Start,
        };
        introductory.Inlines.Add(new ControlRun("Rich ")
        {
            Attributes = Attributes.Bold,
            Foreground = Palette.Success,
        });
        introductory.Inlines.Add(new ControlRun("terminal text") { Attributes = Attributes.Italic });
        introductory.Inlines.Add(new LineBreak());
        introductory.Inlines.Add(new ControlRun("Unicode: café · 你好 · 👩‍💻 · "));
        introductory.Inlines.Add(new Hyperlink("project source", "https://github.com/pavkam")
        {
            Attributes = Attributes.Underline,
            Foreground = Palette.Accent,
        });
        examples.Children.Add(SampleSection(
            "Styled document and OSC 8 link",
            "Runs carry independent foreground, attributes, and hyperlink metadata. The link is explicitly underlined as well as semantic; compatible terminals expose it on hover or open it with their configured gesture.",
            Card(introductory, Glyphs.Rounded)));

        var attributes = new RichText { Wrapping = Wrapping.Word };
        AddAttributeLine(attributes, "Bold", "increased intensity", Attributes.Bold, Palette.Text);
        AddAttributeLine(attributes, "Dim", "reduced intensity", Attributes.Dim, Palette.Muted);
        AddAttributeLine(attributes, "Italic", "slanted presentation", Attributes.Italic, Palette.Accent);
        AddAttributeLine(attributes, "Underline", "single underline", Attributes.Underline, Palette.Success);
        AddAttributeLine(attributes, "Blink", "blink requested; terminal policy may suppress it", Attributes.Blink, Palette.Warning);
        AddAttributeLine(attributes, "Reverse", "foreground and background exchanged", Attributes.Reverse, Palette.Accent);
        AddAttributeLine(attributes, "Strike", "strikethrough presentation", Attributes.Strike, Palette.Warning);
        AddAttributeLine(attributes, "Hidden", "concealed run follows", Attributes.Hidden, Palette.Muted);
        attributes.Inlines.Add(new ControlRun(" (the concealed sample is intentional)") { Foreground = Palette.Muted });
        AddAttributeLine(
            attributes,
            "Combined",
            "bold + underline + italic",
            Attributes.Bold | Attributes.Underline | Attributes.Italic,
            Palette.Success);
        examples.Children.Add(SampleSection(
            "Terminal text attributes",
            "Every row below is a real RichText run. Bold, dim, italic, underline, blink, reverse, concealed, and strike are terminal cell attributes; blink and concealed output remain subject to terminal settings.",
            Card(attributes, Glyphs.Light)));

        var wrapped = new RichText { Width = Length.Cells(30), Wrapping = Wrapping.Word };
        wrapped.Inlines.Add(new ControlRun("Resize this narrow reading column. RichText wraps between words while keeping Unicode graphemes intact. "));
        wrapped.Inlines.Add(new Hyperlink("Read the protocol guide", "https://invisible-island.net/xterm/ctlseqs/ctlseqs.html"));

        var activity = new ControlText("Activity log: waiting for an inline mutation.")
        {
            Foreground = Palette.Muted,
        };
        var append = new Button
        {
            Content = new ControlText("Append a Run"),
            Style = Palette.Interactive(),
        };
        var mutation = 0;
        var mutationStyles = new[]
        {
            (Name: "underline", Value: Attributes.Underline, Color: Palette.Success),
            (Name: "strikethrough", Value: Attributes.Strike, Color: Palette.Warning),
            (Name: "reverse", Value: Attributes.Reverse, Color: Palette.Accent),
            (Name: "bold + italic", Value: Attributes.Bold | Attributes.Italic, Color: Palette.Text),
        };
        append.Click += (_, eventArgs) =>
        {
            var selectedStyle = mutationStyles[mutation % mutationStyles.Length];
            mutation++;
            wrapped.Inlines.Add(new LineBreak());
            wrapped.Inlines.Add(new ControlRun(
                $"Mutation {mutation}: {selectedStyle.Name} Run appended through the {eventArgs.Cause} path.")
            {
                Attributes = selectedStyle.Value,
                Foreground = selectedStyle.Color,
            });
            activity.Content = $"Activity log: {eventArgs.Cause} appended {selectedStyle.Name} Run {mutation}.";
        };

        var readingExample = Vertical();
        readingExample.Children.Add(wrapped);
        readingExample.Children.Add(ButtonSpecimen(append));
        readingExample.Children.Add(activity);
        examples.Children.Add(SampleSection(
            "Responsive reading column",
            "A constrained document is useful for help panes, release notes, and inline documentation. Activate the button to append a differently styled run and watch the log.",
            Card(readingExample, Glyphs.Light)));
        return examples;
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
        examples.Children.Add(SampleSection(
            "Unicode-safe wrapping",
            "Word wrapping leaves complete grapheme clusters together, including combining marks and wide emoji.",
            new ControlText("Plain Unicode: café · 你好 · 👩‍💻\nA narrow reading column wraps words without splitting clusters.")
            {
                Width = Length.Cells(28),
                Wrapping = Wrapping.Word,
            }));
        examples.Children.Add(SampleSection(
            "Centered label",
            "Centering is for compact labels and status messages; it is deliberately shown without trimming.",
            new ControlText("Centered status")
            {
                Width = Length.Cells(28),
                TextAlignment = Alignment.Center,
                Foreground = Palette.Warning,
                Attributes = Attributes.Bold,
            }));
        examples.Children.Add(SampleSection(
            "Single-line truncation",
            "Ellipsis is for one-line labels where the remaining space matters more than wrapping.",
            new ControlText("This deliberately long one-line label trims safely")
            {
                Width = Length.Cells(28),
                Trimming = Trimming.GraphemeEllipsis,
                Foreground = Palette.Accent,
            }));
        return examples;
    }

    #endregion

    #region Interactive controls

    /// <summary>Creates enabled, disabled, default, cancel, and live-click button variants.</summary>
    internal static Control Button()
    {
        var examples = Vertical();
        var status = new ControlText("Activation log: waiting");
        var active = new Button
        {
            Content = new ControlText("Click or press Enter"),
            Style = Palette.Interactive(),
        };
        active.Click += (_, eventArgs) =>
            status.Content = $"Activation log: {eventArgs.Cause}";
        var primary = Vertical();
        primary.Children.Add(ButtonSpecimen(active));
        primary.Children.Add(status);
        examples.Children.Add(SampleSection(
            "Primary action",
            "A raised, bordered action surface responds to hover, focus, press, Enter, Space, and a primary pointer click.",
            primary));

        var roles = Horizontal();
        roles.Children.Add(ButtonSpecimen(new Button
        {
            Content = new ControlText("Default action"),
            IsDefault = true,
            Style = Palette.Interactive(),
        }));
        roles.Children.Add(ButtonSpecimen(new Button
        {
            Content = new ControlText("Cancel action"),
            IsCancel = true,
            Style = Palette.Interactive(),
        }));
        examples.Children.Add(SampleSection(
            "Dialog command roles",
            "Default and cancel roles let an owning dialog map Enter and Escape to conventional actions.",
            roles));

        examples.Children.Add(SampleSection(
            "Turbo Vision block shadow",
            "Composite is a quiet surface lift. Block glyph mode deliberately draws a visible shade footprint when the control needs old-school terminal depth.",
            ButtonSpecimen(new Button
            {
                Content = new ControlText("Block glyph shadow"),
                ShadowMode = ShadowMode.BlockGlyph,
                ShadowGlyph = new Rune('░'),
                Style = Palette.Interactive(),
            })));

        examples.Children.Add(SampleSection(
            "Disabled action",
            "Unavailable actions remain readable but do not accept focus, pointer capture, or activation.",
            ButtonSpecimen(new Button
            {
                Content = new ControlText("Disabled"),
                IsEnabled = false,
                Style = Palette.Interactive(),
            })));
        return examples;
    }

    /// <summary>Creates two-state, three-state, custom-mark, and disabled check boxes.</summary>
    internal static Control CheckBox()
    {
        var examples = Vertical();
        var brackets = Vertical();
        brackets.Children.Add(new CheckBox
        {
            Content = new ControlText("Unchecked brackets"),
            MarkStyle = CheckBoxStyle.Brackets,
            Style = Palette.Interactive(),
        });
        brackets.Children.Add(new CheckBox
        {
            Content = new ControlText("Checked brackets"),
            IsChecked = true,
            MarkStyle = CheckBoxStyle.Brackets,
            Style = Palette.Interactive(),
        });
        brackets.Children.Add(new CheckBox
        {
            Content = new ControlText("Indeterminate brackets"),
            IsThreeState = true,
            IsChecked = null,
            MarkStyle = CheckBoxStyle.Brackets,
            Style = Palette.Interactive(),
        });
        examples.Children.Add(SampleSection(
            "Bracket marks",
            "Classic [ ] / [x] marks reserve three cells, so toggling and indeterminate state never shift the label.",
            Card(brackets, Glyphs.Rounded)));
        examples.Children.Add(SampleSection(
            "Disabled bracket state",
            "The familiar [x] mark stays structurally recognizable while the disabled palette deliberately recedes from interactive choices.",
            Card(new CheckBox
            {
                Content = new ControlText("Disabled bracket"),
                IsChecked = true,
                IsEnabled = false,
                MarkStyle = CheckBoxStyle.Brackets,
                Style = Palette.Interactive(),
            }, Glyphs.Light)));
        return examples;
    }

    /// <summary>Creates a real popup-style combo box with pointer and keyboard selection feedback.</summary>
    internal static Control ComboBox()
    {
        var status = new ControlText("Selected: Comfortable") { Foreground = Palette.Muted };
        var comboBox = new ComboBox
        {
            Width = Length.Cells(28),
            Items = ["Compact", "Comfortable", "Spacious"],
            SelectedIndex = 1,
            DropDownHeight = 4,
            Style = Palette.Interactive(),
        };
        comboBox.SelectionChanged += (_, _) =>
        {
            status.Content = comboBox.SelectedIndex >= 0
                ? $"Selected: {comboBox.Items[comboBox.SelectedIndex]}."
                : "No selection.";
        };
        var stage = new ControlCanvas
        {
            Width = Length.Cells(30),
            Height = Length.Cells(6),
            ClipToBounds = false,
        };
        stage.Children.Add(comboBox);
        var examples = Vertical();
        examples.Children.Add(SampleSection(
            "Popup choice field",
            "Click or press Enter/Space to open. The popup list owns arrow navigation; Enter chooses and closes it, while Escape dismisses it.",
            stage));
        examples.Children.Add(status);
        return examples;
    }

    /// <summary>Creates a scrollable selectable list beside an explicitly explained disabled list.</summary>
    internal static Control List()
    {
        var status = new ControlText("Selected item: Beta. Use Up or Down to move the selection.")
        {
            Foreground = Palette.Muted,
        };
        var active = new ControlList
        {
            Width = Length.Cells(18),
            Height = Length.Cells(6),
            Style = Palette.List(),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarChrome = ScrollBarStyle.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            Items = new object?[]
            {
                "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta",
            },
            SelectedIndex = 1,
        };
        active.SelectionChanged += (_, _) =>
        {
            status.Content = active.SelectedIndex >= 0
                ? $"Selected item: {active.Items[active.SelectedIndex]}. Use Up or Down to move the selection."
                : "No item selected.";
        };
        active.ItemInvoked += (_, eventArgs) =>
            status.Content = $"Activated {eventArgs.Item} via {eventArgs.Cause}.";

        var disabled = new ControlList
        {
            Width = Length.Cells(18),
            Height = Length.Cells(4),
            IsEnabled = false,
            Style = Palette.List(),
            Items = new object?[] { "Alpha", "Beta", "Gamma" },
        };

        var examples = Vertical();
        examples.Children.Add(SampleSection(
            "Selectable list",
            "The focused list accepts Up, Down, paging, Enter, and pointer clicks. The status line reports the current selection or activation.",
            active));
        examples.Children.Add(SampleSection(
            "Disabled list",
            "These rows stay visible so the data context is clear, but IsEnabled is false: the list cannot receive focus, change selection, or invoke an item.",
            disabled));
        examples.Children.Add(status);
        return examples;
    }

    /// <summary>Creates a keyboard and pointer navigable vertical command menu with check and radio choices.</summary>
    internal static Control Menu()
    {
        var examples = Vertical();
        var status = new ControlText("Choose an action.") { Foreground = Palette.Muted };
        var menu = new Menu
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            Style = Palette.Interactive(),
        };
        menu.Items.Add(new MenuItem { Header = "New project" });
        menu.Items.Add(new MenuItem { Header = "Open recent" });
        menu.Items.Add(new MenuItem { Kind = MenuItemKind.Separator });
        menu.Items.Add(new MenuItem { Header = "Auto save", Kind = MenuItemKind.Check, IsChecked = true });
        menu.Items.Add(new MenuItem { Header = "Compact mode", Kind = MenuItemKind.Radio, GroupName = "density", IsChecked = true });
        menu.Items.Add(new MenuItem { Header = "Comfortable mode", Kind = MenuItemKind.Radio, GroupName = "density" });
        menu.ItemInvoked += (_, eventArgs) => status.Content = $"Invoked {eventArgs.Item.Header}.";
        examples.Children.Add(SampleSection(
            "Command menu",
            "Use arrow keys to skip the separator, Enter or Space to invoke, or click an item. Check and radio states commit before the invocation message.",
            new Border
            {
                BorderThickness = new Thickness(1),
                Glyphs = Glyphs.Rounded,
                BorderColor = Palette.Border,
                Background = Palette.Surface,
                Child = menu,
            }));
        examples.Children.Add(status);
        return examples;
    }

    /// <summary>Creates one mutually exclusive named radio group with a disabled option.</summary>
    internal static Control RadioButton()
    {
        var examples = Vertical();
        var group = Vertical();
        group.Children.Add(new RadioButton
        {
            Content = new ControlText("Fast"),
            GroupName = "quality",
            IsChecked = true,
            Style = Palette.Interactive(),
        });
        group.Children.Add(new RadioButton
        {
            Content = new ControlText("Balanced"),
            GroupName = "quality",
            Style = Palette.Interactive(),
        });
        group.Children.Add(new RadioButton
        {
            Content = new ControlText("Unavailable"),
            GroupName = "quality",
            IsEnabled = false,
            Style = Palette.Interactive(),
        });
        examples.Children.Add(SampleSection(
            "Named quality group",
            "Pick one mode. Arrow keys move selection between available members; the disabled member remains visibly unavailable.",
            Card(group, Glyphs.Rounded)));

        var independent = new RadioButton
        {
            Content = new ControlText("Independent selection group"),
            GroupName = "delivery",
            IsChecked = true,
            Style = Palette.Interactive(),
        };
        examples.Children.Add(SampleSection(
            "Separate group",
            "A different GroupName scopes selection independently, so this choice does not disturb the quality group.",
            Card(independent, Glyphs.Light)));
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
        var full = Vertical();
        full.Children.Add(horizontal);
        full.Children.Add(status);
        examples.Children.Add(SampleSection(
            "Full horizontal rail",
            "Drag the solid thumb, click the shaded track for page movement, or use the arrow buttons for line movement.",
            Card(full, Glyphs.Rounded)));

        var vertical = new ScrollBar
        {
            Height = Length.Cells(8),
            Maximum = 40,
            ViewportSize = 10,
            Value = 12,
            DecrementGlyph = new Rune('▲'),
            IncrementGlyph = new Rune('▼'),
            TrackGlyph = new Rune('│'),
            ThumbGlyph = new Rune('█'),
        };
        examples.Children.Add(SampleSection(
            "Vertical rail",
            "The same canonical ScrollBar changes orientation while retaining keyboard, wheel, track, and live drag behavior.",
            Card(vertical, Glyphs.Light)));

        examples.Children.Add(SampleSection(
            "Thin line chrome",
            "Thin rails omit buttons to conserve cells; a heavy line thumb remains distinct from the passive track.",
            Card(new ScrollBar
            {
                Width = Length.Cells(28),
                Orientation = Orientation.Horizontal,
                Chrome = ScrollBarStyle.Thin,
                Fill = ScrollBarFill.Line,
                Maximum = 100,
                Value = 62,
                ViewportSize = 30,
            }, Glyphs.Paired)));
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
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarChrome = ScrollBarStyle.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            Text = "Multiline editor\nWheel here to scroll\nwithout losing focus\nAt the edge, the page scrolls",
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

    /// <summary>Creates an anchored popup menu with pointer and keyboard selection feedback.</summary>
    internal static Control Popup()
    {
        var status = new ControlText("Choose an item with the mouse, arrows, or Enter.")
        {
            Foreground = Palette.Muted,
        };
        var trigger = new Button
        {
            Content = new ControlText("Actions ▼"),
            Style = Palette.Interactive(),
        };
        var choices = new ControlList
        {
            Width = Length.Cells(24),
            Height = Length.Cells(5),
            Items = ["Duplicate", "Rename", "Archive", "Delete"],
            SelectedIndex = 0,
            Style = Palette.Interactive(),
        };
        var popup = new Popup
        {
            Anchor = trigger,
            Placement = PopupPlacement.Below,
            Glyphs = Glyphs.Rounded,
            BorderColor = Palette.Accent,
            Background = Palette.Surface,
            Child = choices,
        };
        trigger.Click += (_, _) => popup.IsOpen = !popup.IsOpen;
        choices.ItemInvoked += (_, eventArgs) =>
        {
            status.Content = eventArgs.Item is string choice
                ? $"Selected {choice}."
                : "No action selected.";
            popup.IsOpen = false;
        };
        var content = Vertical();
        content.Children.Add(SampleSection(
            "Anchored action menu",
            "Open the compact menu, then select with the mouse or keyboard. Escape closes it without selecting anything.",
            ButtonSpecimen(trigger)));
        content.Children.Add(status);
        var overlay = new ControlOverlay { ClipToBounds = false };
        overlay.Children.Add(content);
        ControlOverlay.SetZIndex(popup, 10);
        overlay.Children.Add(popup);
        return overlay;
    }

    /// <summary>Creates a framed application window with an owned settings form and block shadow.</summary>
    internal static Control Window()
    {
        var chromeOptions = Horizontal();
        chromeOptions.Children.Add(WindowVariant("Left", Glyphs.Rounded, WindowTitlePlacement.Left));
        chromeOptions.Children.Add(WindowVariant("Center", Glyphs.Paired, WindowTitlePlacement.Center));
        chromeOptions.Children.Add(WindowVariant("Right", Glyphs.Ascii, WindowTitlePlacement.Right));

        var form = Vertical();
        form.Children.Add(new ControlText("Choose how this project opens.")
        {
            Foreground = Palette.Text,
        });
        form.Children.Add(new CheckBox
        {
            Content = new ControlText("Restore last session"),
            IsChecked = true,
            MarkStyle = CheckBoxStyle.Tick,
            Style = Palette.Interactive(),
        });
        form.Children.Add(new CheckBox
        {
            Content = new ControlText("Start in safe mode"),
            MarkStyle = CheckBoxStyle.Brackets,
            Style = Palette.Interactive(),
        });
        var actions = Horizontal();
        actions.HorizontalAlignment = HorizontalAlignment.Center;
        actions.Children.Add(ButtonSpecimen(new Button
        {
            Content = new ControlText("Apply"),
            IsDefault = true,
            Style = Palette.Interactive(),
        }));
        actions.Children.Add(ButtonSpecimen(new Button
        {
            Content = new ControlText("Cancel"),
            IsCancel = true,
            Style = Palette.Interactive(),
        }));
        form.Children.Add(actions);
        var window = new Window
        {
            Width = Length.Cells(42),
            Height = Length.Auto,
            Title = "Project settings",
            BorderColor = Palette.Accent,
            Background = Palette.Surface,
            HasShadow = true,
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowOffset = new Point(2, 1),
            Child = form,
        };
        var stage = new ControlCanvas
        {
            Width = Length.Cells(48),
            Height = Length.Cells(13),
            ClipToBounds = true,
        };
        ControlCanvas.SetLeft(window, Length.Cells(1));
        ControlCanvas.SetTop(window, Length.Cells(1));
        stage.Children.Add(window);
        var examples = Vertical();
        examples.Children.Add(SampleSection(
            "Border and title options",
            "Each Window uses the same child contract with a different Glyphs family and title placement: rounded left, paired center, and portable ASCII right.",
            chromeOptions));
        examples.Children.Add(SampleSection(
            "Titled application surface",
            "A Window owns its interior while title chrome, frame, and shadow render as one terminal-safe surface. Try Enter for Apply or Escape for Cancel.",
            new Border
            {
                BorderThickness = new Thickness(1),
                Glyphs = Glyphs.Light,
                BorderColor = Palette.Border,
                Background = Palette.Panel,
                Child = stage,
            }));
        return examples;
    }

    private static Window WindowVariant(string title, Glyphs glyphs, WindowTitlePlacement placement) => new()
    {
        Width = Length.Cells(14),
        Height = Length.Cells(5),
        Title = title,
        TitlePlacement = placement,
        Glyphs = glyphs,
        BorderColor = Palette.Accent,
        Background = Palette.Surface,
        ShadowOffset = new Point(1, 1),
        Child = new ControlText("Preview")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Palette.Text,
        },
    };

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
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
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
        examples.Children.Add(SampleSection(
            "Mixed horizontal tracks",
            "Fixed cells, percentage sizing, and proportional remainder can coexist in one horizontal Stack.",
            horizontal));
        var reversed = Horizontal();
        reversed.Reverse = true;
        reversed.Children.Add(Card(new ControlText("First"), Glyphs.Light));
        reversed.Children.Add(Card(new ControlText("Second"), Glyphs.Heavy));
        reversed.Children.Add(Card(new ControlText("Third"), Glyphs.Paired));
        examples.Children.Add(SampleSection(
            "Reverse order",
            "Reverse changes visual and keyboard-navigation order without changing the source child collection.",
            reversed));

        var vertical = Vertical();
        vertical.Children.Add(Card(new ControlText("Top"), Glyphs.Rounded));
        vertical.Children.Add(Card(new ControlText("Spacing = 1"), Glyphs.Light));
        vertical.Children.Add(Card(new ControlText("Bottom"), Glyphs.Heavy));
        examples.Children.Add(SampleSection(
            "Vertical spacing",
            "Vertical is the default orientation; explicit spacing is applied only between participating children.",
            vertical));
        return examples;
    }

    /// <summary>Creates styled fixed, percentage, fill, headerless, and rich-cell table specimens.</summary>
    internal static Control Table()
    {
        var primary = new Table
        {
            Width = Length.Cells(58),
            HeaderForeground = Palette.Text,
            HeaderBackground = Palette.Highlight,
            GridLineColor = Palette.Border,
            CellPadding = new Thickness(1, 0),
            RowSpacing = 1,
        };
        primary.Columns.Add(TableColumn.Fixed("Name", 12));
        primary.Columns.Add(TableColumn.Percent("Status", 25));
        primary.Columns.Add(TableColumn.Fill("Details"));
        primary.Rows.Add(new TableRow([
            new ControlText("Terminal core"),
            new ControlText("Stable") { Foreground = Palette.Success },
            new ControlText("ANSI, OSC, CSI, and input decoding."),
        ]));
        var linked = new RichText { Wrapping = Wrapping.Word };
        linked.Inlines.Add(new ControlRun("Open "));
        linked.Inlines.Add(new Hyperlink("protocol guide", "https://invisible-island.net/xterm/ctlseqs/ctlseqs.html"));
        primary.Rows.Add(new TableRow([
            new ControlText("UI toolkit"),
            new ControlText("Preview") { Foreground = Palette.Warning },
            linked,
        ]));

        var compact = new Table
        {
            Width = Length.Cells(42),
            ShowHeader = false,
            ShowGridLines = false,
            CellPadding = new Thickness(1, 0),
            ColumnSpacing = 2,
        };
        compact.Columns.Add(TableColumn.Auto("Key"));
        compact.Columns.Add(TableColumn.Fill("Meaning"));
        compact.Rows.Add(new TableRow([new ControlText("Enter"), new ControlText("Apply the default action")]));
        compact.Rows.Add(new TableRow([new ControlText("Escape"), new ControlText("Dismiss a popup or cancel a window")]));

        var examples = Vertical();
        examples.Children.Add(SampleSection(
            "Mixed column sizing",
            "Fixed identity, percentage status, and fill details stay contained while the rich detail cell wraps and preserves its OSC 8 link.",
            primary));
        examples.Children.Add(SampleSection(
            "Headerless key/value table",
            "A compact table can omit headers and grid lines when spacing and cell padding carry the structure.",
            compact));
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

    private static void AddAttributeLine(
        RichText document,
        string label,
        string sample,
        Attributes attributes,
        Color foreground)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(sample);

        if (document.Inlines.Count > 0)
        {
            document.Inlines.Add(new LineBreak());
        }

        document.Inlines.Add(new ControlRun($"{label}: ") { Foreground = Palette.Muted });
        document.Inlines.Add(new ControlRun(sample)
        {
            Attributes = attributes,
            Foreground = foreground,
        });
    }

    private static Border Card(Control child, Glyphs glyphs) => new()
    {
        Child = child,
        BorderThickness = new Thickness(1),
        Glyphs = glyphs,
        Padding = new Thickness(1, 0),
    };

    private static Button ButtonSpecimen(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Margin = new Thickness(0, 0, 1, 1);
        return button;
    }

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

    private static ControlStack SampleSection(string heading, string description, Control sample)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(sample);
        var section = Vertical();
        var text = new RichText { Wrapping = Wrapping.Word };
        text.Inlines.Add(new ControlRun(heading)
        {
            Foreground = Palette.Success,
            Attributes = Attributes.Bold,
        });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new ControlRun(description) { Foreground = Palette.Muted });
        section.Children.Add(text);
        section.Children.Add(sample);
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
