// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using System.Text;

using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

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
        int count = 0;
        int offset = 0;

        while ((offset = Text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    /// <summary>Gets whether the copied frame contains an explicit foreground or background color.</summary>
    internal bool HasNonDefaultColor()
    {
        for (int y = 0; y < _frame.Size.Height; y++)
        {
            for (int x = 0; x < _frame.Size.Width; x++)
            {
                CellInfo cell = _frame.GetCell(new Point(x, y));

                if (cell.Style.Foreground != Color.Default || cell.Style.Background != Color.Default)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Throws when a continuation does not point to the preceding width-two lead cell.</summary>
    /// <exception cref="InvalidDataException">A continuation relationship is structurally invalid.</exception>
    internal void ValidateContinuations()
    {
        for (int y = 0; y < _frame.Size.Height; y++)
        {
            for (int x = 0; x < _frame.Size.Width; x++)
            {
                Point point = new(x, y);
                CellInfo cell = _frame.GetCell(point);

                if (!cell.IsContinuation)
                {
                    continue;
                }

                if (cell.Lead.Y != y || cell.Lead.X != x - 1)
                {
                    throw new InvalidDataException($"Continuation {point} has invalid lead {cell.Lead}.");
                }

                CellInfo lead = _frame.GetCell(cell.Lead);

                if (lead.IsContinuation || lead.Width != 2)
                {
                    throw new InvalidDataException($"Lead {cell.Lead} is not a width-two grapheme.");
                }
            }
        }
    }

    private static string CopyText(Frame frame)
    {
        StringBuilder text = new();

        for (int y = 0; y < frame.Size.Height; y++)
        {
            if (y > 0)
            {
                _ = text.Append('\n');
            }

            for (int x = 0; x < frame.Size.Width; x++)
            {
                Point point = new(x, y);
                CellInfo cell = frame.GetCell(point);

                if (cell.IsContinuation)
                {
                    continue;
                }

                int length = frame.GetGraphemeByteCount(point);

                if (length == 0)
                {
                    _ = text.Append(' ');
                    continue;
                }

                byte[] bytes = new byte[length];
                _ = frame.CopyGrapheme(point, bytes);
                _ = text.Append(Encoding.UTF8.GetString(bytes));
            }
        }

        return text.ToString();
    }
}
