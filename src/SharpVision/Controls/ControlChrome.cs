// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;



/// <summary>Draws shared control border, shadow, and body-fill chrome into semantic cells.</summary>
internal static class ControlChrome
{
    /// <summary>Expands one body rectangle by a signed shadow offset when enabled.</summary>
    /// <param name="body">The non-negative body rectangle.</param>
    /// <param name="hasShadow">Whether visual overflow is active.</param>
    /// <param name="offset">The signed shadow translation.</param>
    /// <returns>The union of the body and translated shadow footprint.</returns>
    internal static Rect ExpandVisualBounds(Rect body, bool hasShadow, Point offset) =>
        hasShadow ? Union(body, Shift(body, offset)) : body;

    /// <summary>Draws shadow, optional opaque fill, and border chrome for one control body.</summary>
    /// <param name="control">The control supplying resolved theme values.</param>
    /// <param name="canvas">The clipped semantic canvas.</param>
    /// <param name="visualState">The active visual-state flags.</param>
    /// <param name="options">Optional body and shadow overrides.</param>
    internal static void Render(
        Control control,
        TerminalCanvas canvas,
        State visualState,
        ChromeRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(control);

        var settings = options ?? default;
        var body = settings.BodyBounds ?? control.Bounds;
        var bodyStyle = control.GetResolvedAppearance(visualState).Style;
        var opaque = ControlAppearance.HasOpaqueFill(control, visualState);
        var background = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;

        if (control.HasShadow && !settings.SkipShadow)
        {
            DrawShadow(
                canvas,
                control,
                body,
                settings.ShadowExcludeBounds ?? body,
                background,
                settings.ShadowAppearanceSource ?? control.GetResolvedAppearance(State.Normal).Style,
                settings.PreserveButtonShadowGap);
        }

        if (opaque && !settings.SkipBodyFill &&
            (!settings.ClearBodyWhenPressedWithShadow || !control.IsPressed || !control.HasShadow))
        {
            canvas.Clear(body, bodyStyle);
        }
        else if (settings.ClearBodyWhenPressedWithShadow && control.IsPressed && control.HasShadow)
        {
            canvas.Clear(body, bodyStyle);
        }

        if (!settings.SkipBorder && control.BorderThickness != default)
        {
            var borderStyle = ControlAppearance.ResolveBorderStyle(control, visualState);
            var glyphs = settings.BorderGlyphs ?? control.BorderGlyphs;
            DrawPartialBorder(
                canvas,
                body,
                control.BorderThickness,
                glyphs,
                borderStyle,
                background,
                control.CellPolicy);
        }
    }

    /// <summary>Draws one complete one-cell-wide border frame.</summary>
    /// <param name="canvas">The semantic canvas.</param>
    /// <param name="bounds">The frame rectangle.</param>
    /// <param name="glyphs">The validated glyph family.</param>
    /// <param name="style">The border semantic style.</param>
    /// <param name="background">Whether border backgrounds replace destination cells.</param>
    internal static void DrawUniformBorder(
        TerminalCanvas canvas,
        Rect bounds,
        Glyphs glyphs,
        TerminalStyle style,
        BackgroundMode background)
    {
        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        for (var x = bounds.X; x < bounds.Right; x++)
        {
            var top = x == bounds.X ? glyphs.TopLeft : x == bounds.Right - 1 ? glyphs.TopRight : glyphs.Top;
            var bottom = x == bounds.X ? glyphs.BottomLeft : x == bounds.Right - 1 ? glyphs.BottomRight : glyphs.Bottom;
            canvas.DrawRune(top, new Point(x, bounds.Y), style, background);

            if (bounds.Height > 1)
            {
                canvas.DrawRune(bottom, new Point(x, bounds.Bottom - 1), style, background);
            }
        }

        for (var y = bounds.Y + 1; y < bounds.Bottom - 1; y++)
        {
            canvas.DrawRune(glyphs.Left, new Point(bounds.X, y), style, background);

            if (bounds.Width > 1)
            {
                canvas.DrawRune(glyphs.Right, new Point(bounds.Right - 1, y), style, background);
            }
        }
    }

    /// <summary>Draws independently enabled zero-or-one-cell border edges.</summary>
    /// <param name="canvas">The semantic canvas.</param>
    /// <param name="bounds">The frame rectangle.</param>
    /// <param name="thickness">The enabled border edges.</param>
    /// <param name="glyphs">The validated glyph family.</param>
    /// <param name="style">The border semantic style.</param>
    /// <param name="background">Whether border backgrounds replace destination cells.</param>
    /// <param name="cellPolicy">The Unicode cell policy used for glyph repair.</param>
    internal static void DrawPartialBorder(
        TerminalCanvas canvas,
        Rect bounds,
        Thickness thickness,
        Glyphs glyphs,
        TerminalStyle style,
        BackgroundMode background,
        Policy cellPolicy)
    {
        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        DrawHorizontalEdge(canvas, bounds, thickness, glyphs, style, background, cellPolicy, top: true);
        if (bounds.Height > 1)
        {
            DrawHorizontalEdge(canvas, bounds, thickness, glyphs, style, background, cellPolicy, top: false);
        }

        DrawVerticalEdge(canvas, bounds, thickness, glyphs, style, background, cellPolicy, left: true);
        if (bounds.Width > 1)
        {
            DrawVerticalEdge(canvas, bounds, thickness, glyphs, style, background, cellPolicy, left: false);
        }
    }

