using System.Buffers;
using System.Text;

using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Reports one completed in-memory frame encoding operation.
/// </summary>
/// <param name="Spans">The number of damage spans encoded.</param>
/// <param name="Full">Whether the target was encoded as a full redraw.</param>
public readonly record struct EncodeResult(int Spans, bool Full);

/// <summary>
/// Encodes semantic frame damage into deterministic terminal control bytes.
/// </summary>
public static class Encoder
{
    private const int _stackLinkBytes = 512;

    /// <summary>Encodes full or incremental target state to a byte writer.</summary>
    /// <param name="front">The committed frame, or null for a full redraw.</param>
    /// <param name="back">The target semantic frame.</param>
    /// <param name="destination">The synchronous byte destination.</param>
    /// <param name="full">Whether to force a full redraw.</param>
    /// <returns>The number of spans and full/incremental classification.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="back"/> or <paramref name="destination"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">A supplied frame is disposed.</exception>
    public static EncodeResult Encode(
        Frame? front,
        Frame back,
        IBufferWriter<byte> destination,
        bool full = false)
    {
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(destination);
        back.ThrowIfDisposed();
        front?.ThrowIfDisposed();
        var redraw = full || front is null || front.Size != back.Size;
        var writer = new Writer(destination);
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

                ApplyStyle(writer, style, cell.Style);
                style = cell.Style;
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

    private static void ApplyStyle(Writer writer, Style current, Style target)
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
            Sgr.Foreground(writer, target.Foreground);
        }

        if (target.Background != Color.Default)
        {
            Sgr.Background(writer, target.Background);
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
