// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using DisplayText = Display.Text;

/// <summary>Frames one owned content control with a titled border.</summary>
/// <remarks>
/// The header is arranged into the top border edge but, because it shares that row with the frame
/// glyphs, is painted from <see cref="RenderOverlay"/> after the border rather than through the
/// ordinary descendant render pass; <see cref="RenderChildren"/> excludes it accordingly. A plain
/// <see cref="DisplayText"/> header (the common case, materialized by
/// <see cref="HeaderedContentControl.HeaderText"/>) paints with the frame's own theme-owned style,
/// so the title always reads as part of the border regardless of a locally assigned
/// <see cref="ControlBase.Face"/>; any other header control paints with its own resolved style.
/// </remarks>
[PublicAPI]
public sealed class GroupBox: HeaderedContentControl
{
    /// <summary>Initializes an empty group box.</summary>
    public GroupBox() => EnableChromeAuthoring();

    // Enabled here rather than on HeaderedContentControl, for the same reason Container-derived
    // panels enable it individually instead of on Container: the other headered types are
    // composite parts that own their own presentation and must not expose raw chrome authoring.
    // TabItem is a part of TabControl, and Expander paints no frame at all - only GroupBox is a
    // framed control whose entire purpose is a caller-authored titled border.

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(ContainerStyle.Default).ToAppearanceStates();

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var child = Content;
        var headerContentWidth = 0;

        if (Header is { } header)
        {
            var hd = MeasureChild(header, new Constraint(null, 1));
            headerContentWidth = header.Visibility == Visibility.Collapsed
                ? 0
                : (int) Math.Min(int.MaxValue, (long) hd.Width + header.Margin.Horizontal);
        }

        var hw = Header is null
            ? 0
            : (int) Math.Min(int.MaxValue, 2L + headerContentWidth);
        if (child is null)
        {
            return new Size(hw, 0);
        }

        var desired = MeasureChild(child, constraint);
        var contentWidth = child.Visibility == Visibility.Collapsed
            ? 0
            : (int) Math.Min(int.MaxValue, (long) desired.Width + child.Margin.Horizontal);
        var contentHeight = child.Visibility == Visibility.Collapsed
            ? 0
            : (int) Math.Min(int.MaxValue, (long) desired.Height + child.Margin.Vertical);
        return new Size(Math.Max(contentWidth, hw), contentHeight);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is { } c)
        {
            ArrangeChild(c, bounds, ResolvedAxes.Both);
        }

        if (Header is { } header)
        {
            var titleRect = Bounds.Width > 3
                ? new Rect(Bounds.X + 2, Bounds.Y, Bounds.Width - 3, 1)
                : default;
            ArrangeChild(header, titleRect, ResolvedAxes.Height);
        }
    }

    /// <inheritdoc/>
    protected override ChromeRenderOptions GetChromeRenderOptions() => new() { SkipBodyFill = true, SkipBorder = true };

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var opaque = this.HasOpaqueFill(GetAppearanceState());
        if (opaque)
        {
            canvas.Clear(Bounds, ResolvedStyle);
        }
    }

    /// <summary>Renders only <see cref="ContentControl.Content"/> through the ordinary descendant
    /// pass; the header renders from <see cref="RenderOverlay"/> instead, after the border.</summary>
    internal override void RenderChildren(TerminalCanvas canvas, Rect contentClip) =>
        Content?.Render(canvas, contentClip);

    /// <inheritdoc/>
    internal override void RenderOverlay(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var opaque = this.HasOpaqueFill(GetAppearanceState());
        var border = this.ResolveBorderStyle(GetAppearanceState());
        var bg = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        var actualBorder = ActualBorder;
        canvas.DrawPartialBorder(
            Bounds,
            actualBorder.Sides,
            ResolveBorderGlyphs(actualBorder.GlyphStyle),
            border,
            bg);
        if (Header is { } header && Bounds.Width > 3)
        {
            var title = canvas.Clip(new Rect(Bounds.X + 1, Bounds.Y, Bounds.Width - 2, 1));
            var start = title.Draw(
                " ".AsSpan(),
                new Point(Bounds.X + 1, Bounds.Y),
                border,
                background: bg);

            if (header is DisplayText text)
            {
                var cells = text.Content.Draw(
                    title,
                    start.Final,
                    border,
                    bg,
                    CellPolicy.AmbiguousWidth,
                    UseMnemonic,
                    EffectiveIsEnabled ? Theme?.Hotkey ?? Color.Default : null);
                _ = title.Draw(
                    " ".AsSpan(),
                    new Point(start.Final.X + cells, start.Final.Y),
                    border,
                    background: bg);
            }
            else
            {
                header.Render(canvas);
                _ = title.Draw(
                    " ".AsSpan(),
                    new Point(start.Final.X + header.Bounds.Width, start.Final.Y),
                    border,
                    background: bg);
            }
        }
    }
}