    /// <summary>Draws translated shadow overflow outside one excluded body region.</summary>
    /// <param name="canvas">The semantic canvas.</param>
    /// <param name="control">The control supplying shadow theme values.</param>
    /// <param name="sourceBounds">The rectangle translated to form the shadow footprint.</param>
    /// <param name="excludeBounds">Cells inside this rectangle are not shadowed.</param>
    /// <param name="background">Whether shadow backgrounds replace destination cells.</param>
    /// <param name="appearanceSource">The inherited style supplying dimmed shadow colors.</param>
    /// <param name="preserveButtonShadowGap">Whether to leave one cell before the bottom strip.</param>
    internal static void DrawShadow(
        TerminalCanvas canvas,
        Control control,
        Rect sourceBounds,
        Rect excludeBounds,
        BackgroundMode background,
        TerminalStyle appearanceSource,
        bool preserveButtonShadowGap = false)
    {
        ArgumentNullException.ThrowIfNull(control);

        var target = Shift(sourceBounds, control.ShadowOffset).Intersect(canvas.Bounds);
        var shadowBackground = control.ShadowBackground ?? control.Background ?? appearanceSource.Background;
        var style = ResolveShadowStyle(control, appearanceSource, shadowBackground);

        for (var y = target.Y; y < target.Bottom; y++)
        {
            for (var x = target.X; x < target.Right; x++)
            {
                var point = new Point(x, y);

                if (excludeBounds.Contains(point))
                {
                    continue;
                }

                if (preserveButtonShadowGap &&
                    y >= sourceBounds.Bottom &&
                    x <= sourceBounds.X + Math.Abs(control.ShadowOffset.X))
                {
                    continue;
                }

                if (control.ShadowMode == ShadowMode.Composite)
                {
                    canvas.ApplyStyle(new Rect(x, y, 1, 1), style, background);
                }
                else
                {
                    Debug.Assert(
                        control.ShadowMode == ShadowMode.BlockGlyph,
                        "Public validation limits shadow modes.");
                    var glyph = CellGlyph.Resolve(
                        control.ShadowGlyph,
                        new Rune('#'),
                        control.CellPolicy.AmbiguousWidth);
                    canvas.DrawRune(glyph, point, style, background);
                }
            }
        }
    }

    internal static Rect Shift(Rect value, Point offset) => new(
        SaturatingAdd(value.X, offset.X),
        SaturatingAdd(value.Y, offset.Y),
        value.Width,
        value.Height);

    internal static Rect Union(Rect left, Rect right)
    {
        var x = Math.Min(left.X, right.X);
        var y = Math.Min(left.Y, right.Y);
        var rightEdge = Math.Max(left.Right, right.Right);
        var bottom = Math.Max(left.Bottom, right.Bottom);
        return new Rect(x, y, Extent(x, rightEdge), Extent(y, bottom));
    }

    private static TerminalStyle ResolveShadowStyle(
        Control control,
        TerminalStyle appearanceSource,
        Color shadowBackground)
    {
        (var attributes, var underline, var underlineColor) = Decoration.Resolve(
            appearanceSource,
            control.ShadowAttributes);
        return new TerminalStyle(
            control.ShadowForeground ?? appearanceSource.Foreground,
            shadowBackground,
            attributes,
            appearanceSource.Hyperlink,
            underline,
            underlineColor);
    }

    private static void DrawHorizontalEdge(
        TerminalCanvas canvas,
        Rect bounds,
        Thickness thickness,
        Glyphs glyphs,
        TerminalStyle style,
        BackgroundMode background,
        Policy cellPolicy,
        bool top)
    {
        var active = top ? thickness.Top != 0 : thickness.Bottom != 0;

        if (!active)
        {
            return;
        }

        var y = top ? bounds.Y : bounds.Bottom - 1;

        for (var x = bounds.X; x < bounds.Right; x++)
        {
            var glyph = top ? glyphs.Top : glyphs.Bottom;

            if (x == bounds.X && thickness.Left != 0)
            {
                glyph = top ? glyphs.TopLeft : glyphs.BottomLeft;
            }
            else if (x == bounds.Right - 1 && thickness.Right != 0)
            {
                glyph = top ? glyphs.TopRight : glyphs.BottomRight;
            }

            var fallback = x == bounds.X || x == bounds.Right - 1
                ? new Rune('+')
                : new Rune('-');
            canvas.DrawRune(
                CellGlyph.Resolve(glyph, fallback, cellPolicy.AmbiguousWidth),
                new Point(x, y),
                style,
                background);
        }
    }

    private static void DrawVerticalEdge(
        TerminalCanvas canvas,
        Rect bounds,
        Thickness thickness,
        Glyphs glyphs,
        TerminalStyle style,
        BackgroundMode background,
        Policy cellPolicy,
        bool left)
    {
        var active = left ? thickness.Left != 0 : thickness.Right != 0;

        if (!active)
        {
            return;
        }

        var x = left ? bounds.X : bounds.Right - 1;
        var start = bounds.Y + thickness.Top;
        var end = bounds.Bottom - thickness.Bottom;

        for (var y = start; y < end; y++)
        {
            var glyph = left ? glyphs.Left : glyphs.Right;
            canvas.DrawRune(
                CellGlyph.Resolve(glyph, new Rune('|'), cellPolicy.AmbiguousWidth),
                new Point(x, y),
                style,
                background);
        }
    }

    private static int Extent(int start, int end) =>
        (int) Math.Min(int.MaxValue, Math.Max(0L, (long) end - start));

    private static int SaturatingAdd(int left, int right) =>
        (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);
}

