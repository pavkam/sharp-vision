// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

using Capabilities;

using Kitty.Graphics;

/// <summary>Encodes semantic frame damage through one immutable terminal profile.</summary>
[PublicAPI]
public static class FrameEncoder
{
    private const int _stackLinkBytes = 512;
    [ThreadStatic]
    private static Interpreter? _ansiInterpreter;
    [ThreadStatic]
    private static ProgramLimits? _ansiInterpreterLimits;
    [ThreadStatic]
    private static TerminalProfile? _ansiProfile;

    /// <summary>Encodes through the built-in ANSI compatibility profile.</summary>
    /// <param name="front">The committed frame, or null for a full redraw.</param>
    /// <param name="back">The target semantic frame.</param>
    /// <param name="destination">The synchronous byte destination.</param>
    /// <param name="capabilities">The non-null semantic capability snapshot.</param>
    /// <param name="full">Whether to force a full redraw.</param>
    /// <param name="limits">The finite interpretation limits, or <see langword="null"/> for defaults.</param>
    /// <returns>The number of spans and full/incremental classification.</returns>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ObjectDisposedException">A supplied frame is disposed.</exception>
    public static EncodeResult Encode(
        Frame? front,
        Frame back,
        IBufferWriter<byte> destination,
        TerminalCapabilities capabilities,
        bool full = false,
        ProgramLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var profile = _ansiProfile;

        if (profile is null || !Equals(profile.Capabilities, capabilities))
        {
            profile = TerminalProfile.CreateAnsi(capabilities);
            _ansiProfile = profile;
        }

        var effectiveLimits = limits ?? ProgramLimits.Default;

        if (_ansiInterpreter is null || _ansiInterpreterLimits != effectiveLimits)
        {
            _ansiInterpreter = new Interpreter(effectiveLimits);
            _ansiInterpreterLimits = effectiveLimits;
        }

        return Encode(
            front,
            back,
            destination,
            profile,
            _ansiInterpreter,
            full);
    }

    /// <summary>Encodes through compiled programs owned by one terminal profile.</summary>
    /// <param name="front">The committed frame, or null for a full redraw.</param>
    /// <param name="back">The target semantic frame.</param>
    /// <param name="destination">The synchronous byte destination.</param>
    /// <param name="profile">The non-null immutable terminal profile.</param>
    /// <param name="full">Whether to force a full redraw.</param>
    /// <param name="limits">The finite interpretation limits, or <see langword="null"/> for defaults.</param>
    /// <returns>The number of spans and full/incremental classification.</returns>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ObjectDisposedException">A supplied frame is disposed.</exception>
    /// <exception cref="InvalidOperationException">A required description program cannot expand.</exception>
    /// <remarks>
    /// This direct convenience call uses a fresh one-shot interpreter. Session-scoped
    /// ncurses static variables and warmed allocation guarantees require <see cref="Renderer"/>,
    /// which owns and transactionally commits its interpreter across frames.
    /// </remarks>
    public static EncodeResult Encode(
        Frame? front,
        Frame back,
        IBufferWriter<byte> destination,
        TerminalProfile profile,
        bool full = false,
        ProgramLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var interpreter = new Interpreter(limits ?? ProgramLimits.Default);
        return Encode(front, back, destination, profile, interpreter, full);
    }

    /// <summary>Encodes with a renderer-owned interpreter that preserves description static variables.</summary>
    internal static EncodeResult Encode(
        Frame? front,
        Frame back,
        IBufferWriter<byte> destination,
        TerminalProfile profile,
        Interpreter interpreter,
        bool full = false,
        GraphicsCellOverlay? frontOverlay = null,
        GraphicsCellOverlay? backOverlay = null)
    {
        return EncodeWithState(
            front,
            back,
            destination,
            profile,
            interpreter,
            full,
            frontOverlay,
            backOverlay,
            resetScrollRegion: false,
            out _);
    }

