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
            actions);

        var window = new Window()
        {
            Width = Length.Cells(42),
            Height = Length.Auto,
            Title = "Project settings",
            HasShadow = true,
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowOffset = new Point(2, 1),
            Content = form,
        };

        var stage = new Canvas()
        {
            Width = Length.Cells(48),
            Height = Length.Cells(13),
            ClipToBounds = true,
        };
        Canvas.SetLeft(window, Length.Cells(1));
        Canvas.SetTop(window, Length.Cells(1));
        stage.Children.Add(window);

        return Doc.Page(
            Title,
            "Frames one owned content control as a titled terminal application surface with optional Turbo Vision-style shadowing.",
            Doc.Example(
                "Border and title options",
                "Each Window uses the same content contract with a different Glyphs family and title placement: rounded left, paired center, and portable ASCII right.",
                chromeOptions),
            Doc.Example(
                "Titled application surface",
                "A Window owns its interior while title chrome, frame, and shadow render as one terminal-safe surface. Try Enter for Apply or Escape for Cancel.",
                new Dock
                {
                    BorderThickness = new Thickness(1),
                    BorderGlyphs = Glyphs.Light,
                    Children = { stage },
                }));
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
}
