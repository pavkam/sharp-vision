// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Demonstrates the layout, participation, input, and box-model options shared by every control.</summary>
internal sealed class ControlPane: CompositeControl
{
    /// <summary>Initializes the retained Control concept page.</summary>
    internal ControlPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Control";

    private static DocPage CreateContent()
    {
        var percentSample = CreateLengthSample("Percent(50) · half the bounded slot", Length.Percent(50));
        percentSample.HorizontalAlignment = HorizontalAlignment.Stretch;
        var starSample = CreateLengthSample("Star(1) · remaining bounded space", Length.Star(1));
        starSample.HorizontalAlignment = HorizontalAlignment.Stretch;
        var constrainedSample = CreateConstrainedSample();
        constrainedSample.HorizontalAlignment = HorizontalAlignment.Stretch;

        var lengths = new DocColumn(
            CreateLengthSample("Auto · intrinsic content", Length.Auto),
            CreateLengthSample("Cells(18) · fixed terminal cells", Length.Cells(18)),
            percentSample,
            starSample,
            constrainedSample);

        var horizontalAlignments = new DocColumn(
            CreateHorizontalAlignmentSample(HorizontalAlignment.Left),
            CreateHorizontalAlignmentSample(HorizontalAlignment.Center),
            CreateHorizontalAlignmentSample(HorizontalAlignment.Right),
            CreateHorizontalAlignmentSample(HorizontalAlignment.Stretch));
        var verticalAlignments = new DocRow(
            CreateVerticalAlignmentSample(VerticalAlignment.Top),
            CreateVerticalAlignmentSample(VerticalAlignment.Center),
            CreateVerticalAlignmentSample(VerticalAlignment.Bottom),
            CreateVerticalAlignmentSample(VerticalAlignment.Stretch));

        var boxModel = new Dock
        {
            Width = Length.Cells(48),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Heavy,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(2, 1),
            Children =
            {
                new Dock
                {
                    Margin = new Thickness(2, 1),
                    Border = new Border(
                        BorderSide.All,
                        BorderGlyphStyle.Rounded,
                        ThemeColor.ControlBorder,
                        Color.Transparent,
                        ThemeDecoration.Border),
                    Padding = new Thickness(2, 0),
                    Children = { new Text("content inside padding; margin remains outside") }
                }
            }
        };

        var visible = CreateParticipationButton("Visible", Visibility.Visible, isEnabled: true);
        var hidden = CreateParticipationButton("Hidden · keeps its slot", Visibility.Hidden, isEnabled: true);
        var collapsed = CreateParticipationButton("Collapsed · removes its slot", Visibility.Collapsed, isEnabled: true);
        var disabled = CreateParticipationButton("Disabled", Visibility.Visible, isEnabled: false);
        var noHitTest = CreateParticipationButton("Pointer passes through", Visibility.Visible, isEnabled: true);
        noHitTest.IsHitTestVisible = false;
        var noFocus = CreateParticipationButton("Skipped by keyboard focus", Visibility.Visible, isEnabled: true);
        noFocus.Focusable = false;

        return new DocPage(
            Title,
            "<info>Control</info> is the mutable retained foundation: every component shares its cell sizing, alignment, box model, participation, focus, and pointer policy.",
            new DocSection(
                "📐",
                "Length and constraints",
                "<info>Width</info> and <info>Height</info> accept automatic, fixed-cell, percentage, and star lengths; minimum and maximum bounds clamp the resolved border box.",
                new DocExample(
                    "Every length kind",
                    "Resize the showcase to watch intrinsic, fixed, percentage, and remaining-space requests resolve independently.",
                    lengths,
                    "control.Width = Length.Percent(50);\ncontrol.MinWidth = 18;\ncontrol.MaxWidth = 30;")),
            new DocSection(
                "↔️",
                "Alignment",
                "Horizontal and vertical alignment place an intrinsic control at the leading edge, center, trailing edge, or stretch it through the available slot.",
                new DocExample(
                    "Horizontal alignment",
                    "The bordered specimen moves inside the same 42-cell host.",
                    horizontalAlignments),
                new DocExample(
                    "Vertical alignment",
                    "Top, center, bottom, and stretch use the same five-row host height.",
                    verticalAlignments)),
            new DocSection(
                "📦",
                "Box model",
                "<info>Margin</info> separates siblings, border defines the border box, and <info>Padding</info> reserves cells between chrome and content.",
                new DocExample(
                    "Margin, border, padding, content",
                    "The heavy outer frame exposes the margin around the rounded inner control; the inner frame exposes its padding.",
                    boxModel)),
            new DocSection(
                "🚦",
                "Participation and input",
                "Visible, hidden, and collapsed control layout differ deliberately. Enabled, hit-test-visible, and focusable are independent input decisions.",
                new DocExample(
                    "Visibility",
                    "Hidden retains blank layout; collapsed contributes no layout. Both stop rendering and input.",
                    new DocColumn(
                        new Text("Visible specimen:"),
                        visible,
                        new Text("Hidden specimen (blank row below):"),
                        hidden,
                        new Text("Collapsed specimen (no row below):"),
                        collapsed)),
                new DocExample(
                    "Independent input gates",
                    "Disabled blocks all interaction; hit testing controls pointer targeting; focusable controls keyboard navigation.",
                    new DocColumn(disabled, noHitTest, noFocus))));
    }

    private static DocColumn CreateLengthSample(string caption, Length width)
    {
        var shortLabel = caption.Split('·')[0].Trim();
        var sample = new Dock
        {
            Width = width,
            Height = Length.Cells(3),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { new Text(shortLabel) }
        };
        return new DocColumn(new Text(caption), sample);
    }

    private static DocColumn CreateConstrainedSample()
    {
        var sample = new Dock
        {
            Width = Length.Percent(80),
            MinWidth = 18,
            MaxWidth = 30,
            Height = Length.Cells(3),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Paired,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { new Text("Percent(80), clamped 18…30") }
        };
        return new DocColumn(new Text("MinWidth and MaxWidth"), sample);
    }

    private static Overlay CreateHorizontalAlignmentSample(HorizontalAlignment alignment)
    {
        var sample = new Dock
        {
            Width = alignment == HorizontalAlignment.Stretch ? Length.Auto : Length.Cells(14),
            Height = Length.Cells(3),
            HorizontalAlignment = alignment,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { new Text(alignment.ToString()) }
        };
        return new Overlay
        {
            Width = Length.Cells(42),
            Height = Length.Cells(5),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Children = { sample }
        };
    }

    private static Overlay CreateVerticalAlignmentSample(VerticalAlignment alignment)
    {
        var sample = new Dock
        {
            Width = Length.Cells(10),
            Height = alignment == VerticalAlignment.Stretch ? Length.Auto : Length.Cells(1),
            VerticalAlignment = alignment,
            Border = new Border(
                BorderSide.Left | BorderSide.Right,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Children = { new Text(alignment.ToString()) }
        };
        return new Overlay
        {
            Width = Length.Cells(12),
            Height = Length.Cells(5),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Children = { sample }
        };
    }

    private static Button CreateParticipationButton(string caption, Visibility visibility, bool isEnabled) => new()
    {
        Content = new Text(caption),
        Visibility = visibility,
        IsEnabled = isEnabled,
        UseMnemonic = false
    };
}