    /// <summary>Encodes while reporting and repairing renderer-owned scroll-region state.</summary>
    internal static EncodeResult EncodeWithState(
        Frame? front,
        Frame back,
        IBufferWriter<byte> destination,
        TerminalProfile profile,
        Interpreter interpreter,
        bool full,
        GraphicsCellOverlay? frontOverlay,
        GraphicsCellOverlay? backOverlay,
        bool resetScrollRegion,
        out bool usedScrollRegion)
    {
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(interpreter);
        back.ThrowIfDisposed();
        front?.ThrowIfDisposed();
        var redraw = full || front is null || front.Size != back.Size;
        var semanticStyle = CellStyle.Default;
        var styleCacheValid = true;
        var style = CellStyle.Default;
        var spanCount = 0;
        var scroll = default(VerticalScrollDamage);
        var placeholderStyle = default(GraphicsCellOverlayValue);
        var usedFallback = false;
        usedScrollRegion = false;

        if (redraw)
        {
            if (resetScrollRegion)
            {
                Csi.ResetScrollRegion(new ProtocolWriter(destination));
            }

            // sgr0 restores SGR attributes only; it never terminates an OSC 8 hyperlink
            // (IsVisualDefault below deliberately excludes Hyperlink for the same reason).
            // A redraw exists to repair unknown terminal state after a torn or interrupted
            // write, so it must assume nothing about hyperlink state either, or a link left
            // open by a truncated prior frame strands every later cell inside it.
            Osc.CloseHyperlink(new ProtocolWriter(destination));
            WriteRequired(profile, interpreter, destination, "sgr0");

            if (!profile.Programs.TryWrite("clear", [], interpreter, destination))
            {
                WriteRequired(profile, interpreter, destination, "cup", 0, 0);
                WriteRequired(profile, interpreter, destination, "ed");
            }
        }

        // An eager-wrap description (am without xenl) never has its bottom-right cell written
        // below: the write loop truncates writeEnd one column short of the margin there, so the
        // real screen's bottom-right glyph can differ from what the retained front/back models
        // both agree it is (Renderer.CommitFront copies back into front wholesale, cell skipped
        // or not). Damage.TryFindVerticalScroll compares those in-memory models, so it would
        // treat the bottom row as an eligible scroll source and, once a real csr/scroll moves the
        // stale on-screen content into a row the model already considers matched, that wrong
        // content would never be repainted. Disabling the scroll path entirely for this
        // description shape keeps every row's content honestly re-derived from cup-positioned
        // writes instead.
        var eagerWrapProfile = IsEagerWrapProfile(profile);

        if (!redraw &&
            profile.AnsiCompatible &&
            !eagerWrapProfile &&
            Damage.TryFindVerticalScroll(
                front!,
                back,
                frontOverlay,
                backOverlay,
                out scroll))
        {
            var writer = new ProtocolWriter(destination);
            Csi.SetScrollRegion(writer, scroll.Top + 1, scroll.Bottom + 1);

            if (scroll.SourceOffset > 0)
            {
                Csi.ScrollUp(writer, scroll.Count);
            }
            else
            {
                Csi.ScrollDown(writer, scroll.Count);
            }

            Csi.ResetScrollRegion(writer);
            usedScrollRegion = true;
        }

        var damage = Damage.Enumerate(
            front,
            back,
            redraw,
            frontOverlay,
            backOverlay,
            scroll);

        foreach (var span in damage)
        {
            WriteRequired(profile, interpreter, destination, "cup", span.Row, span.Start);
            spanCount++;
            var end = span.Start + span.Length;

            if (TryEraseTrailingBlanks(
                    back,
                    span,
                    end,
                    destination,
                    profile,
                    interpreter,
                    ref semanticStyle,
                    ref style,
                    ref usedFallback,
                    ref placeholderStyle,
                    backOverlay))
            {
                continue;
            }

            // terminfo(5) distinguishes two shapes of "am" (automatic margins). A deferred-wrap
            // (xenl) terminal - the xterm family, kitty, tmux, conhost, and Windows Terminal -
            // leaves the wrap pending until the next byte arrives, so writing the final column
            // and then repositioning below still lands correctly: nothing after the write can
            // race the repair. An eager-wrap terminal (am without xenl, the classic vt100 shape)
            // wraps, and on the bottom row scrolls the whole screen, as part of the write itself:
            // no repair sequence can run early enough to prevent it. ncurses' own answer is to
            // never print into the bottom-right cell at all on such a terminal, and this mirrors
            // that: the row's write is truncated one column short of the margin so the cell is
            // left showing whatever was already there instead of ever being touched.
            var eagerBottomRow = eagerWrapProfile && span.Row == back.Size.Height - 1;
            var writeEnd = eagerBottomRow ? Math.Min(end, back.Size.Width - 1) : end;

            for (var column = span.Start; column < writeEnd; column++)
            {
                var index = checked((span.Row * back.Size.Width) + column);
                var cell = back.GetCellByIndex(index);
                var overlay = backOverlay?.GetCell(index) ?? default;

                // A continuation cell owns no independent output - it is a wide glyph's second
                // column, folded into its lead's single emission below. This is checked before
                // the overlay branch, not after it, so an overlay destination that (should
                // never, but might) still land on a continuation cell can't emit a placeholder
                // there: a placeholder is always exactly one protocol column wide, and letting
                // one fire on a continuation cell would desynchronize the row's emitted column
                // count from the frame width even though CanUsePlaceholder is meant to reject
                // such placements upstream.
                if (cell.IsContinuation)
                {
                    placeholderStyle = default;
                    continue;
                }

                // A wide lead one column short of the margin still visually occupies the
                // untouchable bottom-right cell once printed - printing it would defeat the
                // whole point of truncating writeEnd. Leave the entire cluster unwritten instead
                // of only its trailing column, the same as the plain single-width case above.
                if (eagerBottomRow && column + cell.Width > writeEnd)
                {
                    placeholderStyle = default;
                    continue;
                }

                if (overlay.IsActive)
                {
                    if (!PlaceholderStylesEqual(placeholderStyle, overlay))
                    {
                        style = ApplyPlaceholderStyle(
                            destination,
                            style,
                            overlay,
                            profile,
                            interpreter);
                        semanticStyle = new CellStyle(background: overlay.Background);
                        styleCacheValid = false;
                        placeholderStyle = overlay;
                    }

                    KittyGraphicsPlaceholderWriter.WriteText(overlay, destination);
                    continue;
                }

                placeholderStyle = default;

                var projected = styleCacheValid && cell.Style == semanticStyle
                    ? style
                    : Project(cell.Style, profile);
                usedFallback |= UsesFallback(cell.Style, projected, profile);
                style = ApplyStyle(destination, style, projected, profile, interpreter);
                semanticStyle = cell.Style;
                styleCacheValid = true;
                var grapheme = back.GetGrapheme(index);
                destination.Write(grapheme.IsEmpty ? " "u8 : grapheme);
            }

            // Writing the final column can leave a deferred-wrap (xenl) terminal in delayed-wrap
            // state. An immediate absolute position clears that state before another byte can
            // wrap or scroll. This repair only applies when EatNewlineGlitch is proven true: an
            // eager-wrap terminal already never reached the final column of its bottom row above,
            // so no repair is needed there, and on its non-bottom rows an eager wrap merely moves
            // the cursor to the next row's column 0 - harmless, since every span below still
            // repositions with an absolute "cup" before writing, so it can never inherit a stale
            // cursor column left by an earlier row's wrap.
            if (profile.Description.AutomaticMargins &&
                profile.Description.EatNewlineGlitch &&
                end == back.Size.Width &&
                end > 0)
            {
                WriteRequired(profile, interpreter, destination, "cup", span.Row, end - 1);
            }

            // The bottom-right cell an eager-wrap terminal skips above is permanently
            // untouchable, not merely delayed: Renderer.CommitFront copies every cell of "back"
            // into the retained "front" model wholesale (Frame.CopyFrom), not only the cells this
            // call actually wrote, so the in-memory model claims that cell already holds whatever
            // content "back" specifies even though the real screen was never sent it. Later
            // frames' plain cup-and-rewrite damage re-derives every visible cell from span writes
            // regardless of what the model believes, so that mismatch stays harmless there - but
            // Damage.TryFindVerticalScroll instead trusts the front/back models' agreement to
            // decide a real csr/scroll is safe, which would carry the screen's stale glyph into a
            // row the model then considers already correct and never repaint it. That is exactly
            // why the scroll path above is disabled outright for this description shape, the same
            // trade-off ncurses makes for this terminal shape.
        }

        ResetStyle(destination, style, profile, interpreter);
        var positionChanged = redraw || front!.Cursor.Position != back.Cursor.Position;

        if ((spanCount > 0 || positionChanged) && back.Size is { Width: > 0, Height: > 0 })
        {
            WriteRequired(
                profile,
                interpreter,
                destination,
                "cup",
                back.Cursor.Position.Y,
                back.Cursor.Position.X);
        }

        if ((redraw || front!.Cursor.Visible != back.Cursor.Visible) &&
            profile.Programs.HasZeroParameterPair("civis", "cnorm"))
        {
            WriteRequired(
                profile,
                interpreter,
                destination,
                back.Cursor.Visible ? "cnorm" : "civis");
        }

        if ((redraw || front!.Cursor.Shape != back.Cursor.Shape) &&
            profile.Programs.Has("Ss") &&
            profile.Programs.Has("Se"))
        {
            if (back.Cursor.Shape == CursorShape.Block)
            {
                WriteRequired(profile, interpreter, destination, "Se");
            }
            else
            {
                var shape = back.Cursor.Shape == CursorShape.Underline ? 4 : 6;
                WriteRequired(profile, interpreter, destination, "Ss", shape);
            }
        }

        return new EncodeResult(spanCount, redraw, usedFallback);
    }

