// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;



/// <summary>Documents the Window control with framed chrome and titled application surface specimens.</summary>
internal sealed class WindowPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Window";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var chromeOptions = Doc.Row(
            WindowVariant("Left", Glyphs.Rounded, WindowTitlePlacement.Left),
            WindowVariant("Center", Glyphs.Paired, WindowTitlePlacement.Center),
            WindowVariant("Right", Glyphs.Ascii, WindowTitlePlacement.Right));

        var apply = ActionButton(new Text("Apply"));
        apply.IsDefault = true;
        var cancel = ActionButton(new Text("Cancel"));
        cancel.IsCancel = true;
        var actionStatus = new Text("Action: waiting");
        apply.Click += (_, eventArgs) => actionStatus.Content = $"Action: Apply ({eventArgs.Cause})";
        cancel.Click += (_, eventArgs) => actionStatus.Content = $"Action: Cancel ({eventArgs.Cause})";
        var actions = Doc.Row(apply, cancel);
        actions.HorizontalAlignment = HorizontalAlignment.Center;

        var form = Doc.Column(
            new Text("Choose how this project opens."),
            new CheckBox
            {
                Content = new Text("Restore last session"),
                IsChecked = true,
                MarkStyle = CheckBoxMarks.Tick,
            },
            new CheckBox
            {
                Content = new Text("Start in safe mode"),
                MarkStyle = CheckBoxMarks.Brackets,
            },
            actions,
            actionStatus);

        var window = new Window()
        {
            Width = Length.Cells(42),
            Height = Length.Auto,
            Title = "Project settings",
            HasShadow = true,
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowOffset = new Point(2, 1),
            Child = form,
        };

        var stage = new Canvas()
        {
            Width = Length.Cells(48),
            Height = Length.Cells(13),
            ClipToBounds = true,
        };
        var workspace = ApplicationSurface(
            "Workspace  Build  Test  Help\n\n" +
            "Recent projects\n" +
            "  sharp-vision      modified now\n" +
            "  terminal-lab      modified 8m ago\n\n" +
            "Ready · 2 tasks running");
        workspace.Width = Length.Cells(48);
        workspace.Height = Length.Cells(13);
        stage.Children.Add(workspace);
        Canvas.SetLeft(window, Length.Cells(1));
        Canvas.SetTop(window, Length.Cells(1));
        stage.Children.Add(window);

        var composite = WindowVariant("Composite", Glyphs.Rounded, WindowTitlePlacement.Left);
        var block = WindowVariant("Block", Glyphs.Heavy, WindowTitlePlacement.Center);
        block.ShadowMode = ShadowMode.BlockGlyph;
        block.ShadowGlyph = new Rune('░');
        var flat = WindowVariant("No shadow", Glyphs.Ascii, WindowTitlePlacement.Right);
        flat.HasShadow = false;

        var styled = new Window
        {
            Width = Length.Cells(30),
            Height = Length.Cells(6),
            Title = "Styled surface",
            BorderColor = Color.Indexed(14),
            Background = Color.Indexed(0),
            Attributes = TerminalAttributes.Bold,
            Child = new Text("Explicit chrome over theme defaults"),
        };

        var overlayWindow = new Window
        {
            Width = Length.Cells(28),
            Height = Length.Cells(6),
            Title = "Overlay child",
            Child = new Button { Content = new Text("Focusable content") },
        };
        var overlayComposition = new Overlay
        {
            Width = Length.Cells(36),
            Height = Length.Cells(8),
            Children =
            {
                ApplicationSurface(
                    "Dashboard  Activity\n\n" +
                    "Build passing\nTests 89 / 89\n\nReady"),
                overlayWindow,
            },
        };
        Overlay.SetZIndex(overlayWindow, 5);

        var longTitle = new Window
        {
            Width = Length.Cells(18),
            Height = Length.Cells(4),
            Title = "A deliberately long title that clips safely",
            Child = new Text("Corners survive"),
        };
        var tiny = new Window
        {
            Width = Length.Cells(2),
            Height = Length.Cells(2),
            Title = "Tiny",
            Child = new Text("x"),
        };

        return Doc.Page(
            Title,
            "Frames one owned child as a titled terminal application surface with optional Turbo Vision-style shadowing.",
            Doc.Section(
                "🪟",
                "Frame and title",
                "Choose glyph family and title placement while retaining one capacity-one child contract.",
                Doc.Example(
                    "Rounded, paired, and ASCII",
                    "The three windows place titles left, center, and right without allowing text to overwrite corners.",
                    chromeOptions,
                    "var window = new Window\n{\n    Title = \"Project settings\",\n    Child = form,\n};")),
            Doc.Section(
                "🪟",
                "Shadows",
                "Composite, block-glyph, and flat windows share the same owned body and input behavior.",
                Doc.Example(
                    "Three depth treatments",
                    "Compare quiet composite darkening, visible block glyphs, and a surface with shadow disabled.",
                    Doc.Row(composite, block, flat))),
            Doc.Section(
                "🪟",
                "Default and cancel",
                "Unhandled Enter and Escape route to the first available IsDefault or IsCancel Button in the Window.",
                Doc.Example(
                    "Project settings surface",
                    "The Window visibly floats above a populated workspace while remaining an ordinary routed-input child. Move focus, then try Enter for Apply and Escape for Cancel.",
                    new Dock
                    {
                        BorderThickness = new Thickness(1),
                        BorderGlyphs = Glyphs.Light,
                        Children = { stage },
                    })),
            Doc.Section(
                "🪟",
                "Surface style",
                "Border, background, and attributes may explicitly override theme defaults on the complete Window surface.",
                Doc.Example(
                    "Explicit chrome",
                    "Only this Window owns the local color and attribute overrides; sibling windows continue following the theme.",
                    styled)),
            Doc.Section(
                "🪟",
                "Composition",
                "Window introduces no private modality, movement, or resize model; compose it inside Canvas or Overlay as ordinary content.",
                Doc.Example(
                    "Window above content",
                    "The focusable child remains in the surrounding routed-input tree while z-order controls presentation.",
                    overlayComposition)),
            Doc.Section(
                "🪟",
                "Boundaries",
                "Long titles clip before corners and tiny boxes saturate safely without drawing outside their committed bounds.",
                Doc.Example(
                    "Long and tiny windows",
                    "The long title preserves both corners; the two-cell surface degrades without negative interior geometry.",
                    Doc.Row(longTitle, tiny))));
    }

    private static Window WindowVariant(string title, Glyphs glyphs, WindowTitlePlacement placement) => new()
    {
        Width = Length.Cells(14),
        Height = Length.Cells(5),
        Title = title,
        TitlePlacement = placement,
        Glyphs = glyphs,
        ShadowOffset = new Point(1, 1),
        Child = new Text("Preview")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    private static Button ActionButton(Text content) => new()
    {
        Content = content,
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(0, 0, 1, 1),
    };

    private static Dock ApplicationSurface(string content) => new()
    {
        Background = ThemeColors.Surface,
        FillMode = FillMode.Opaque,
        BorderThickness = new Thickness(1),
        BorderGlyphs = Glyphs.Light,
        Padding = new Thickness(1, 0),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        Children = { new Text(content) },
    };
}
