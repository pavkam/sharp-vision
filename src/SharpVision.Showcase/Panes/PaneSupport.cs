using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Text;

namespace SharpVision.Showcase.Panes;

/// <summary>Shared layout and specimen helpers used across showcase panes.</summary>
internal static class PaneSupport
{
    internal static ControlStack Vertical() => new() { Spacing = 1 };

    internal static ControlStack Horizontal() => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 2,
    };

    internal static void AddAttributeLine(
        ControlRichText document,
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

    internal static ControlBorder Card(Control child, Glyphs glyphs) => new()
    {
        Child = child,
        BorderThickness = new Thickness(1),
        Glyphs = glyphs,
        Padding = new Thickness(1, 0),
    };

    internal static ControlButton ButtonSpecimen(ControlButton button)
    {
        ArgumentNullException.ThrowIfNull(button);
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Margin = new Thickness(0, 0, 1, 1);
        return button;
    }

    internal static ControlBorder DemoCard(string content, Glyphs glyphs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new ControlBorder
        {
            Child = new ControlText(content),
            BorderThickness = new Thickness(1),
            Glyphs = glyphs,
            BorderColor = Palette.Accent,
            Background = Palette.Surface,
            Padding = new Thickness(1, 0),
        };
    }

    internal static ControlCanvas CanvasStage() => new()
    {
        Width = Length.Cells(36),
        Height = Length.Cells(7),
        ClipToBounds = true,
    };

    internal static ControlStack CanvasSection(string heading, string description, Control sample)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(sample);
        var section = Vertical();
        var text = new ControlRichText { Wrapping = Wrapping.Word };
        text.Inlines.Add(new ControlRun(heading)
        {
            Foreground = Palette.Warning,
            Attributes = Attributes.Bold,
        });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new ControlRun(description) { Foreground = Palette.Muted });
        section.Children.Add(text);
        section.Children.Add(new ControlBorder
        {
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Light,
            BorderColor = Palette.Border,
            Background = Palette.Panel,
            Child = sample,
        });
        return section;
    }

    internal static ControlStack SampleSection(string heading, string description, Control sample)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(sample);
        var section = Vertical();
        var text = new ControlRichText { Wrapping = Wrapping.Word };
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

    internal static ControlBorder ShadowStage(ControlShadow shadow)
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
        return new ControlBorder
        {
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Light,
            BorderColor = Palette.Border,
            Background = Palette.Panel,
            Child = stage,
        };
    }

    internal static void AddBorder(ControlStack examples, string name, Glyphs glyphs) =>
        examples.Children.Add(Card(new ControlText(name), glyphs));

    internal static void AddGrid(ControlGrid grid, string text, int row, int column)
    {
        var child = Card(new ControlText(text), Glyphs.Light);
        ControlGrid.SetRow(child, row);
        ControlGrid.SetColumn(child, column);
        grid.Children.Add(child);
    }
}