    private static bool TryEraseTrailingBlanks(
        Frame back,
        DamageSpan span,
        int end,
        IBufferWriter<byte> destination,
        TerminalProfile profile,
        Interpreter interpreter,
        ref CellStyle semanticStyle,
        ref CellStyle style,
        ref bool usedFallback,
        ref GraphicsCellOverlayValue placeholderStyle,
        GraphicsCellOverlay? overlay)
    {
        if (!profile.Description.BackColorErase ||
            end != back.Size.Width ||
            !profile.Programs.Has("el"))
        {
            return false;
        }

        CellStyle? semantic = null;

        for (var column = span.Start; column < end; column++)
        {
            var index = checked((span.Row * back.Size.Width) + column);
            var cell = back.GetCellByIndex(index);

            if (overlay?.GetCell(index).IsActive == true ||
                cell.IsContinuation ||
                !back.GetGrapheme(index).IsEmpty)
            {
                return false;
            }

            semantic ??= cell.Style;

            if (semantic.Value != cell.Style)
            {
                return false;
            }
        }

        if (semantic is null)
        {
            return false;
        }

        var projected = Project(semantic.Value, profile);
        usedFallback |= UsesFallback(semantic.Value, projected, profile);

        if (projected.Foreground != Color.Default ||
            projected.Attributes != TerminalAttributes.None ||
            projected.Hyperlink is not null ||
            projected.Underline != Underline.None ||
            projected.UnderlineColor != Color.Default)
        {
            return false;
        }

        // This path can emit sgr0 (via ApplyStyle, when the current style still carries a
        // placeholder's identity foreground) without going through the ordinary-text branch
        // that normally invalidates the placeholder cache. Clear it here too, or the next
        // placeholder cell with the same identity hits the cache fast path and never
        // re-emits the SGR the terminal just had reset.
        placeholderStyle = default;
        style = ApplyStyle(destination, style, projected, profile, interpreter);
        semanticStyle = semantic.Value;
        return profile.Programs.TryWrite("el", [], interpreter, destination);
    }

