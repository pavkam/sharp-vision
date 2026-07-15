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
        Height = Length.Cells(4);
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
    protected override Size MeasureOverride(Constraint constraint) => new(26, 4);

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        RenderChrome(canvas);

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;
        _ = canvas.Draw(Caption.AsSpan(), ResolveCaptionPoint(Caption), style);
        _ = canvas.Draw("Themed body".AsSpan(), ResolveBodyPoint(Caption), style);
    }

    private Point ResolveCaptionPoint(string caption)
    {
        if (LabelPlacement == LabelPlacement.Right)
        {
            return new Point(Bounds.Right - caption.Length - 1, Bounds.Y + 1);
        }
        else if (LabelPlacement == LabelPlacement.Above)
        {
            return new Point(Bounds.X + 1, Bounds.Y);
        }
        else if (LabelPlacement == LabelPlacement.Below)
        {
            return new Point(Bounds.X + 1, Bounds.Bottom - 1);
        }

        return new Point(Bounds.X + 1, Bounds.Y + 1);
    }

    private Point ResolveBodyPoint(string caption)
    {
        if (LabelPlacement == LabelPlacement.Right)
        {
            return new Point(Bounds.X + 1, Bounds.Y + 1);
        }
        else if (LabelPlacement == LabelPlacement.Above)
        {
            return new Point(Bounds.X + 1, Bounds.Y + 1);
        }
        else if (LabelPlacement == LabelPlacement.Below)
        {
            return new Point(Bounds.X + 1, Bounds.Y + Math.Max(0, Bounds.Height - 2));
        }

        return new Point(Bounds.X + caption.Length + 2, Bounds.Y + 1);
    }
}
