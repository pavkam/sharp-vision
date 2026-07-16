// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Text;

using Text = SharpVision.Controls.Text;



/// <summary>Documents the Window control with draggable frames, shadow depth, and modal dialog specimens.</summary>
internal sealed class WindowPane: CompositeControl
{

    internal WindowPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Window";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        var draggable = new Window
        {
            Width = Length.Cells(34),
            Height = Length.Auto,
            Title = "Draggable settings",
            Content = CreateSettingsForm(),
        };
        Canvas.SetLeft(draggable, Length.Cells(16));
        Canvas.SetTop(draggable, Length.Cells(1));

        var dragSurface = Workspace(
            "Dashboard  Activity  Settings\n\n" +
            "Recent projects\n" +
            "  sharp-vision      now\n" +
            "  terminal-lab      8m ago\n\n" +
            "Drag the window by its title bar.");
        dragSurface.Width = Length.Cells(58);
        dragSurface.Height = Length.Cells(14);
        var dragStage = new Canvas
        {
            Width = Length.Cells(58),
            Height = Length.Cells(14),
            ClipToBounds = true,
        };
        dragStage.Children.Add(dragSurface);
        dragStage.Children.Add(draggable);

        var dialog = new Window
        {
            Width = Length.Cells(32),
            Height = Length.Auto,
            Title = "Confirm",
            Visibility = Visibility.Collapsed,
        };
        var dialogStatus = new Text("Dialog: closed");
        var closeBtn = new Button { Content = new Text("Close"), IsCancel = true };
        var okBtn = new Button { Content = new Text("OK"), IsDefault = true };
        closeBtn.Click += (_, _) =>
        {
            dialog.Visibility = Visibility.Collapsed;
            dialogStatus.Content = "Dialog: closed";
        };
        okBtn.Click += (_, _) =>
        {
            dialog.Visibility = Visibility.Collapsed;
            dialogStatus.Content = "Dialog: confirmed";
        };
        dialog.Content = Doc.Column(
            new Text("Proceed with deployment?") { Overflow = Overflow.Wrap },
            Doc.Row(okBtn, closeBtn));
        var openBtn = new Button { Content = new Text("Open dialog") };
        openBtn.Click += (_, _) =>
        {
            dialog.Visibility = Visibility.Visible;
            dialogStatus.Content = "Dialog: open";
        };
        Canvas.SetLeft(dialog, Length.Cells(14));
        Canvas.SetTop(dialog, Length.Cells(2));
        Canvas.SetLeft(openBtn, Length.Cells(2));
        Canvas.SetTop(openBtn, Length.Cells(2));
        Canvas.SetLeft(dialogStatus, Length.Cells(2));
        Canvas.SetTop(dialogStatus, Length.Cells(5));

        var dialogSurface = Workspace("Application workspace\n\nReady");
        dialogSurface.Width = Length.Cells(52);
        dialogSurface.Height = Length.Cells(10);
        var dialogStage = new Canvas
        {
            Width = Length.Cells(52),
            Height = Length.Cells(10),
            ClipToBounds = true,
        };
        dialogStage.Children.Add(dialogSurface);
        dialogStage.Children.Add(openBtn);
        dialogStage.Children.Add(dialogStatus);
        dialogStage.Children.Add(dialog);

        var composite = FrameVariant("Composite", Glyphs.Rounded, WindowTitlePlacement.Left);
        var block = FrameVariant("Block", Glyphs.Heavy, WindowTitlePlacement.Center);
        block.ShadowMode = ShadowMode.BlockGlyph;
        block.ShadowGlyph = new Rune('░');
        var flat = FrameVariant("No shadow", Glyphs.Ascii, WindowTitlePlacement.Right);
        flat.HasShadow = false;

        var shadowRow = Doc.Row(
            FrameStage(composite, 18, 8),
            FrameStage(block, 18, 8),
            FrameStage(flat, 18, 8));

        var leftTitle = FrameVariant("Left", Glyphs.Rounded, WindowTitlePlacement.Left);
        var centerTitle = FrameVariant("Center", Glyphs.Paired, WindowTitlePlacement.Center);
        var rightTitle = FrameVariant("Right", Glyphs.Ascii, WindowTitlePlacement.Right);
        var titleRow = Doc.Row(
            FrameStage(leftTitle, 18, 8),
            FrameStage(centerTitle, 18, 8),
            FrameStage(rightTitle, 18, 8));

        return Doc.Page(
            Title,
            "Frames one owned child as a titled terminal window with optional shadow and title-bar dragging.",
            Doc.Section(
                "🪟",
                "Draggable window",
                "Click and drag the title bar to reposition the window inside a Canvas. CanMove is true by default.",
                Doc.Example(
                    "Settings dialog over a workspace",
                    "Drag the title bar to move the window. The window content, shadow, and border follow the pointer.",
                    dragStage,
                    "var window = new Window { Title = \"Settings\", Content = form };\nCanvas.SetLeft(window, Length.Cells(16));\nCanvas.SetTop(window, Length.Cells(1));")),
            Doc.Section(
                "🪟",
                "Modal dialog",
                "Open a window with OK and Close buttons. Escape routes to the IsCancel button, Enter routes to the IsDefault button.",
                Doc.Example(
                    "Open, confirm, or cancel",
                    "Click Open dialog to show the confirmation window. Press Enter for OK or Escape for Close.",
                    dialogStage)),
            Doc.Section(
                "🪟",
                "Shadow depth",
                "Each window uses a different shadow mode. Composite blends with the surface, Block uses a visible glyph, and flat removes the shadow entirely.",
                Doc.Example(
                    "Composite, block, and flat",
                    "Compare the three shadow treatments on windows with the same content.",
                    shadowRow)),
            Doc.Section(
                "🪟",
                "Title placement",
                "Titles can be aligned Left, Center, or Right within the top frame edge. Different border glyph families change the visual weight.",
                Doc.Example(
                    "Rounded, paired, and ASCII",
                    "Three windows show the same content with different title placement and border styles.",
                    titleRow,
                    "var window = new Window\n{\n    Title = \"Center\",\n    Glyphs = Glyphs.Paired,\n    TitlePlacement = WindowTitlePlacement.Center,\n};")));
    }

    private static Stack CreateSettingsForm()
    {
        var apply = new Button { Content = new Text("Apply"), IsDefault = true };
        var cancel = new Button { Content = new Text("Cancel"), IsCancel = true };
        var status = new Text("Action: waiting");
        apply.Click += (_, e) => status.Content = $"Action: Apply ({e.Cause})";
        cancel.Click += (_, e) => status.Content = $"Action: Cancel ({e.Cause})";

        return Doc.Column(
            new Text("Choose project options."),
            new CheckBox { Content = new Text("Restore last session"), IsChecked = true },
            new CheckBox { Content = new Text("Start in safe mode") },
            Doc.Row(apply, cancel),
            status);
    }

    private static Window FrameVariant(string title, Glyphs glyphs, WindowTitlePlacement placement) => new()
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

    private static Dock Workspace(string content) => new()
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

    private static Overlay FrameStage(Window window, int width, int height)
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
                Workspace("Workspace\n\nReady"),
                window,
            },
        };
    }
}