    /// <summary>Determines whether a description's automatic margins wrap eagerly.</summary>
    /// <param name="profile">The non-null terminal profile.</param>
    /// <returns>
    /// Whether the description declares automatic margins (<c>am</c>) without deferred wrap
    /// (<c>xenl</c>) - the classic <c>vt100</c> shape that wraps, and on the bottom row scrolls,
    /// as part of writing the final column itself. The vertical-scroll optimization must never be
    /// used for such a description: its bottom-right cell is permanently left unwritten by the
    /// write loop below, so the retained front/back frame models can disagree with the real screen
    /// there even though both models agree with each other, and <see cref="Damage.TryFindVerticalScroll"/>
    /// - which compares only those models - would otherwise treat the bottom row as a legitimate
    /// scroll source and carry that stale on-screen content into a row the models then believe is
    /// already correct.
    /// </returns>
    internal static bool IsEagerWrapProfile(TerminalProfile profile) =>
        profile.Description.AutomaticMargins && !profile.Description.EatNewlineGlitch;

    private static bool UsesFallback(
        CellStyle semantic,
        CellStyle projected,
        TerminalProfile profile) =>
        semantic != projected ||
        (profile.RenderingColorDepth != ColorDepth.TrueColor &&
         (semantic.Foreground.IsRgb || semantic.Background.IsRgb || semantic.UnderlineColor.IsRgb));

    private static bool PlaceholderStylesEqual(
        GraphicsCellOverlayValue left,
        GraphicsCellOverlayValue right) =>
        left.IsActive &&
        left.ImageId == right.ImageId &&
        left.PlacementId == right.PlacementId &&
        left.Background == right.Background &&
        left.IdentityColorDepth == right.IdentityColorDepth;

