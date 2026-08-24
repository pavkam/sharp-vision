// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

using SharpVision.Controls.Display;
using SharpVision.Controls.Input;
using SharpVision.Controls.Scrolling;

/// <summary>Defines one theme-wide glyph personality shared by CheckBox, RadioButton, ScrollBar,
/// Spinner, ProgressBar, and ChaseIndicator. A theme document selects one family by name through
/// its root-level "glyphs" field; an absent field resolves every one of those six styles to
/// <see cref="Default"/>, the exact code-owned presentation each carried before this type
/// existed.</summary>
[PublicAPI]
public sealed record GlyphFamily
{
    /// <summary>Initializes a complete glyph family.</summary>
    /// <param name="checkBox">The CheckBox mark style and glyph trio.</param>
    /// <param name="radioButton">The RadioButton mark style and glyph pair.</param>
    /// <param name="scrollBar">The ScrollBar chrome, fill, and ten-glyph set.</param>
    /// <param name="spinner">The non-empty sequence of printable one-cell spinner frames.</param>
    /// <param name="progressBar">The ProgressBar fill, track, and indeterminate glyph trio.</param>
    /// <param name="chaseIndicator">The ChaseIndicator active and inactive glyph pair.</param>
    /// <exception cref="ArgumentNullException"><paramref name="spinner"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="spinner"/> is empty, exceeds <see cref="SpinnerStyle.MaximumFrameCount"/>, or contains a control or non-one-cell glyph.</exception>
    [SetsRequiredMembers]
    public GlyphFamily(
        CheckBoxGlyphFamily checkBox,
        RadioButtonGlyphFamily radioButton,
        ScrollBarGlyphFamily scrollBar,
        IEnumerable<Rune> spinner,
        ProgressBarGlyphs progressBar,
        ChaseIndicatorGlyphs chaseIndicator)
    {
        CheckBox = checkBox;
        RadioButton = radioButton;
        ScrollBar = scrollBar;
        Spinner = SpinnerStyle.CopyFrames(spinner, nameof(spinner));
        ProgressBar = progressBar;
        ChaseIndicator = chaseIndicator;
    }

    /// <summary>Gets the code-owned family: the exact CheckBox, RadioButton, ScrollBar, Spinner,
    /// ProgressBar, and ChaseIndicator presentation each carried before glyph families existed.
    /// Every theme that omits the root-level "glyphs" field, including both zero-config
    /// defaults, resolves to this family.</summary>
    /// <remarks>
    /// CheckBox's and RadioButton's glyph trio/pair here are NOT
    /// <see cref="CheckBoxGlyphs.Default"/>/<see cref="RadioButtonGlyphs.Default"/> - those back
    /// the one-cell Square/Circle presentations. <see cref="CheckBoxStyle.Brackets"/> and
    /// <see cref="RadioButtonStyle.Parentheses"/> - the code-owned defaults this family
    /// reproduces - pair the three-cell bracket/parenthesis layout with a distinct blank/checkmark
    /// glyph set instead; confusing the two is what regressed the resolved defaults to "[☐]"/"(○)"
    /// once before (see <c>CuratedThemesTests</c>).
    /// </remarks>
    public static GlyphFamily Default { get; } = new(
        new CheckBoxGlyphFamily(CheckBoxMarkStyle.Brackets, new CheckBoxGlyphs(new Rune(' '), new Rune('✓'), new Rune('─'))),
        new RadioButtonGlyphFamily(RadioButtonMarkStyle.Parentheses, new RadioButtonGlyphs(new Rune(' '), new Rune('•'))),
        new ScrollBarGlyphFamily(ScrollBarChrome.Full, ScrollBarFill.Block, ScrollBarGlyphs.Default),
        [
            new Rune('⠋'), new Rune('⠙'), new Rune('⠹'), new Rune('⠸'), new Rune('⠼'),
            new Rune('⠴'), new Rune('⠦'), new Rune('⠧'), new Rune('⠇'), new Rune('⠏')
        ],
        ProgressBarGlyphs.Default,
        new ChaseIndicatorGlyphs(new Rune('●'), new Rune('◯')));

