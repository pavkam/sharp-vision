// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Draws shared control border, shadow, and body-fill chrome into semantic cells.</summary>
internal static class ControlChrome
{
    extension(Rect body)
    {
        /// <summary>Expands one body rectangle by a signed shadow offset when enabled.</summary>
        /// <param name="hasShadow">Whether visual overflow is active.</param>
        /// <param name="mode">The composition mode that determines vertical shadow units.</param>
        /// <param name="offset">The signed shadow translation.</param>
        /// <returns>The union of the body and translated shadow footprint.</returns>
        public Rect ExpandVisualBounds(
            bool hasShadow,
            ShadowMode mode,
            Point offset) =>
            hasShadow
                ? body.Union(mode == ShadowMode.FractionalBlock
                    ? FractionalShadowBounds(body, offset)
                    : body.Shift(offset))
                : body;
    }

    /// <summary>Resolves one control's own visual clip from hard and soft branch constraints.</summary>
    /// <param name="contentClip">The inherited ordinary-layout aperture.</param>
    /// <param name="body">The control's arranged body rectangle.</param>
    /// <param name="visual">The control's own visual rectangle.</param>
    /// <param name="hardClip">The nearest frame, viewport, or explicit clipping rectangle.</param>
    /// <returns>The finite visible portion of <paramref name="visual"/>.</returns>
    public static Rect ResolveVisualClip(
        Rect contentClip,
        Rect body,
        Rect visual,
        Rect hardClip) =>
        visual.Intersect(ResolveClipBox(contentClip, body, visual, hardClip));

    /// <summary>Widens an inherited soft-layout aperture so it follows a rectangle that extends
    /// beyond one control's own body, exactly as <see cref="ResolveVisualClip"/> widens the own-paint
    /// clip for a visual that overflows its body - clamped last by one hard constraint.</summary>
    /// <param name="contentClip">The inherited ordinary-layout aperture.</param>
    /// <param name="body">The control's arranged body rectangle.</param>
    /// <param name="extent">The rectangle being clipped, compared against <paramref name="body"/>
    /// to decide whether - and how far - the aperture widens.</param>
    /// <param name="hardClip">The nearest frame, viewport, or explicit clipping rectangle.</param>
    /// <returns>The widened aperture, not yet intersected with <paramref name="extent"/>.</returns>
    public static Rect ResolveClipBox(
        Rect contentClip,
        Rect body,
        Rect extent,
        Rect hardClip)
    {
        var left = contentClip.X;
        var top = contentClip.Y;
        var right = contentClip.Right;
        var bottom = contentClip.Bottom;
        var bodyIntersection = body.Intersect(contentClip);

        if (bodyIntersection.Width > 0 && bodyIntersection.Height > 0)
        {
            if (extent.X < body.X)
            {
                left = Math.Min(left, extent.X);
            }

            if (extent.Y < body.Y)
            {
                top = Math.Min(top, extent.Y);
            }

            if (extent.Right > body.Right)
            {
                right = Math.Max(right, extent.Right);
            }

            if (extent.Bottom > body.Bottom)
            {
                bottom = Math.Max(bottom, extent.Bottom);
            }
        }

        left = Math.Max(left, hardClip.X);
        top = Math.Max(top, hardClip.Y);
        right = Math.Max(left, Math.Min(right, hardClip.Right));
        bottom = Math.Max(top, Math.Min(bottom, hardClip.Bottom));
        return new Rect(left, top, Extent(left, right), Extent(top, bottom));
    }

    /// <summary>Draws shadow, optional opaque fill, and border chrome for one control body.</summary>
    /// <param name="control">The control supplying resolved theme values.</param>
    /// <param name="canvas">The clipped semantic canvas.</param>
    /// <param name="visualState">The active visual-state flags.</param>
    /// <param name="options">Optional body and shadow overrides.</param>
    public static void Render(
        ControlBase control,
        TerminalCanvas canvas,
        VisualState visualState,
        ChromeRenderOptions? options = null)
    {
        control.RenderUnderlay(canvas, visualState, options);
        control.RenderBorder(canvas, visualState, options);
    }