    private static CellStyle ApplyPlaceholderStyle(
        IBufferWriter<byte> destination,
        CellStyle current,
        GraphicsCellOverlayValue placeholder,
        TerminalProfile profile,
        Interpreter interpreter)
    {
        var background = Project(
            new CellStyle(background: placeholder.Background),
            profile);
        _ = ApplyStyle(destination, current, background, profile, interpreter);
        var writer = new ProtocolWriter(destination);
        Color foreground;

        // The underline-color channel below carries the placement identity, not a real
        // underline decoration, so it cannot be folded into the returned CellStyle's
        // UnderlineColor: the constructor rejects a non-default underline color unless the
        // style also declares an actual underline, and this style declares none. Tracking it
        // faithfully would require a typed or legacy underline flag that was never actually
        // emitted, which would corrupt the encoder's model of the terminal's real underline
        // state for whatever cell follows.
        if (placeholder.IdentityColorDepth == ColorDepth.Indexed256)
        {
            Sgr.ForegroundPalette(writer, (int) placeholder.ImageId);
            Sgr.UnderlineColorPalette(writer, (int) placeholder.PlacementId);
            foreground = TerminalPalette.ColorAt((int) placeholder.ImageId);
        }
        else
        {
            foreground = IdentifierColor(placeholder.ImageId);
            Sgr.Foreground(writer, foreground);
            Sgr.UnderlineColor(writer, IdentifierColor(placeholder.PlacementId));
        }

        return new CellStyle(
            foreground,
            background.Background);
    }

    private static Color IdentifierColor(uint value) => Color.Rgb(
        (int) ((value >> 16) & byte.MaxValue),
        (int) ((value >> 8) & byte.MaxValue),
        (int) (value & byte.MaxValue));

    private static CellStyle ApplyStyle(
        IBufferWriter<byte> destination,
        CellStyle current,
        CellStyle target,
        TerminalProfile profile,
        Interpreter interpreter)
    {
        if (profile.AnsiCompatible)
        {
            ApplyAnsiStyle(destination, current, target, profile.Capabilities);
            return target;
        }

        if (!string.Equals(current.Hyperlink, target.Hyperlink, StringComparison.Ordinal))
        {
            var writer = new ProtocolWriter(destination);

            if (current.Hyperlink is not null)
            {
                Osc.CloseHyperlink(writer);
            }

            if (target.Hyperlink is not null)
            {
                OpenHyperlink(writer, target.Hyperlink);
            }
        }

        if (current.Attributes == target.Attributes &&
            current.Foreground == target.Foreground &&
            current.Background == target.Background &&
            current.Underline == target.Underline &&
            current.UnderlineColor == target.UnderlineColor)
        {
            return target;
        }

        if (!IsVisualDefault(current))
        {
            WriteRequired(profile, interpreter, destination, "sgr0");
        }

        var attributes = TerminalAttributes.None;
        attributes |= ApplyAttribute(destination, target.Attributes, TerminalAttributes.Bold, "bold", profile, interpreter);
        attributes |= ApplyAttribute(destination, target.Attributes, TerminalAttributes.Dim, "dim", profile, interpreter);
        attributes |= ApplyAttribute(destination, target.Attributes, TerminalAttributes.Italic, "sitm", profile, interpreter);
        attributes |= ApplyAttribute(destination, target.Attributes, TerminalAttributes.Blink, "blink", profile, interpreter);
        attributes |= ApplyAttribute(destination, target.Attributes, TerminalAttributes.Reverse, "rev", profile, interpreter);
        attributes |= ApplyAttribute(destination, target.Attributes, TerminalAttributes.Hidden, "invis", profile, interpreter);
        attributes |= ApplyAttribute(destination, target.Attributes, TerminalAttributes.Strike, "smxx", profile, interpreter);
        attributes |= ApplyAttribute(destination, target.Attributes, TerminalAttributes.Overline, "Smol", profile, interpreter);
        var (underlineAttribute, underline) = ApplyUnderline(destination, target, profile, interpreter);
        attributes |= underlineAttribute;
        var (foreground, background) = ApplyColors(destination, target, profile, interpreter);
        var underlineColor = ApplyUnderlineColor(
            destination,
            target.UnderlineColor,
            underlineAttribute != TerminalAttributes.None || underline != Underline.None,
            profile,
            interpreter);
        return new CellStyle(
            foreground,
            background,
            attributes,
            target.Hyperlink,
            underline,
            underlineColor);
    }

