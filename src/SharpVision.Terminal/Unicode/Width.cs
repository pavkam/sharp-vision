namespace SharpVision.Terminal.Unicode;

using System.Buffers;
using System.Diagnostics;
using System.Text;

/// <summary>
/// Measures Unicode 17 extended grapheme clusters in terminal cells.
/// </summary>
public static class Width
{
    /// <summary>Measures a borrowed UTF-16 span under explicit width policy.</summary>
    /// <param name="value">The borrowed UTF-16 text.</param>
    /// <param name="ambiguous">The East Asian Ambiguous width policy.</param>
    /// <returns>Printable cells, graphemes, and contextual controls.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="ambiguous"/> is unknown.
    /// </exception>
    public static Measurement Measure(
        ReadOnlySpan<char> value,
        Ambiguous ambiguous = Ambiguous.Narrow)
    {
        Validate(ambiguous);
        var cells = 0;
        var graphemes = 0;
        var controls = 0;

        foreach (var segment in Graphemes.Enumerate(value))
        {
            var cluster = AnalyzeCluster(
                value.Slice(segment.Offset, segment.Length),
                ambiguous,
                segment.HasInvalidData);
            graphemes = checked(graphemes + 1);

            if (cluster.Width == CellWidth.Control)
            {
                controls = checked(controls + 1);
            }
            else
            {
                cells = checked(cells + (int) cluster.Width);
            }
        }

        return new Measurement(cells, graphemes, controls);
    }

    /// <summary>Measures one already-segmented cluster without allocation.</summary>
    /// <param name="value">Exactly one borrowed extended grapheme cluster.</param>
    /// <param name="ambiguous">The validated ambiguous-width policy.</param>
    /// <param name="hasInvalidData">Whether the cluster contains replacement input.</param>
    /// <returns>The cluster width.</returns>
    internal static CellWidth GetCluster(
        ReadOnlySpan<char> value,
        Ambiguous ambiguous,
        bool hasInvalidData = false) =>
        AnalyzeCluster(value, ambiguous, hasInvalidData).Width;

    /// <summary>Classifies one already-segmented cluster without allocation.</summary>
    /// <param name="value">Exactly one borrowed extended grapheme cluster.</param>
    /// <param name="ambiguous">The validated ambiguous-width policy.</param>
    /// <param name="hasInvalidData">Whether the cluster contains replacement input.</param>
    /// <returns>The cluster width and safe presentation classification.</returns>
    internal static Cluster AnalyzeCluster(
        ReadOnlySpan<char> value,
        Ambiguous ambiguous,
        bool hasInvalidData = false)
    {
        Debug.Assert(!value.IsEmpty, "An enumerated grapheme cannot be empty.");
        Debug.Assert(Enum.IsDefined(ambiguous), "Public measurement validates width policy.");

        if (hasInvalidData)
        {
            return new Cluster(CellWidth.Narrow, requiresReplacement: true);
        }

        var baseScalar = -1;
        var hasTextSelector = false;
        var hasEmojiSelector = false;
        var hasEmojiPresentation = false;
        var hasExtendedPictographic = false;
        var hasZwj = false;
        var hasKeycap = false;
        var position = 0;

        while (position < value.Length)
        {
            var status = Rune.DecodeFromUtf16(value[position..], out var rune, out var consumed);

            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            var scalar = rune.Value;
            var graphemeBreak = Data.GetGraphemeBreak(scalar);

            if (graphemeBreak is GraphemeBreak.Control or GraphemeBreak.Cr or GraphemeBreak.Lf)
            {
                return new Cluster(CellWidth.Control, requiresReplacement: false);
            }

            hasTextSelector |= scalar == 0xfe0e;
            hasEmojiSelector |= scalar == 0xfe0f;
            hasEmojiPresentation |= Data.IsEmojiPresentation(scalar);
            hasExtendedPictographic |= Data.IsExtendedPictographic(scalar);
            hasZwj |= graphemeBreak == GraphemeBreak.Zwj;
            hasKeycap |= scalar == 0x20e3 && IsKeycapBase(baseScalar);

            if (baseScalar < 0 && graphemeBreak is not (
                GraphemeBreak.Extend or
                GraphemeBreak.Zwj or
                GraphemeBreak.SpacingMark or
                GraphemeBreak.Prepend))
            {
                baseScalar = Data.GetCanonicalBase(scalar);
            }

            position += consumed;
        }

        if (baseScalar < 0)
        {
            return new Cluster(CellWidth.Narrow, requiresReplacement: true);
        }

        if (!Data.IsAssigned(baseScalar) || IsPrivateUse(baseScalar))
        {
            return new Cluster(CellWidth.Narrow, requiresReplacement: false);
        }

        var eastAsianWidth = Data.GetEastAsianWidth(baseScalar);

        var width = eastAsianWidth is EastAsianWidth.Wide or EastAsianWidth.Fullwidth
            ? CellWidth.Wide
            : hasTextSelector
            ? CellWidth.Narrow
            : hasEmojiSelector ||
            hasKeycap ||
            hasEmojiPresentation ||
            (hasExtendedPictographic && hasZwj)
            ? CellWidth.Wide
            : eastAsianWidth == EastAsianWidth.Ambiguous && ambiguous == Ambiguous.Wide
            ? CellWidth.Wide
            : CellWidth.Narrow;
        return new Cluster(width, requiresReplacement: false);
    }

    private static bool IsKeycapBase(int scalar) => scalar is
        (>= '0' and <= '9') or '#' or '*';

    private static bool IsPrivateUse(int scalar) => scalar is
        (>= 0xe000 and <= 0xf8ff) or
        (>= 0xf0000 and <= 0xffffd) or
        (>= 0x100000 and <= 0x10fffd);

    private static void Validate(Ambiguous value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The ambiguous-width policy is unknown.");
        }
    }
}
