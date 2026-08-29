// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>
/// Demonstrates third-party control styling with a custom style property outside the core theme catalog.
/// </summary>
public sealed class ShowcasePanel: ControlBase
{
    private const string _bodyText = "Themed body";

    /// <summary>Initializes a compact themed panel specimen.</summary>
    public ShowcasePanel()
    {
        EnableChromeAuthoring();
        Width = Length.Cells(26);
        Height = Length.Cells(6);
        Border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Rounded,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border);
        Padding = new Thickness(1);
        Caption = "Showcase panel";
    }

    /// <summary>Gets or sets the caption placement.</summary>
    public LabelPlacement LabelPlacement
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets the readable caption drawn according to <see cref="LabelPlacement"/>.</summary>
    /// <exception cref="ArgumentNullException">The assigned caption is null.</exception>
    public string Caption
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => new(26, 6);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var content = ContentBounds;

        if (content.Width == 0 || content.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;
        var clipped = canvas.Clip(content);
        _ = clipped.Draw(Caption.AsSpan(), ResolveCaptionPoint(content, Caption), style);
        _ = clipped.Draw(_bodyText.AsSpan(), ResolveBodyPoint(content, _bodyText), style);
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
        else if (LabelPlacement == LabelPlacement.Left)
        {
            return new Point(content.X, content.Y);
        }

        return new Point(content.X, content.Y);
    }

    private Point ResolveBodyPoint(Rect content, string body)
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
        else if (LabelPlacement == LabelPlacement.Left)
        {
            return new Point(content.Right - body.Length, content.Y + 1);
        }

        return new Point(content.X, content.Y + 1);
    }
}