    private static TerminalAttributes ApplyAttribute(
        IBufferWriter<byte> destination,
        TerminalAttributes attributes,
        TerminalAttributes value,
        string program,
        TerminalProfile profile,
        Interpreter interpreter)
    {
        return (attributes & value) != 0 &&
            profile.Programs.TryWrite(program, [], interpreter, destination)
                ? value
                : TerminalAttributes.None;
    }

    private static (TerminalAttributes Attribute, Underline Underline) ApplyUnderline(
        IBufferWriter<byte> destination,
        CellStyle style,
        TerminalProfile profile,
        Interpreter interpreter)
    {
        if (style.Underline != Underline.None &&
            profile.Capabilities.StyledUnderlines.Authoritative &&
            profile.Programs.TryWrite("Smulx", [(int) style.Underline], interpreter, destination))
        {
            return (TerminalAttributes.None, style.Underline);
        }

        if ((style.Attributes & TerminalAttributes.Underline) != 0 || style.Underline != Underline.None)
        {
            return profile.Programs.TryWrite("smul", [], interpreter, destination)
                ? (TerminalAttributes.Underline, Underline.None)
                : (TerminalAttributes.None, Underline.None);
        }

        return (TerminalAttributes.None, Underline.None);
    }

    private static Color ApplyColor(
        IBufferWriter<byte> destination,
        Color color,
        bool foreground,
        TerminalProfile profile,
        Interpreter interpreter) =>
        color == Color.Default
            ? profile.Programs.TryWrite(
                    foreground ? "setdf" : "setdb",
                    [],
                    interpreter,
                    destination)
                ? color
                : Color.Default
            : profile.RenderingColorDepth == ColorDepth.TrueColor && color.IsRgb
            ? profile.Programs.TryWrite(
                    foreground ? "setrgbf" : "setrgbb",
                    [color.Red, color.Green, color.Blue],
                    interpreter,
                    destination)
                ? color
                : Color.Default
            : color.IsRgb &&
              profile.RenderingColorDepth is ColorDepth.Basic16 or ColorDepth.Indexed256 &&
              profile.Programs.TryWrite(
                  foreground ? "setaf" : "setab",
                  [TerminalPalette.FindPosition(color, profile.RenderingColorDepth)],
                  interpreter,
                  destination)
                    ? color
                    : Color.Default;

    private static (Color Foreground, Color Background) ApplyColors(
        IBufferWriter<byte> destination,
        CellStyle target,
        TerminalProfile profile,
        Interpreter interpreter)
    {
        if (target.Foreground == Color.Default &&
            target.Background == Color.Default &&
            profile.Programs.TryWrite("op", [], interpreter, destination))
        {
            return (Color.Default, Color.Default);
        }

        return (
            ApplyColor(destination, target.Foreground, foreground: true, profile, interpreter),
            ApplyColor(destination, target.Background, foreground: false, profile, interpreter));
    }

    private static Color ApplyUnderlineColor(
        IBufferWriter<byte> destination,
        Color color,
        bool hasUnderline,
        TerminalProfile profile,
        Interpreter interpreter)
    {
        if (color == Color.Default ||
            !hasUnderline ||
            !profile.Capabilities.UnderlineColor.Authoritative ||
            !profile.Programs.Has("Setulc"))
        {
            return Color.Default;
        }

        Debug.Assert(color.IsRgb, "Projected underline colors are concrete RGB values.");
        var parameter = Packed(color);
        return profile.Programs.TryWrite("Setulc", [parameter], interpreter, destination)
            ? color
            : Color.Default;
    }

    private static void ResetStyle(
        IBufferWriter<byte> destination,
        CellStyle style,
        TerminalProfile profile,
        Interpreter interpreter)
    {
        if (style.Hyperlink is not null)
        {
            Osc.CloseHyperlink(new ProtocolWriter(destination));
        }

        if (!IsVisualDefault(style))
        {
            WriteRequired(profile, interpreter, destination, "sgr0");
        }
    }

