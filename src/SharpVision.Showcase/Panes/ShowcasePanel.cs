// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;



/// <summary>
/// Demonstrates third-party control styling with a custom style property outside the core theme catalog.
/// </summary>
public sealed class ShowcasePanel: Control
{
    /// <summary>Registers the label-placement style property.</summary>
    public static StyleProperty<LabelPlacement> LabelPlacementProperty { get; } =
        StyleProperty<LabelPlacement>.Register<ShowcasePanel>(
            "label-placement",
            LabelPlacement.Left,
            ChangeImpact.Measure);

    /// <summary>Initializes a compact themed panel specimen.</summary>
    public ShowcasePanel()
    {
        Width = Length.Cells(26);
        Height = Length.Cells(6);
        BorderThickness = new Thickness(1);
        BorderGlyphs = Glyphs.Rounded;
        Padding = new Thickness(1);
        FillMode = FillMode.Opaque;
        Caption = "Showcase panel";
    }

    /// <summary>Gets or sets the caption placement resolved through the theme cascade.</summary>
    public LabelPlacement LabelPlacement
    {
        get => GetValue(LabelPlacementProperty);
        set => SetValue(LabelPlacementProperty, value);
    }

    /// <summary>Gets or sets the readable caption drawn according to <see cref="LabelPlacement"/>.</summary>
    /// <exception cref="ArgumentNullException">The assigned caption is null.</exception>
    public string Caption
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = string.Empty;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => new(26, 6);

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        RenderChrome(canvas);
        var content = ContentBounds;

        if (content.Width == 0 || content.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;
        var clipped = canvas.Clip(content);
        _ = clipped.Draw(Caption.AsSpan(), ResolveCaptionPoint(content, Caption), style);
        _ = clipped.Draw("Themed body".AsSpan(), ResolveBodyPoint(content, Caption), style);
    }

    private Point ResolveCaptionPoint(Rect content, string caption)
    {
        if (LabelPlacement == LabelPlacement.Right)
        {
            return new Point(content.Right - caption.Length, content.Y);
        }
        else if (LabelPlacement == LabelPlacement.Above)
        {
            return new Point(content.X, content.Y);
        }
        else if (LabelPlacement == LabelPlacement.Below)
        {
            return new Point(content.X, content.Bottom - 1);
        }

        return new Point(content.X, content.Y);
    }

    private Point ResolveBodyPoint(Rect content, string _)
    {
        if (LabelPlacement == LabelPlacement.Right)
        {
            return new Point(content.X, content.Y + 1);
        }
        else if (LabelPlacement == LabelPlacement.Above)
        {
            return new Point(content.X, content.Y + 1);
        }
        else if (LabelPlacement == LabelPlacement.Below)
        {
            return new Point(content.X, Math.Max(content.Y, content.Bottom - 2));
        }

        return new Point(content.X, content.Y + 1);
    }
}
