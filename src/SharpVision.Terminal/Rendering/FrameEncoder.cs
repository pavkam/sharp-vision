// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

using Capabilities;

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
        bool full = false)
    {
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(interpreter);
        back.ThrowIfDisposed();
        front?.ThrowIfDisposed();
        var redraw = full || front is null || front.Size != back.Size;
        var semanticStyle = CellStyle.Default;
        var style = CellStyle.Default;
        var spanCount = 0;

        if (redraw)
        {
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

        foreach (var span in Damage.Enumerate(front, back, redraw))
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
                    ref style))
            {
                continue;
            }

            for (var column = span.Start; column < end; column++)
            {
                var index = checked((span.Row * back.Size.Width) + column);
                var cell = back.GetCellByIndex(index);

                if (cell.IsContinuation)
                {
                    continue;
                }

                var projected = cell.Style == semanticStyle
                    ? style
                    : Project(cell.Style, profile);
                style = ApplyStyle(destination, style, projected, profile, interpreter);
                semanticStyle = cell.Style;
                var grapheme = back.GetGrapheme(index);
                destination.Write(grapheme.IsEmpty ? " "u8 : grapheme);
            }

            // Writing the final column can leave an automatic-margin terminal in
            // delayed-wrap state. An immediate absolute position clears that state
            // before another byte can wrap or scroll, including xenl terminals.
            if (!profile.AnsiCompatible &&
                profile.Description.AutomaticMargins &&
                end == back.Size.Width &&
                end > 0)
            {
                WriteRequired(profile, interpreter, destination, "cup", span.Row, end - 1);
            }
        }

        ResetStyle(destination, style, profile, interpreter);
        var cursorChanged = redraw || front!.Cursor != back.Cursor;

        if ((spanCount > 0 || cursorChanged) && back.Size is { Width: > 0, Height: > 0 })
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

        return new EncodeResult(spanCount, redraw);
    }

    private static bool TryEraseTrailingBlanks(
        Frame back,
        DamageSpan span,
        int end,
        IBufferWriter<byte> destination,
        TerminalProfile profile,
        Interpreter interpreter,
        ref CellStyle semanticStyle,
        ref CellStyle style)
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

            if (cell.IsContinuation || !back.GetGrapheme(index).IsEmpty)
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

        if (projected.Foreground != Color.Default ||
            projected.Attributes != TerminalAttributes.None ||
            projected.Hyperlink is not null ||
            projected.Underline != Underline.None ||
            projected.UnderlineColor != Color.Default)
        {
            return false;
        }

        style = ApplyStyle(destination, style, projected, profile, interpreter);
        semanticStyle = semantic.Value;
        return profile.Programs.TryWrite("el", [], interpreter, destination);
    }

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

        var underlineColor = profile.Capabilities.UnderlineColor.Authoritative && programs.Has("Setulc")
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