    private static CellStyle Project(CellStyle value, TerminalProfile profile)
    {
        if (profile.AnsiCompatible)
        {
            return ProjectAnsi(value, profile.Capabilities);
        }

        var programs = profile.Programs;
        var attributes = value.Attributes;
        attributes = programs.Has("bold") ? attributes : attributes & ~TerminalAttributes.Bold;
        attributes = programs.Has("dim") ? attributes : attributes & ~TerminalAttributes.Dim;
        attributes = programs.Has("sitm") ? attributes : attributes & ~TerminalAttributes.Italic;
        if (programs.Has("blink"))
        {
            if ((attributes & TerminalAttributes.RapidBlink) != 0)
            {
                attributes = (attributes & ~TerminalAttributes.RapidBlink) | TerminalAttributes.Blink;
            }
        }
        else
        {
            attributes &= ~(TerminalAttributes.Blink | TerminalAttributes.RapidBlink);
        }
        attributes = programs.Has("rev") ? attributes : attributes & ~TerminalAttributes.Reverse;
        attributes = programs.Has("invis") ? attributes : attributes & ~TerminalAttributes.Hidden;
        attributes = programs.Has("smxx") ? attributes : attributes & ~TerminalAttributes.Strike;
        attributes = programs.Has("Smol") && profile.Capabilities.Overline.Authoritative
            ? attributes
            : attributes & ~TerminalAttributes.Overline;
        var underline = value.Underline;

        if (underline != Underline.None &&
            (!profile.Capabilities.StyledUnderlines.Authoritative || !programs.Has("Smulx")))
        {
            attributes |= TerminalAttributes.Underline;
            underline = Underline.None;
        }

        if (underline == Underline.None && !programs.Has("smul"))
        {
            attributes &= ~TerminalAttributes.Underline;
        }

        var hasUnderline = (attributes & TerminalAttributes.Underline) != 0 || underline != Underline.None;
        var underlineColor = hasUnderline &&
            profile.Capabilities.UnderlineColor.Authoritative &&
            programs.Has("Setulc")
                ? TerminalPalette.Project(value.UnderlineColor, profile.RenderingColorDepth)
                : Color.Default;
        return new CellStyle(
            TerminalPalette.Project(value.Foreground, profile.RenderingColorDepth),
            TerminalPalette.Project(value.Background, profile.RenderingColorDepth),
            attributes,
            value.Hyperlink,
            underline,
            underlineColor);
    }

    private static void ApplyAnsiStyle(
        IBufferWriter<byte> destination,
        CellStyle current,
        CellStyle target,
        TerminalCapabilities capabilities)
    {
        var writer = new ProtocolWriter(destination);

        if (!string.Equals(current.Hyperlink, target.Hyperlink, StringComparison.Ordinal))
        {
            if (current.Hyperlink is not null)
            {
                Osc.CloseHyperlink(writer);
            }

            if (target.Hyperlink is not null)
            {
                OpenHyperlink(writer, target.Hyperlink);
            }
        }

        if (current.Attributes == target.Attributes &&
            current.Foreground == target.Foreground &&
            current.Background == target.Background &&
            current.Underline == target.Underline &&
            current.UnderlineColor == target.UnderlineColor)
        {
            return;
        }

        if (!IsVisualDefault(current))
        {
            Sgr.Reset(writer);
        }

        ApplyAnsiAttribute(writer, target.Attributes, TerminalAttributes.Bold, Rendition.Bold);
        ApplyAnsiAttribute(writer, target.Attributes, TerminalAttributes.Dim, Rendition.Dim);
        ApplyAnsiAttribute(writer, target.Attributes, TerminalAttributes.Italic, Rendition.Italic);
        ApplyAnsiAttribute(writer, target.Attributes, TerminalAttributes.Blink, Rendition.SlowBlink);
        ApplyAnsiAttribute(writer, target.Attributes, TerminalAttributes.RapidBlink, Rendition.RapidBlink);
        ApplyAnsiAttribute(writer, target.Attributes, TerminalAttributes.Reverse, Rendition.Reverse);
        ApplyAnsiAttribute(writer, target.Attributes, TerminalAttributes.Hidden, Rendition.Hidden);
        ApplyAnsiAttribute(writer, target.Attributes, TerminalAttributes.Strike, Rendition.Strike);
        ApplyAnsiAttribute(writer, target.Attributes, TerminalAttributes.Overline, Rendition.Overline);

        if ((target.Attributes & TerminalAttributes.Underline) != 0)
        {
            Sgr.Apply(writer, Rendition.Underline);
        }
        else if (target.Underline != Underline.None)
        {
            Sgr.Apply(writer, target.Underline);
        }

        ApplyColor(writer, target.Foreground, capabilities, foreground: true);
        ApplyColor(writer, target.Background, capabilities, foreground: false);

        if (target.UnderlineColor != Color.Default)
        {
            ApplyUnderlineColor(writer, target.UnderlineColor, capabilities);
        }
    }

