// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents Window roles, bracketed frame chrome, interaction, and modal presentation.</summary>
internal sealed class WindowPane: CompositeControl
{
    internal WindowPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Window";

    private static DocPage CreateContent()
    {
        // Static role comparison: normal versus dialog defaults.
        var normalRole = new Window
        {
            UseMnemonic = false,
            Width = Length.Cells(24),
            Height = Length.Cells(7),
            Header = "Normal",
            CanClose = true,
            Content = RoleDescription("Movable application surface", "Paired frame · left title")
        };
        var dialogRole = new Window
        {
            CanMove = false,
            CanClose = true,
            CloseOnEscape = true,
            HeaderPlacement = WindowTitlePlacement.Center,
            UseMnemonic = false,
            Width = Length.Cells(24),
            Height = Length.Cells(7),
            Header = "Dialog",
            Content = RoleDescription("Focused decision surface", "Paired frame · centered title")
        };
        var roleRow = new DocRow(
            FrameStage(normalRole, 29, 10),
            FrameStage(dialogRole, 29, 10));

        // Draggable normal window with reopen control on a framed workspace canvas.
        var draggable = new Window
        {
            UseMnemonic = false,
            Width = Length.Cells(40),
            Height = Length.Auto,
            Header = "Draggable settings",
            CanClose = true,
            Content = CreateSettingsForm()
        };
        var activityWindow = new Window
        {
            UseMnemonic = false,
            Width = Length.Cells(24),
            Height = Length.Cells(7),
            Header = "Activity",
            Content = RoleDescription("Modeless activity", "Click the title to activate")
        };
        var windowStatus = new Text("Window: open");
        var activationStatus = new Text("Active Window: none");
        var reopenWindow = new Button { Content = new Text("Reopen &window") };
        draggable.Closing += (_, _) =>
        {
            draggable.Visibility = Visibility.Collapsed;
            windowStatus.Content = "Window: closed";
        };
        reopenWindow.Click += (_, _) =>
        {
            draggable.Visibility = Visibility.Visible;
            windowStatus.Content = "Window: open";
        };

        void ReportActivation() => activationStatus.Content = draggable.IsActive
            ? "Active Window: Draggable settings"
            : activityWindow.IsActive
                ? "Active Window: Activity"
                : "Active Window: none";

        _ = draggable.AddHandler(
            Events.Pointer,
            (_, eventArgs) =>
            {
                if (eventArgs is { Phase: Phase.Bubble, Pointer.Action: PointerAction.Press })
                {
                    ReportActivation();
                }
            },
            handledEventsToo: true);
        _ = activityWindow.AddHandler(
            Events.Pointer,
            (_, eventArgs) =>
            {
                if (eventArgs is { Phase: Phase.Bubble, Pointer.Action: PointerAction.Press })
                {
                    ReportActivation();
                }
            },
            handledEventsToo: true);
        Place(draggable, 22, 3);
        Place(activityWindow, 54, 12);
        Place(reopenWindow, 2, 2);
        Place(windowStatus, 2, 6);
        Place(activationStatus, 2, 8);

        var dragSurface = new Text(
            "Dashboard  Activity  Settings\n\n\n\n\n\n\n\n\n\n\n" +
            "Recent projects\n" +
            "  sharp-vision now\n" +
            "  terminal-lab 8m\n\n" +
            "Drag title bars.")
        { IsHitTestVisible = false };
        var dragStage = new Overlay
        {
            Width = Length.Cells(82),
            Height = Length.Cells(22),
            ClipToBounds = true,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { dragSurface, reopenWindow, windowStatus, activationStatus, draggable, activityWindow }
        };

        // Modal dialog with Ignore outside interaction and default/cancel role buttons.
        var dialog = new Window
        {
            CanMove = false,
            CanClose = true,
            CloseOnEscape = true,
            HeaderPlacement = WindowTitlePlacement.Center,
            UseMnemonic = false,
            Width = Length.Cells(40),
            Height = Length.Auto,
            Header = "Confirm deployment",
            Visibility = Visibility.Collapsed
        };
        var dialogStatus = new Text("Dialog: closed");
        var workspaceStatus = new Text("Workspace action: untouched");
        var workspaceAction = new Button { Content = new Text("Wor&kspace action") };
        var closeButton = new Button
        {
            Content = new Text("Ca&ncel"),
            IsCancel = true
        };
        var okButton = new Button
        {
            Content = new Text("&Deploy"),
            IsDefault = true
        };
        workspaceAction.Click += (_, _) => workspaceStatus.Content = "Workspace action: activated";

        void CloseDialog(string status)
        {
            dialog.Visibility = Visibility.Collapsed;
            dialogStatus.Content = status;
        }

        closeButton.Click += (_, _) => CloseDialog("Dialog: cancelled; focus restored");
        okButton.Click += (_, _) => CloseDialog("Dialog: confirmed; focus restored");
        dialog.Closing += (_, _) => CloseDialog("Dialog: frame close; focus restored");
        dialog.Content = new DocColumn(
            new Text("Proceed with deployment?") { Overflow = Overflow.Wrap },
            new DocRow(okButton, closeButton));

        var openButton = new Button { Content = new Text("&Open dialog") };
        openButton.Click += (_, _) =>
        {
            // Ignore blocks background activation while the modal scope owns focus.
            _ = dialog.ShowModal(OutsideInteraction.Ignore, okButton);
            dialogStatus.Content = "Dialog: modal (Ignore); focus confined";
        };
        Place(dialog, 14, 2);
        Place(openButton, 2, 2);
        Place(dialogStatus, 2, 5);
        Place(workspaceAction, 2, 7);
        Place(workspaceStatus, 20, 7);

        var dialogSurface = Workspace("Application workspace\n\nReady");
        dialogSurface.Width = Length.Cells(62);
        dialogSurface.Height = Length.Cells(12);
        var dialogStage = new Overlay
        {
            Width = Length.Cells(62),
            Height = Length.Cells(12),
            ClipToBounds = true,
            Children =
            {
                dialogSurface,
                openButton,
                dialogStatus,
                workspaceAction,
                workspaceStatus,
                dialog
            }
        };

        // Border-family catalog with title and close placement variants.
        var light = FrameVariant(
            "Light",
            BorderGlyphStyle.Light,
            WindowTitlePlacement.Left,
            WindowClosePlacement.Left);
        var rounded = FrameVariant(
            "Rounded",
            BorderGlyphStyle.Rounded,
            WindowTitlePlacement.Center,
            WindowClosePlacement.Right);
        var heavy = FrameVariant(
            "Heavy",
            BorderGlyphStyle.Heavy,
            WindowTitlePlacement.Right,
            WindowClosePlacement.Left);
        var paired = FrameVariant(
            "Paired",
            BorderGlyphStyle.Paired,
            WindowTitlePlacement.Center,
            WindowClosePlacement.Right);
        var ascii = FrameVariant(
            "ASCII",
            BorderGlyphStyle.Ascii,
            WindowTitlePlacement.Right,
            WindowClosePlacement.Left);
        var chromeCatalog = new DocColumn(
            new DocRow(
                FrameStage(light, 22, 9),
                FrameStage(rounded, 22, 9),
                FrameStage(heavy, 22, 9)),
            new DocRow(
                FrameStage(paired, 22, 9),
                FrameStage(ascii, 22, 9)));

        // Shadow depth row: composite, block glyph, and flat elevation.
        var composite = FrameVariant(
            "Composite",
            BorderGlyphStyle.Rounded,
            WindowTitlePlacement.Left,
            WindowClosePlacement.Left);
        var block = FrameVariant(
            "Block",
            BorderGlyphStyle.Heavy,
            WindowTitlePlacement.Center,
            WindowClosePlacement.Right);
        block.Shadow = WindowShadow(ShadowMode.BlockGlyph, new Point(1, 1), new Rune('░'));
        var flat = FrameVariant(
            "Flat",
            BorderGlyphStyle.Ascii,
            WindowTitlePlacement.Right,
            WindowClosePlacement.Left);
        flat.Shadow = HiddenShadow();
        var shadowRow = new DocRow(
            FrameStage(composite, 22, 9),
            FrameStage(block, 22, 9),
            FrameStage(flat, 22, 9));

        return new DocPage(
            Title,
            "<info>Window</info> provides explicit normal and dialog roles, compact bracketed title-bar chrome, movement, and modal presentation.",
            new DocSection(
                "🪟",
                "Normal and dialog roles",
                "Window chrome and interaction defaults are ordinary mutable properties; modality remains explicit.",
                new DocExample(
                    "Role defaults",
                    "Normal is movable. Dialog is fixed, closable, and centered. Both use the unmistakable paired frame by default.",
                    roleRow,
                    "var normal = new Window();\nvar dialog = new Window { CanMove = false, CanClose = true, CloseOnEscape = true };")),
            new DocSection(
                "↔",
                "Move, close, and reopen",
                "Click either modeless Window to switch the Application's active Window. Drag the settings Window by unoccupied title chrome; its close target responds on pointer-up.",
                new DocExample(
                    "Active modeless Windows",
                    "Click either title to switch the active border without stealing focus, then drag, close, and reopen the retained settings Window.",
                    dragStage,
                    "Window? active = application.ActiveWindow;\nbool isThisWindowActive = window.IsActive;\nwindow.Closing += (_, _) => window.Visibility = Visibility.Collapsed;")),
            new DocSection(
                "◈",
                "Modal dialog",
                "<info>ShowModal</info> owns an <info>Ignore</info> plane. Tab stays inside, Enter invokes Deploy, Escape invokes Cancel, and frame close restores the opener.",
                new DocExample(
                    "Focus confinement and restoration",
                    "Open the dialog, try the background action, cycle focus, then use Enter, Escape, or the title-bar close control.",
                    dialogStage,
                    "var dialog = new Window { CanMove = false, CanClose = true, CloseOnEscape = true };\nvar scope = dialog.ShowModal(OutsideInteraction.Ignore, deployButton);")),
            new DocSection(
                "╬",
                "Border and title chrome",
                "Every line family embeds the quiet [■] close target directly in its top edge. Hover and press recolor only the mark, never the frame or Window background.",
                new DocExample(
                    "Five border families",
                    "Compare both close edges and Left, Center, and Right title placement without collisions.",
                    chromeCatalog,
                    "window.BorderGlyphStyle = BorderGlyphStyle.Paired;\nwindow.ClosePlacement = WindowClosePlacement.Right;\nwindow.HeaderPlacement = WindowTitlePlacement.Center;")),
            new DocSection(
                "◩",
                "Shadow depth",
                "Composite darkening is subtle, block glyphs are deliberately retro, and a flat Window removes elevation entirely.",
                new DocExample(
                    "Composite, block, and flat",
                    "The frame remains authoritative even when content and shadows overlap its cells.",
                    shadowRow)));
    }

