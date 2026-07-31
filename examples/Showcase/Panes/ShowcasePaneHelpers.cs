// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>
/// Shared layout and wiring helpers for showcase pane specimens.
/// </summary>
internal static class ShowcasePaneHelpers
{
    /// <summary>Builds a bordered dock card used to label layout regions.</summary>
    internal static Dock Card(string label, BorderGlyphStyle glyphs, Thickness? padding = null) => new()
    {
        Border = new Border(
            BorderSide.All,
            glyphs,
            ThemeColor.ControlBorder,
            Color.Transparent,
            ThemeDecoration.Border),
        Padding = padding ?? default,
        Children = { new Text(label) }
    };

    /// <summary>Wraps a child in a light bordered frame for overlay placement specimens.</summary>
    internal static Dock Frame(Control child) => Frame(child, BorderGlyphStyle.Light);

    /// <summary>Wraps a child in a bordered frame for overlay placement specimens.</summary>
    internal static Dock Frame(Control child, BorderGlyphStyle glyphs) => new()
    {
        Border = new Border(
            BorderSide.All,
            glyphs,
            ThemeColor.ControlBorder,
            Color.Transparent,
            ThemeDecoration.Border),
        Children = { child }
    };

    /// <summary>Creates a fixed-size overlay stage for placement specimens.</summary>
    internal static Overlay OverlayStage(int width, int height, bool clipToBounds = true) => new()
    {
        Width = Length.Cells(width),
        Height = Length.Cells(height),
        ClipToBounds = clipToBounds
    };

    /// <summary>Assigns cell offsets on an overlay-hosted control.</summary>
    internal static void Place(Control control, int left, int top)
    {
        Overlay.SetLeft(control, Length.Cells(left));
        Overlay.SetTop(control, Length.Cells(top));
    }

    /// <summary>
    /// Creates a drop-down stage whose host disables clipping so popup lists can extend outside the box.
    /// </summary>
    internal static Overlay ComboStage(int width, int height, ComboBox combo)
    {
        var stage = OverlayStage(width, height, clipToBounds: false);
        // An unpositioned overlay child stretches to the stage, which would
        // inflate the field's border over the room reserved for the popup.
        combo.VerticalAlignment = VerticalAlignment.Top;
        stage.Children.Add(combo);
        return stage;
    }

    /// <summary>Reflects the committed combo selection in a status line.</summary>
    internal static void WireComboSelectionStatus(
        ComboBox combo,
        Text status,
        string prefix,
        bool trailingPeriod = true)
    {
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
            {
                var item = combo.Items[combo.SelectedIndex];
                status.Content = trailingPeriod
                    ? $"{prefix}: {item}."
                    : $"{prefix}: {item}";
            }
            else
            {
                status.Content = "No selection.";
            }
        };
    }

    /// <summary>Routes the four label-placement buttons to every supplied preview panel.</summary>
    internal static void WireLabelPlacement(
        Button left,
        Button right,
        Button above,
        Button below,
        params ShowcasePanel[] targets)
    {
        left.Click += (_, _) => SetLabelPlacement(targets, LabelPlacement.Left);
        right.Click += (_, _) => SetLabelPlacement(targets, LabelPlacement.Right);
        above.Click += (_, _) => SetLabelPlacement(targets, LabelPlacement.Above);
        below.Click += (_, _) => SetLabelPlacement(targets, LabelPlacement.Below);
    }

    /// <summary>Builds the dim workspace surface behind popup interaction specimens.</summary>
    internal static Dock ApplicationSurface(string content) => new()
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

    /// <summary>
    /// Composes a clipped overlay stage with a workspace surface, positioned controls, and one popup.
    /// </summary>
    internal static Overlay ApplicationStage(
        int width,
        int height,
        string content,
        Popup popup,
        params Control[] controls)
    {
        var interactions = new Overlay { ClipToBounds = false };
        foreach (var control in controls)
        {
            interactions.Children.Add(control);
        }

        return new Overlay
        {
            Width = Length.Cells(width),
            Height = Length.Cells(height),
            ClipToBounds = true,
            Children = { ApplicationSurface(content), interactions, popup }
        };
    }

    /// <summary>
    /// Composes a clipped overlay stage with a workspace surface, positioned controls, and one flyout.
    /// </summary>
    internal static Overlay ApplicationStage(
        int width,
        int height,
        string content,
        Flyout flyout,
        params Control[] controls)
    {
        var interactions = new Overlay { ClipToBounds = false };
        foreach (var control in controls)
        {
            interactions.Children.Add(control);
        }

        interactions.Children.Add(flyout);

        return new Overlay
        {
            Width = Length.Cells(width),
            Height = Length.Cells(height),
            ClipToBounds = true,
            Children = { ApplicationSurface(content), interactions }
        };
    }

    private static void SetLabelPlacement(ShowcasePanel[] targets, LabelPlacement placement)
    {
        foreach (var target in targets)
        {
            target.LabelPlacement = placement;
        }
    }
}