    private static void ApplyColor(
        ProtocolWriter writer,
        Color color,
        TerminalCapabilities capabilities,
        bool foreground)
    {
        if (color == Color.Default)
        {
            return;
        }

        if (capabilities.ColorDepth == ColorDepth.Basic16)
        {
            var basic = (BasicColor) TerminalPalette.FindPosition(color, capabilities.ColorDepth);

            if (foreground)
            {
                Sgr.Foreground(writer, basic);
            }
            else
            {
                Sgr.Background(writer, basic);
            }

            return;
        }

        if (capabilities.ColorDepth == ColorDepth.Indexed256)
        {
            var position = TerminalPalette.FindPosition(color, capabilities.ColorDepth);

            if (foreground)
            {
                Sgr.ForegroundPalette(writer, position);
            }
            else
            {
                Sgr.BackgroundPalette(writer, position);
            }

            return;
        }

        if (foreground)
        {
            Sgr.Foreground(writer, color);
        }
        else
        {
            Sgr.Background(writer, color);
        }
    }

    private static void ApplyUnderlineColor(
        ProtocolWriter writer,
        Color color,
        TerminalCapabilities capabilities)
    {
        if (capabilities.ColorDepth is ColorDepth.Basic16 or ColorDepth.Indexed256)
        {
            Sgr.UnderlineColorPalette(writer, TerminalPalette.FindPosition(color, capabilities.ColorDepth));
            return;
        }

        Sgr.UnderlineColor(writer, color);
    }

    private static void ApplyAnsiAttribute(
        ProtocolWriter writer,
        TerminalAttributes attributes,
        TerminalAttributes value,
        Rendition rendition)
    {
        if ((attributes & value) != 0)
        {
            Sgr.Apply(writer, rendition);
        }
    }

    private static CellStyle ProjectAnsi(CellStyle value, TerminalCapabilities capabilities)
    {
        var attributes = capabilities.Overline.Authoritative
            ? value.Attributes
            : value.Attributes & ~TerminalAttributes.Overline;
        var underline = value.Underline;

        if (underline != Underline.None && !capabilities.StyledUnderlines.Authoritative)
        {
            attributes |= TerminalAttributes.Underline;
            underline = Underline.None;
        }

        var underlineColor = capabilities.UnderlineColor.Authoritative
            ? TerminalPalette.Project(value.UnderlineColor, capabilities.ColorDepth)
            : Color.Default;
        return new CellStyle(
            TerminalPalette.Project(value.Foreground, capabilities.ColorDepth),
            TerminalPalette.Project(value.Background, capabilities.ColorDepth),
            attributes,
            value.Hyperlink,
            underline,
            underlineColor);
    }

    private static bool IsVisualDefault(CellStyle style) =>
        style.Attributes == TerminalAttributes.None &&
        style.Foreground == Color.Default &&
        style.Background == Color.Default &&
        style.Underline == Underline.None &&
        style.UnderlineColor == Color.Default;

    private static int Packed(Color color)
    {
        Debug.Assert(color.IsRgb, "Resolved indexed colors are RGB values.");
        return (color.Red << 16) | (color.Green << 8) | color.Blue;
    }

    private static void WriteRequired(
        TerminalProfile profile,
        Interpreter interpreter,
        IBufferWriter<byte> destination,
        string name,
        params ReadOnlySpan<int> parameters)
    {
        if (!profile.Programs.TryWrite(name, parameters, interpreter, destination))
        {
            throw new InvalidOperationException($"Terminal description program '{name}' is required for rendering.");
        }
    }

    private static void OpenHyperlink(ProtocolWriter writer, string hyperlink)
    {
        var byteCount = Encoding.UTF8.GetByteCount(hyperlink);
        var rented = byteCount > _stackLinkBytes ? ArrayPool<byte>.Shared.Rent(byteCount) : null;
        var bytes = rented is null ? stackalloc byte[byteCount] : rented.AsSpan(0, byteCount);

        try
        {
            var written = Encoding.UTF8.GetBytes(hyperlink.AsSpan(), bytes);
            Osc.OpenHyperlink(writer, bytes[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }
        }
    }
}
