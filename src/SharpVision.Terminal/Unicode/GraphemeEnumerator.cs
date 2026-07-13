namespace SharpVision.Terminal.Unicode;

using System.Buffers;
using System.Diagnostics;
using System.Text;

/// <summary>Segments borrowed UTF-16 text using Unicode 17 extended-grapheme rules.</summary>
public ref struct GraphemeEnumerator
{
    private readonly ReadOnlySpan<char> _value;
    private int _position;

    /// <summary>Initializes an enumerator positioned before the first cluster.</summary>
    /// <param name="value">The borrowed UTF-16 text.</param>
    internal GraphemeEnumerator(ReadOnlySpan<char> value)
    {
        _value = value;
        _position = 0;
        Current = default;
    }

    /// <summary>Gets the current borrowed segment coordinates.</summary>
    public Grapheme Current { get; private set; }

    /// <summary>Advances to the next extended grapheme cluster.</summary>
    /// <returns><see langword="true"/> when a cluster is available.</returns>
    public bool MoveNext()
    {
        if (_position >= _value.Length)
        {
            return false;
        }

        var start = _position;
        Decode(_position, out var previous, out var consumed, out var invalid);
        var hasInvalidData = invalid;
        var previousBreak = Data.GetGraphemeBreak(previous.Value);
        var regionalIndicators = previousBreak == GraphemeBreak.RegionalIndicator ? 1 : 0;
        var extendedPictographicCandidate = Data.IsExtendedPictographic(previous.Value);
        var previousZwjAfterPictographic = false;
        var indicState = NextIndicState(0, Data.GetIndicConjunct(previous.Value));
        _position += consumed;

        while (_position < _value.Length)
        {
            Decode(_position, out var current, out consumed, out invalid);
            var currentBreak = Data.GetGraphemeBreak(current.Value);
            var currentIndic = Data.GetIndicConjunct(current.Value);
            var currentPictographic = Data.IsExtendedPictographic(current.Value);

            if (ShouldBreak(
                previousBreak,
                currentBreak,
                currentIndic,
                currentPictographic,
                regionalIndicators,
                indicState,
                previousZwjAfterPictographic))
            {
                break;
            }

            hasInvalidData |= invalid;
            regionalIndicators = currentBreak == GraphemeBreak.RegionalIndicator
                ? regionalIndicators + 1
                : 0;
            indicState = NextIndicState(indicState, currentIndic);

            // GB11 needs to know whether the immediately preceding ZWJ was
            // itself preceded by Extended_Pictographic Extend*.
            previousZwjAfterPictographic = currentBreak == GraphemeBreak.Zwj &&
                extendedPictographicCandidate;

            if (currentPictographic)
            {
                extendedPictographicCandidate = true;
            }
            else if (currentBreak != GraphemeBreak.Extend)
            {
                extendedPictographicCandidate = false;
            }

            previousBreak = currentBreak;
            _position += consumed;
        }

        var length = _position - start;
        Debug.Assert(length > 0, "Every grapheme segment must consume UTF-16 input.");
        Debug.Assert(_position <= _value.Length, "A grapheme cannot exceed its source span.");
        Current = new Grapheme(start, length, hasInvalidData);

        return true;
    }

    private static int NextIndicState(int state, IndicConjunct value) => value switch
    {
        IndicConjunct.Consonant => 1,
        IndicConjunct.Linker => state > 0 ? 2 : 0,
        IndicConjunct.Extend => state,
        IndicConjunct.None => 0,
        _ => throw new UnreachableException(),
    };

    private static bool ShouldBreak(
        GraphemeBreak previous,
        GraphemeBreak current,
        IndicConjunct currentIndic,
        bool currentPictographic,
        int regionalIndicators,
        int indicState,
        bool previousZwjAfterPictographic)
    {
        // GB3 through GB5 keep CR/LF together and isolate every other control.
        if (previous == GraphemeBreak.Cr && current == GraphemeBreak.Lf)
        {
            return false;
        }

        if (IsControl(previous) || IsControl(current))
        {
            return true;
        }

        // GB6 through GB8 keep algorithmic Hangul syllable sequences whole.
        if (previous == GraphemeBreak.L && current is
            GraphemeBreak.L or GraphemeBreak.V or GraphemeBreak.Lv or GraphemeBreak.Lvt)
        {
            return false;
        }

        if (previous is GraphemeBreak.Lv or GraphemeBreak.V && current is
            GraphemeBreak.V or GraphemeBreak.T)
        {
            return false;
        }

        if (previous is GraphemeBreak.Lvt or GraphemeBreak.T && current == GraphemeBreak.T)
        {
            return false;
        }

        // GB9, GB9a, and GB9b attach extenders, joiners, spacing marks, and
        // prepend characters without normalizing or allocating.
        if (current is GraphemeBreak.Extend or GraphemeBreak.Zwj or GraphemeBreak.SpacingMark ||
            previous == GraphemeBreak.Prepend)
        {
            return false;
        }

        // GB9c joins an Indic consonant after a consonant/linker suffix.
        if (currentIndic == IndicConjunct.Consonant && indicState == 2)
        {
            return false;
        }

        // GB11 joins an extended pictograph after ExtPict Extend* ZWJ.
        if (previous == GraphemeBreak.Zwj &&
            previousZwjAfterPictographic &&
            currentPictographic)
        {
            return false;
        }

        // GB12 and GB13 pair regional indicators from the start of each run.
        return !(previous == GraphemeBreak.RegionalIndicator &&
            current == GraphemeBreak.RegionalIndicator &&
            regionalIndicators % 2 == 1);
    }

    private static bool IsControl(GraphemeBreak value) => value is
        GraphemeBreak.Control or GraphemeBreak.Cr or GraphemeBreak.Lf;

    private readonly void Decode(
        int offset,
        out Rune rune,
        out int consumed,
        out bool invalid)
    {
        var status = Rune.DecodeFromUtf16(_value[offset..], out rune, out consumed);
        invalid = status != OperationStatus.Done;

        if (invalid)
        {
            rune = Rune.ReplacementChar;
            consumed = 1;
        }
    }
}
