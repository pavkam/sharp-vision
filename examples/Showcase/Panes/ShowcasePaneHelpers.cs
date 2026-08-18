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
    internal static Dock Card(
        string label,
        BorderGlyphStyle glyphs,
        Thickness? padding = null,
        Overflow overflow = Overflow.Visible) => new()
        {
            Border = new Border(
                BorderSide.All,
                glyphs,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Padding = padding ?? default,
            Children = { new Text(label) { Overflow = overflow } }
        };

    /// <summary>Wraps a child in a light bordered frame for overlay placement specimens.</summary>
    internal static Dock Frame(ControlBase child) => Frame(child, BorderGlyphStyle.Light);

    /// <summary>Wraps a child in a bordered frame for overlay placement specimens.</summary>
    internal static Dock Frame(ControlBase child, BorderGlyphStyle glyphs) => new()
    {
        Border = new Border(
            BorderSide.All,
            glyphs,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border),
        Children = { child }
    };

    /// <summary>The shared dim-caption face used to label paired specimens.</summary>
    internal static readonly Face DimCaptionFace = new(
        SemanticColor.ControlText,
        Color.Transparent,
        TerminalAttributes.Dim,
        Underline.None,
        Color.Default);

    /// <summary>Builds a dim-captioned label used alongside a paired specimen control.</summary>
    internal static Text DimCaption(string text) => new(text) { Face = DimCaptionFace };

    /// <summary>Builds a dim-captioned label of fixed width used alongside a paired specimen control.</summary>
    internal static Text DimCaption(string text, Length width) => new(text) { Face = DimCaptionFace, Width = width };

    /// <summary>Creates a fixed-size overlay stage for placement specimens.</summary>
    internal static Overlay OverlayStage(int width, int height, bool clipToBounds = true) => new()
    {
        Width = Length.Cells(width),
        Height = Length.Cells(height),
        ClipToBounds = clipToBounds
    };

    /// <summary>Assigns cell offsets on an overlay-hosted control.</summary>
    internal static void Place(ControlBase control, int left, int top)
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
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border),
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
        params ControlBase[] controls)
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
        params ControlBase[] controls)
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

    /// <summary>Posts a status update through the status control's dispatcher after an async modal dialog completes.</summary>
    internal static void PostStatus(Text status, string dialogName, Action update)
    {
        var dispatcher = status.Dispatcher ?? throw new InvalidOperationException(
            $"The showcase status must remain attached while the {dialogName} is open.");
        dispatcher.Post(update);
    }

    private static void SetLabelPlacement(ShowcasePanel[] targets, LabelPlacement placement)
    {
        foreach (var target in targets)
        {
            target.LabelPlacement = placement;
        }
    }
}