    /// <summary>Gets the round, dotted family extracted from the Catppuccin themes.</summary>
    public static GlyphFamily Dots { get; } = new(
        new CheckBoxGlyphFamily(CheckBoxMarkStyle.Brackets, new CheckBoxGlyphs(new Rune('○'), new Rune('●'), new Rune('◐'))),
        new RadioButtonGlyphFamily(RadioButtonMarkStyle.Circle, new RadioButtonGlyphs(new Rune('○'), new Rune('●'))),
        new ScrollBarGlyphFamily(
            ScrollBarChrome.Full,
            ScrollBarFill.Line,
            new ScrollBarGlyphs(
                new Rune('▲'), new Rune('▼'), new Rune('◀'), new Rune('▶'),
                new Rune('·'), new Rune('●'),
                new Rune('╌'), new Rune('━'), new Rune('╎'), new Rune('┃'))),
        [
            new Rune('⣿'), new Rune('⣷'), new Rune('⣯'), new Rune('⣟'),
            new Rune('⡿'), new Rune('⢿'), new Rune('⣻'), new Rune('⣽')
        ],
        new ProgressBarGlyphs(new Rune('●'), new Rune('·'), new Rune('◌')),
        new ChaseIndicatorGlyphs(new Rune('◉'), new Rune('·')));

    /// <summary>Gets the solid, blocky family extracted from the Dracula and One Dark themes.</summary>
    public static GlyphFamily Blocks { get; } = new(
        new CheckBoxGlyphFamily(CheckBoxMarkStyle.Square, new CheckBoxGlyphs(new Rune('☐'), new Rune('☒'), new Rune('■'))),
        new RadioButtonGlyphFamily(RadioButtonMarkStyle.Circle, new RadioButtonGlyphs(new Rune('○'), new Rune('●'))),
        new ScrollBarGlyphFamily(
            ScrollBarChrome.Full,
            ScrollBarFill.Block,
            new ScrollBarGlyphs(
                new Rune('▲'), new Rune('▼'), new Rune('◀'), new Rune('▶'),
                new Rune('▒'), new Rune('█'),
                new Rune('━'), new Rune('━'), new Rune('┃'), new Rune('┃'))),
        [
            new Rune('⣿'), new Rune('⣷'), new Rune('⣯'), new Rune('⣟'),
            new Rune('⡿'), new Rune('⢿'), new Rune('⣻'), new Rune('⣽')
        ],
        new ProgressBarGlyphs(new Rune('█'), new Rune('▒'), new Rune('▓')),
        new ChaseIndicatorGlyphs(new Rune('█'), new Rune('▒')));

    /// <summary>Gets the portable ASCII family extracted from the Gruvbox themes.</summary>
    public static GlyphFamily Ascii { get; } = new(
        new CheckBoxGlyphFamily(CheckBoxMarkStyle.Brackets, new CheckBoxGlyphs(new Rune('.'), new Rune('X'), new Rune('-'))),
        new RadioButtonGlyphFamily(RadioButtonMarkStyle.Parentheses, new RadioButtonGlyphs(new Rune('.'), new Rune('*'))),
        new ScrollBarGlyphFamily(
            ScrollBarChrome.Thin,
            ScrollBarFill.Line,
            new ScrollBarGlyphs(
                new Rune('^'), new Rune('v'), new Rune('<'), new Rune('>'),
                new Rune('.'), new Rune('#'),
                new Rune('-'), new Rune('='), new Rune('|'), new Rune('#'))),
        [new Rune('|'), new Rune('/'), new Rune('-'), new Rune('\\')],
        new ProgressBarGlyphs(new Rune('#'), new Rune('.'), new Rune('?')),
        new ChaseIndicatorGlyphs(new Rune('*'), new Rune('.')));

    /// <summary>Gets the shaded family extracted from the Monokai and Tokyo Night themes.</summary>
    public static GlyphFamily Shades { get; } = new(
        new CheckBoxGlyphFamily(CheckBoxMarkStyle.Square, new CheckBoxGlyphs(new Rune('☐'), new Rune('☑'), new Rune('▪'))),
        new RadioButtonGlyphFamily(RadioButtonMarkStyle.Circle, new RadioButtonGlyphs(new Rune('○'), new Rune('◉'))),
        new ScrollBarGlyphFamily(
            ScrollBarChrome.Full,
            ScrollBarFill.Block,
            new ScrollBarGlyphs(
                new Rune('▲'), new Rune('▼'), new Rune('◀'), new Rune('▶'),
                new Rune('▒'), new Rune('▓'),
                new Rune('═'), new Rune('━'), new Rune('║'), new Rune('┃'))),
        [
            new Rune('⣿'), new Rune('⣷'), new Rune('⣯'), new Rune('⣟'),
            new Rune('⡿'), new Rune('⢿'), new Rune('⣻'), new Rune('⣽')
        ],
        new ProgressBarGlyphs(new Rune('▓'), new Rune('░'), new Rune('▒')),
        new ChaseIndicatorGlyphs(new Rune('▓'), new Rune('░')));

