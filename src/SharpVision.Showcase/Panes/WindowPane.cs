// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Text;

using Text = SharpVision.Controls.Text;



/// <summary>Documents the Window control with framed chrome and titled application surface specimens.</summary>
internal sealed class WindowPane: CompositeControl
{

    internal WindowPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Window";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        var chromeOptions = Doc.Row(
            WindowStage(WindowVariant("Left", Glyphs.Rounded, WindowTitlePlacement.Left), 18, 8),
            WindowStage(WindowVariant("Center", Glyphs.Paired, WindowTitlePlacement.Center), 18, 8),
            WindowStage(WindowVariant("Right", Glyphs.Ascii, WindowTitlePlacement.Right), 18, 8));

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
            ShadowMode = ShadowMode.Composite,
            ShadowOffset = new Point(1, 1),
            Content = form,
        };

        var stage = new Canvas()
        {
            Width = Length.Cells(52),
            Height = Length.Cells(15),
            ClipToBounds = true,
        };
        var workspace = ApplicationSurface(
            "Workspace  Build  Test  Help\n\n" +
            "Recent projects\n" +
            "  sharp-vision      modified now\n" +
            "  terminal-lab      modified 8m ago\n\n" +
            "Ready · 2 tasks running");
        workspace.Width = Length.Cells(52);
        workspace.Height = Length.Cells(15);
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
        var shadowVariants = Doc.Row(
            WindowStage(composite, 18, 8),
            WindowStage(block, 18, 8),
            WindowStage(flat, 18, 8));

        var styled = new Window
        {
            Width = Length.Cells(40),
            Height = Length.Cells(6),
            Title = "Styled surface",
            BorderColor = Color.Indexed(14),
            Background = Color.Indexed(0),
            Attributes = TerminalAttributes.Bold,
            Padding = new Thickness(1, 0),
            Content = new Text("Explicit chrome over theme defaults") { Overflow = Overflow.Wrap },
        };

        var overlayWindow = new Window
        {
            Width = Length.Cells(28),
            Height = Length.Cells(6),
            Title = "Overlay child",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new Button { Content = new Text("Focusable content") },
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
            Width = Length.Cells(24),
            Height = Length.Cells(5),
            Title = "A deliberately long title that clips safely",
            Content = new Text("Corners survive") { Overflow = Overflow.Wrap },
        };
        var minimum = new Window
        {
            Width = Length.Cells(14),
            Height = Length.Cells(4),
            Title = "Minimum",
            Content = new Text("Readable"),
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
                    "var window = new Window\n{\n    Title = \"Project settings\",\n    Content = form,\n};")),
            Doc.Section(
                "🪟",
                "Shadows",
                "Composite, block-glyph, and flat windows share the same owned body and input behavior.",
                Doc.Example(
                    "Three depth treatments",
                    "Compare quiet composite darkening, visible block glyphs, and a surface with shadow disabled.",
                    shadowVariants)),
            Doc.Section(
                "🪟",
                "Default and cancel",
                "Unhandled Enter and Escape route to the first available IsDefault or IsCancel Button in the Window.",
                Doc.Example(
                    "Project settings surface",
                    "The Window visibly floats above a populated workspace while remaining an ordinary routed-input child. Move focus, then try Enter for Apply and Escape for Cancel.",
                    stage)),
            Doc.Section(
                "🪟",
                "Surface style",
                "Border, background, and attributes may explicitly override theme defaults on the complete Window surface.",
                Doc.Example(
                    "Explicit chrome",
                    "Only this Window owns the local color and attribute overrides; sibling windows continue following the theme.",
                    WindowStage(styled, 44, 9))),
            Doc.Section(
                "🪟",
                "Movement",
                "Drag the title bar to reposition a window inside a Canvas parent. Set CanMove to false to lock position.",
                Doc.Example(
                    "Draggable window",
                    "Click and drag the title bar to move the window across the workspace surface.",
                    DraggableStage())),
            Doc.Section(
                "🪟",
                "Composition",
                "Compose windows inside Canvas or Overlay as ordinary routed-input children.",
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
                    "The long title preserves both corners; the minimum specimen remains readable. Exact two-cell saturation stays covered by Window unit tests.",
                    Doc.Row(
                        WindowStage(longTitle, 28, 8),
                        WindowStage(minimum, 18, 7)))));
    }

    private static Window WindowVariant(string title, Glyphs glyphs, WindowTitlePlacement placement) => new()
    {
        Width = Length.Cells(14),
        Height = Length.Cells(5),
        Title = title,
        TitlePlacement = placement,
        Glyphs = glyphs,
        ShadowOffset = new Point(1, 1),
        Content = new Text("Preview")
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

    private static Canvas DraggableStage()
    {
        var draggable = new Window
        {
            Width = Length.Cells(30),
            Height = Length.Cells(8),
            Title = "Drag me",
            Content = new Text("Click the title bar\nand drag to reposition\nthis window.") { Overflow = Overflow.Wrap },
        };
        Canvas.SetLeft(draggable, Length.Cells(4));
        Canvas.SetTop(draggable, Length.Cells(2));

        var stage = new Canvas
        {
            Width = Length.Cells(60),
            Height = Length.Cells(14),
            ClipToBounds = true,
        };
        var surface = ApplicationSurface(
            "Desktop\n\n" +
            "Drag the window by its title bar.\n" +
            "Release to place it at the new\n" +
            "position.\n\n" +
            "Ready");
        surface.Width = Length.Cells(60);
        surface.Height = Length.Cells(14);
        stage.Children.Add(surface);
        stage.Children.Add(draggable);
        return stage;
    }

    private static Overlay WindowStage(Window window, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.HorizontalAlignment = HorizontalAlignment.Center;
        window.VerticalAlignment = VerticalAlignment.Center;
        return new Overlay
        {
            Width = Length.Cells(width),
            Height = Length.Cells(height),
            ClipToBounds = true,
            Children =
            {
                ApplicationSurface("Workspace\n\nReady"),
                window,
            },
        };
    }
}
