using System.Buffers;
using System.Text;

using SharpVision.Terminal.Protocols;

using TerminalCapabilities = SharpVision.Terminal.Capabilities.Capabilities;

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Encodes semantic frame damage into deterministic terminal control bytes.
/// </summary>
public static class Encoder
{
    private const int _stackLinkBytes = 512;

    /// <summary>Encodes full or incremental target state for one immutable capability snapshot.</summary>
    /// <param name="front">The committed frame, or null for a full redraw.</param>
    /// <param name="back">The target semantic frame.</param>
    /// <param name="destination">The synchronous byte destination.</param>
    /// <param name="capabilities">The non-null terminal capability snapshot.</param>
    /// <param name="full">Whether to force a full redraw.</param>
    /// <returns>The number of spans and full/incremental classification.</returns>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ObjectDisposedException">A supplied frame is disposed.</exception>
    public static EncodeResult Encode(
        Frame? front,
        Frame back,
        IBufferWriter<byte> destination,
        TerminalCapabilities capabilities,
        bool full = false)
    {
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(capabilities);
        back.ThrowIfDisposed();
        front?.ThrowIfDisposed();
        var redraw = full || front is null || front.Size != back.Size;
        var writer = new Writer(destination);
        var semanticStyle = Style.Default;
        var style = Style.Default;
        var spanCount = 0;

        foreach (var span in Damage.Enumerate(front, back, redraw))
        {
            Csi.Position(writer, span.Row + 1, span.Start + 1);
            spanCount++;
            var end = span.Start + span.Length;

            for (var column = span.Start; column < end; column++)
            {
                var index = checked((span.Row * back.Size.Width) + column);
                var cell = back.GetCell(index);

                if (cell.IsContinuation)
                {
                    continue;
                }

                var projected = cell.Style == semanticStyle
                    ? style
                    : Project(cell.Style, capabilities);
                ApplyStyle(writer, style, projected, capabilities);
                semanticStyle = cell.Style;
                style = projected;
                var grapheme = back.GetGrapheme(index);

                if (grapheme.IsEmpty)
                {
                    destination.Write(" "u8);
                }
                else
                {
                    destination.Write(grapheme);
                }
            }
        }

        ResetStyle(writer, style);
        var cursorChanged = redraw || front!.Cursor != back.Cursor;

        if ((spanCount > 0 || cursorChanged) && back.Size.Width > 0 && back.Size.Height > 0)
        {
            Csi.Position(
                writer,
                back.Cursor.Position.Y + 1,
                back.Cursor.Position.X + 1);
        }

        if (redraw || front!.Cursor.Visible != back.Cursor.Visible)
        {
            Modes.CursorVisible(writer, back.Cursor.Visible);
        }

        return new EncodeResult(spanCount, redraw);
    }

    private static void ApplyStyle(
        Writer writer,
        Style current,
        Style target,
        TerminalCapabilities capabilities)
    {
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
            current.Background == target.Background)
        {
            return;
        }

        if (!IsVisualDefault(current))
        {
            Sgr.Reset(writer);
        }

        ApplyAttributes(writer, target.Attributes);

        if (target.Foreground != Color.Default)
        {
            ApplyColor(writer, target.Foreground, capabilities, foreground: true);
        }

        if (target.Background != Color.Default)
        {
            ApplyColor(writer, target.Background, capabilities, foreground: false);
        }
    }

    private static void ApplyColor(
        Writer writer,
        Color color,
        TerminalCapabilities capabilities,
        bool foreground)
    {
        if (capabilities.ColorDepth == Capabilities.ColorDepth.Basic16)
        {
            var basic = (BasicColor) color.Red;

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

        if (foreground)
        {
            Sgr.Foreground(writer, color);
        }
        else
        {
            Sgr.Background(writer, color);
        }
    }

    private static void ApplyAttributes(Writer writer, Attributes attributes)
    {
        ApplyAttribute(writer, attributes, Attributes.Bold, Rendition.Bold);
        ApplyAttribute(writer, attributes, Attributes.Dim, Rendition.Dim);
        ApplyAttribute(writer, attributes, Attributes.Italic, Rendition.Italic);
        ApplyAttribute(writer, attributes, Attributes.Underline, Rendition.Underline);
        ApplyAttribute(writer, attributes, Attributes.Blink, Rendition.SlowBlink);
        ApplyAttribute(writer, attributes, Attributes.Reverse, Rendition.Reverse);
        ApplyAttribute(writer, attributes, Attributes.Hidden, Rendition.Hidden);
        ApplyAttribute(writer, attributes, Attributes.Strike, Rendition.Strike);
    }

    private static void ApplyAttribute(
        Writer writer,
        Attributes attributes,
        Attributes value,
        Rendition rendition)
    {
        if ((attributes & value) != 0)
        {
            Sgr.Apply(writer, rendition);
        }
    }

    private static void ResetStyle(Writer writer, Style style)
    {
        if (style.Hyperlink is not null)
        {
            Osc.CloseHyperlink(writer);
        }

        if (!IsVisualDefault(style))
        {
            Sgr.Reset(writer);
        }
    }

    private static bool IsVisualDefault(Style style) =>
        style.Attributes == Attributes.None &&
        style.Foreground == Color.Default &&
        style.Background == Color.Default;

    private static Style Project(Style value, TerminalCapabilities capabilities) => new(
        Palette.Project(value.Foreground, capabilities.ColorDepth),
        Palette.Project(value.Background, capabilities.ColorDepth),
        value.Attributes,
        value.Hyperlink);

    private static void OpenHyperlink(Writer writer, string hyperlink)
    {
        var byteCount = Encoding.UTF8.GetByteCount(hyperlink);
        var rented = byteCount > _stackLinkBytes
            ? ArrayPool<byte>.Shared.Rent(byteCount)
            : null;
        var bytes = rented is null
            ? stackalloc byte[byteCount]
            : rented.AsSpan(0, byteCount);

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