    extension(ControlBase control)
    {
        /// <summary>Draws framework-owned shadow and body fill before content.</summary>
        public void RenderUnderlay(
            TerminalCanvas canvas,
            VisualState visualState,
            ChromeRenderOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(control);

            var settings = options ?? default;
            var body = settings.BodyBounds ?? control.Bounds;
            var appearance = control.GetResolvedAppearance(visualState);
            var bodyStyle = appearance.Style;
            var background = appearance.BackgroundMode;

            if (appearance.Shadow.IsVisible && !settings.SkipShadow)
            {
                canvas.DrawShadow(
                    control,
                    body,
                    settings.ShadowExcludeBounds ?? body,
                    appearance.Shadow,
                    appearance.ShadowStyle,
                    appearance.ShadowBackgroundMode,
                    settings.PreserveButtonShadowGap);
            }

            var isPressed = (visualState & VisualState.Pressed) != 0;

            if (background == BackgroundMode.Opaque && !settings.SkipBodyFill &&
                (!settings.ClearBodyWhenPressedWithShadow || !isPressed || !appearance.Shadow.IsVisible))
            {
                canvas.Clear(FillBounds(body, appearance.Border.Sides), bodyStyle);
            }
            else if (settings.ClearBodyWhenPressedWithShadow && isPressed && appearance.Shadow.IsVisible)
            {
                canvas.Clear(FillBounds(body, appearance.Border.Sides), bodyStyle);
            }
        }

        /// <summary>Draws framework-owned border chrome after normal-layer content and children.</summary>
        public void RenderBorder(
            TerminalCanvas canvas,
            VisualState visualState,
            ChromeRenderOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(control);
            var settings = options ?? default;
            var body = settings.BodyBounds ?? control.Bounds;
            var appearance = control.GetResolvedAppearance(visualState);
            var border = appearance.Border;

            if (!settings.SkipBorder && border.Sides != BorderSide.None)
            {
                var glyphs = control.ResolveBorderGlyphs(settings.BorderGlyphStyle ?? border.GlyphStyle);
                canvas.DrawPartialBorder(
                    body,
                    border.Sides,
                    glyphs,
                    appearance.BorderStyle,
                    appearance.BorderBackgroundMode);
            }
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static Rect FillBounds(Rect body, BorderSide sides) => new Thickness(
        (sides & BorderSide.Left) != 0 ? 1 : 0,
        (sides & BorderSide.Top) != 0 ? 1 : 0,
        (sides & BorderSide.Right) != 0 ? 1 : 0,
        (sides & BorderSide.Bottom) != 0 ? 1 : 0).Deflate(body);

    extension(TerminalCanvas canvas)
    {
        /// <summary>Draws one complete one-cell-wide border frame.</summary>
        /// <param name="bounds">The frame rectangle.</param>
        /// <param name="glyphs">The validated glyph family.</param>
        /// <param name="style">The border semantic style.</param>
        /// <param name="background">Whether border backgrounds replace destination cells.</param>
        public void DrawUniformBorder(
            Rect bounds,
            BorderGlyphStyle glyphs,
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
        /// <param name="bounds">The frame rectangle.</param>
        /// <param name="border">The enabled border edges.</param>
        /// <param name="glyphs">The validated glyph family.</param>
        /// <param name="style">The border semantic style.</param>
        /// <param name="background">Whether border backgrounds replace destination cells.</param>
        public void DrawPartialBorder(
            Rect bounds,
            BorderSide border,
            BorderGlyphStyle glyphs,
            TerminalStyle style,
            BackgroundMode background)
        {
            if (bounds.Width == 0 || bounds.Height == 0)
            {
                return;
            }

            // The degenerate-dimension guards exist so a one-cell dimension does not paint the same
            // cell twice - but only the FIRST edge can have claimed it, so they must not fire when
            // that edge is disabled. Unconditionally, they skipped the only enabled edge and left
            // the cell blank: BorderSide.Right on a one-column rect painted nothing at all, while
            // BorderInset still reserved the column, so the space was paid for and never drawn.
            DrawHorizontalEdge(canvas, bounds, border, glyphs, style, background, top: true);
            if (bounds.Height > 1 || (border & BorderSide.Top) == 0)
            {
                DrawHorizontalEdge(canvas, bounds, border, glyphs, style, background, top: false);
            }

            DrawVerticalEdge(canvas, bounds, border, glyphs, style, background, left: true);
            if (bounds.Width > 1 || (border & BorderSide.Left) == 0)
            {
                DrawVerticalEdge(canvas, bounds, border, glyphs, style, background, left: false);
            }
        }

        /// <summary>Draws translated shadow overflow outside one excluded body region.</summary>
        /// <param name="control">The control supplying shadow theme values.</param>
        /// <param name="sourceBounds">The rectangle translated to form the shadow footprint.</param>
        /// <param name="excludeBounds">Cells inside this rectangle are not shadowed.</param>
        /// <param name="shadow">The fully resolved shadow geometry and glyph.</param>
        /// <param name="style">The fully resolved shadow cell style.</param>
        /// <param name="background">Whether shadow backgrounds replace destination cells.</param>
        /// <param name="preserveButtonShadowGap">Whether to leave one cell before the bottom strip.</param>
        public void DrawShadow(
            ControlBase control,
            Rect sourceBounds,
            Rect excludeBounds,
            Shadow shadow,
            TerminalStyle style,
            BackgroundMode background,
            bool preserveButtonShadowGap = false)
        {
            ArgumentNullException.ThrowIfNull(control);

            var target = sourceBounds.Shift(shadow.Offset).Intersect(canvas.Bounds);

            if (shadow.Mode == ShadowMode.FractionalBlock)
            {
                DrawFractionalShadow(
                    canvas,
                    control,
                    sourceBounds,
                    excludeBounds,
                    new TerminalStyle(style.Foreground, Color.Default, style.Attributes,
                        style.Hyperlink, style.Underline, style.UnderlineColor),
                    BackgroundMode.Transparent);
                return;
            }

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
                        x == target.X)
                    {
                        continue;
                    }

                    if (shadow.Mode == ShadowMode.Composite)
                    {
                        canvas.ApplyStyle(new Rect(x, y, 1, 1), style, background);
                    }
                    else
                    {
                        Debug.Assert(
                            shadow.Mode == ShadowMode.BlockGlyph,
                            "Public validation limits shadow modes.");
                        var glyph = shadow.Glyph.Resolve(ControlGlyphs.Chrome.Shadow.Fallback, control.CellPolicy.AmbiguousWidth);
                        canvas.DrawRune(glyph, point, style, background);
                    }
                }
            }
        }
    }