    /// <summary>Gets the thin line-drawing family extracted from the Nord and Solarized themes.</summary>
    public static GlyphFamily Lines { get; } = new(
        new CheckBoxGlyphFamily(CheckBoxMarkStyle.Brackets, new CheckBoxGlyphs(new Rune('☐'), new Rune('☑'), new Rune('▪'))),
        new RadioButtonGlyphFamily(RadioButtonMarkStyle.Circle, new RadioButtonGlyphs(new Rune('○'), new Rune('●'))),
        new ScrollBarGlyphFamily(
            ScrollBarChrome.Thin,
            ScrollBarFill.Line,
            new ScrollBarGlyphs(
                new Rune('▲'), new Rune('▼'), new Rune('◀'), new Rune('▶'),
                new Rune('·'), new Rune('━'),
                new Rune('─'), new Rune('━'), new Rune('│'), new Rune('┃'))),
        [new Rune('-'), new Rune('\\'), new Rune('|'), new Rune('/')],
        new ProgressBarGlyphs(new Rune('━'), new Rune('─'), new Rune('┈')),
        new ChaseIndicatorGlyphs(new Rune('━'), new Rune('─')));

    /// <summary>Gets the CheckBox mark style and glyph trio.</summary>
    public required CheckBoxGlyphFamily CheckBox { get; init; }

    /// <summary>Gets the RadioButton mark style and glyph pair.</summary>
    public required RadioButtonGlyphFamily RadioButton { get; init; }

    /// <summary>Gets the ScrollBar chrome, fill, and ten-glyph set.</summary>
    public required ScrollBarGlyphFamily ScrollBar { get; init; }

    /// <summary>Gets the non-empty sequence of printable one-cell spinner frames.</summary>
    /// <exception cref="ArgumentException">The replacement sequence is empty, oversized, or contains an invalid frame.</exception>
    public required ImmutableArray<Rune> Spinner
    {
        get;
        init => field = SpinnerStyle.CopyFrames(value, nameof(value));
    }

    /// <summary>Gets the ProgressBar fill, track, and indeterminate glyph trio.</summary>
    public required ProgressBarGlyphs ProgressBar { get; init; }

    /// <summary>Gets the ChaseIndicator active and inactive glyph pair.</summary>
    public required ChaseIndicatorGlyphs ChaseIndicator { get; init; }

    /// <summary>Compares two families by <see cref="Spinner"/> <em>content</em> as well as every
    /// other member. The compiler-generated record equality would compare <see cref="Spinner"/>
    /// through <see cref="ImmutableArray{T}"/>'s own <see cref="IEquatable{T}"/>, which compares
    /// the wrapped array <em>handle</em> - see <see cref="SpinnerStyle.Equals(SpinnerStyle)"/> for
    /// the identical reasoning and the assertion failures that motivated it there.</summary>
    /// <param name="other">The family to compare against, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both sides carry the same members, comparing
    /// <see cref="Spinner"/> by content.</returns>
    public bool Equals(GlyphFamily? other) =>
        other is not null &&
        CheckBox == other.CheckBox &&
        RadioButton == other.RadioButton &&
        ScrollBar == other.ScrollBar &&
        Spinner.AsSpan().SequenceEqual(other.Spinner.AsSpan()) &&
        ProgressBar == other.ProgressBar &&
        ChaseIndicator == other.ChaseIndicator;

    /// <summary>Hashes every member, staying consistent with <see cref="Equals(GlyphFamily)"/> by
    /// hashing <see cref="Spinner"/> content rather than its array handle.</summary>
    /// <returns>A content-based hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CheckBox);
        hash.Add(RadioButton);
        hash.Add(ScrollBar);

        foreach (var frame in Spinner)
        {
            hash.Add(frame);
        }

        hash.Add(ProgressBar);
        hash.Add(ChaseIndicator);
        return hash.ToHashCode();
    }
}