    private static void Place(Control control, int left, int top) =>
        ShowcasePaneHelpers.Place(control, left, top);

    private static DocColumn RoleDescription(string primary, string secondary)
    {
        var content = new DocColumn(
            new Text(primary) { TextAlignment = Alignment.Center, Overflow = Overflow.Wrap },
            new Text(secondary)
            {
                TextAlignment = Alignment.Center,
                Overflow = Overflow.Wrap
            })
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        return content;
    }

    private static DocColumn CreateSettingsForm()
    {
        var apply = new Button { Content = new Text("Appl&y"), IsDefault = true };
        var cancel = new Button { Content = new Text("&Cancel"), IsCancel = true };
        var actions = new DocRow(apply, cancel)
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var status = new Text("Action: waiting");
        apply.Click += (_, eventArgs) => status.Content = $"Action: Apply ({eventArgs.Cause})";
        cancel.Click += (_, eventArgs) => status.Content = $"Action: Cancel ({eventArgs.Cause})";

        return new DocColumn(
            new Text("Choose project options."),
            new CheckBox { Content = new Text("&Restore last session"), IsChecked = true },
            new CheckBox { Content = new Text("Start in saf&e mode") },
            actions,
            status);
    }

    private static Window FrameVariant(
        string title,
        BorderGlyphStyle glyphs,
        WindowTitlePlacement titlePlacement,
        WindowClosePlacement closePlacement) => new()
        {
            UseMnemonic = false,
            Width = Length.Cells(18),
            Height = Length.Cells(6),
            Header = title,
            HeaderPlacement = titlePlacement,
            CanClose = true,
            ClosePlacement = closePlacement,
            Border = new Border(
                BorderSide.All,
                glyphs,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Shadow = WindowShadow(ShadowMode.Composite, new Point(1, 1)),
            Content = new Text("Preview")
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

    private static Shadow HiddenShadow() => new(
        false,
        ShadowMode.Composite,
        default,
        default,
        ThemeColor.ControlShadow,
        Color.Transparent,
        ThemeDecoration.Shadow);

    private static Shadow WindowShadow(ShadowMode mode, Point offset, Rune glyph = default) => new(
        true,
        mode,
        offset,
        glyph,
        ThemeColor.ControlShadow,
        Color.Transparent,
        ThemeDecoration.Shadow);

    private static Dock Workspace(string content) => new()
    {
        Border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Light,
            ThemeColor.ControlBorder,
            Color.Transparent,
            ThemeDecoration.Border),
        Padding = new Thickness(1, 0),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        Children = { new Text(content) }
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
            Children = { Workspace("Workspace\n\nReady"), window }
        };
    }
}