    extension(Rect value)
    {
        public Rect Shift(Point offset) => new(
            value.X.SaturatingAdd(offset.X),
            value.Y.SaturatingAdd(offset.Y),
            value.Width,
            value.Height);
    }

    private static Rect FractionalShadowBounds(Rect source, Point offset)
    {
        var horizontal = source.Shift(new Point(offset.X, 0));
        var topHalf = (2L * source.Y) + offset.Y;
        var bottomHalf = (2L * source.Bottom) + offset.Y;
        var top = SaturatingToInt(FloorHalf(topHalf));
        var bottom = SaturatingToInt(CeilingHalf(bottomHalf));
        return new Rect(horizontal.X, top, horizontal.Width, Extent(top, bottom));
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static void DrawFractionalShadow(
        TerminalCanvas canvas,
        ControlBase control,
        Rect sourceBounds,
        Rect excludeBounds,
        TerminalStyle style,
        BackgroundMode background)
    {
        var offset = control.ActualShadow.Offset;

        if (offset == default || sourceBounds.Width == 0 || sourceBounds.Height == 0)
        {
            return;
        }

        var unclippedTarget = FractionalShadowBounds(sourceBounds, offset);
        var target = unclippedTarget.Intersect(canvas.Bounds);
        var targetTopHalf = (2L * sourceBounds.Y) + offset.Y;
        var targetBottomHalf = (2L * sourceBounds.Bottom) + offset.Y;
        var chrome = ControlGlyphs.Chrome;
        var upper = control.ResolveControlGlyph(chrome.FractionalUpper);
        var lower = control.ResolveControlGlyph(chrome.FractionalLower);
        var full = control.ResolveControlGlyph(chrome.FractionalFull);
        for (var y = target.Y; y < target.Bottom; y++)
        {
            var cellTopHalf = 2L * y;
            var coversUpper = targetTopHalf < cellTopHalf + 1 && targetBottomHalf > cellTopHalf;
            var coversLower = targetTopHalf < cellTopHalf + 2 && targetBottomHalf > cellTopHalf + 1;

            for (var x = target.X; x < target.Right; x++)
            {
                var point = new Point(x, y);

                if (excludeBounds.Contains(point))
                {
                    continue;
                }

                var glyph = coversUpper && coversLower
                    ? full
                    : coversUpper
                        ? upper
                        : lower;

                canvas.DrawRune(glyph, point, style, background);
            }
        }
    }

    extension(Rect left)
    {
        public Rect Union(Rect right)
        {
            var x = Math.Min(left.X, right.X);
            var y = Math.Min(left.Y, right.Y);
            var rightEdge = Math.Max(left.Right, right.Right);
            var bottom = Math.Max(left.Bottom, right.Bottom);
            var width = (long) rightEdge - x;
            var height = (long) bottom - y;

            // A single Rect cannot represent a span wider than Int32.MaxValue.
            // Preserve the arranged body rather than clipping valid body content
            // in favor of a practically unreachable translated shadow.
            return width > int.MaxValue || height > int.MaxValue
                ? left
                : new Rect(x, y, (int) width, (int) height);
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static void DrawHorizontalEdge(
        TerminalCanvas canvas,
        Rect bounds,
        BorderSide border,
        BorderGlyphStyle glyphs,
        TerminalStyle style,
        BackgroundMode background,
        bool top)
    {
        var active = top ? (border & BorderSide.Top) != 0 : (border & BorderSide.Bottom) != 0;

        if (!active)
        {
            return;
        }

        var y = top ? bounds.Y : bounds.Bottom - 1;

        for (var x = bounds.X; x < bounds.Right; x++)
        {
            var glyph = top ? glyphs.Top : glyphs.Bottom;

            if (x == bounds.X && (border & BorderSide.Left) != 0)
            {
                glyph = top ? glyphs.TopLeft : glyphs.BottomLeft;
            }
            else if (x == bounds.Right - 1 && (border & BorderSide.Right) != 0)
            {
                glyph = top ? glyphs.TopRight : glyphs.BottomRight;
            }

            canvas.DrawRune(glyph, new Point(x, y), style, background);
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static void DrawVerticalEdge(
        TerminalCanvas canvas,
        Rect bounds,
        BorderSide border,
        BorderGlyphStyle glyphs,
        TerminalStyle style,
        BackgroundMode background,
        bool left)
    {
        var active = left ? (border & BorderSide.Left) != 0 : (border & BorderSide.Right) != 0;

        if (!active)
        {
            return;
        }

        var x = left ? bounds.X : bounds.Right - 1;
        var start = bounds.Y + ((border & BorderSide.Top) != 0 ? 1 : 0);
        var end = bounds.Bottom - ((border & BorderSide.Bottom) != 0 ? 1 : 0);

        for (var y = start; y < end; y++)
        {
            var glyph = left ? glyphs.Left : glyphs.Right;
            canvas.DrawRune(glyph, new Point(x, y), style, background);
        }
    }

    extension(TerminalCanvas canvas)
    {
        /// <summary>Draws one horizontal line of identical glyphs across the full width of a bounds rectangle.</summary>
        public void DrawHorizontalLine(
            Rect bounds,
            Rune glyph,
            TerminalStyle style)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                canvas.DrawRune(glyph, new Point(x, bounds.Y), style, BackgroundMode.Transparent);
            }
        }
    }

    private static int Extent(int start, int end) =>
        (int) Math.Min(int.MaxValue, Math.Max(0L, (long) end - start));

    private static int SaturatingToInt(long value) =>
        (int) Math.Clamp(value, int.MinValue, int.MaxValue);

    private static long FloorHalf(long value)
    {
        var quotient = value / 2;
        return value < 0 && value % 2 != 0 ? quotient - 1 : quotient;
    }

    private static long CeilingHalf(long value)
    {
        var quotient = value / 2;
        return value > 0 && value % 2 != 0 ? quotient + 1 : quotient;
    }
}
