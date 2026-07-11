using System.Text;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;

namespace SharpVision.Showcase.Tests;

/// <summary>Copies a terminal frame into a deterministic text and continuation-cell test oracle.</summary>
internal sealed class Screen
{
    private readonly Frame _frame;

    /// <summary>Initializes a copied textual view over a live non-null frame.</summary>
    /// <param name="frame">The non-null frame retained for semantic cell validation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
    internal Screen(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _frame = frame;
        Text = CopyText(frame);
    }

    /// <summary>Gets newline-separated graphemes with blanks preserved as spaces.</summary>
    internal string Text { get; }

    /// <summary>Counts non-overlapping ordinal occurrences in the copied text.</summary>
    /// <param name="value">The non-empty value to count.</param>
    /// <returns>The number of non-overlapping occurrences.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
    internal int Count(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        var count = 0;
        var offset = 0;

        while ((offset = Text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    /// <summary>Throws when a continuation does not point to the preceding width-two lead cell.</summary>
    /// <exception cref="InvalidDataException">A continuation relationship is structurally invalid.</exception>
    internal void ValidateContinuations()
    {
        for (var y = 0; y < _frame.Size.Height; y++)
        {
            for (var x = 0; x < _frame.Size.Width; x++)
            {
                var point = new Point(x, y);
                var cell = _frame.GetCell(point);

                if (!cell.IsContinuation)
                {
                    continue;
                }

                if (cell.Lead.Y != y || cell.Lead.X != x - 1)
                {
                    throw new InvalidDataException($"Continuation {point} has invalid lead {cell.Lead}.");
                }

                var lead = _frame.GetCell(cell.Lead);

                if (lead.IsContinuation || lead.Width != 2)
                {
                    throw new InvalidDataException($"Lead {cell.Lead} is not a width-two grapheme.");
                }
            }
        }
    }

    private static string CopyText(Frame frame)
    {
        var text = new StringBuilder();

        for (var y = 0; y < frame.Size.Height; y++)
        {
            if (y > 0)
            {
                _ = text.Append('\n');
            }

            for (var x = 0; x < frame.Size.Width; x++)
            {
                var point = new Point(x, y);
                var cell = frame.GetCell(point);

                if (cell.IsContinuation)
                {
                    continue;
                }

                var length = frame.GetGraphemeByteCount(point);

                if (length == 0)
                {
                    _ = text.Append(' ');
                    continue;
                }

                var bytes = new byte[length];
                _ = frame.CopyGrapheme(point, bytes);
                _ = text.Append(Encoding.UTF8.GetString(bytes));
            }
        }

        return text.ToString();
    }
}
