namespace SharpVision.Fonts;

using System.Buffers;

/// <summary>Represents one validated immutable FIGfont version 2 definition.</summary>
public sealed class FigletFont
{
    private readonly IReadOnlyDictionary<int, FigletGlyph> _glyphs;

    /// <summary>Initializes one internally validated immutable font.</summary>
    internal FigletFont(
        string name,
        int height,
        int baseline,
        char hardblank,
        FigletDirection direction,
        FigletLayout layout,
        IReadOnlyDictionary<int, FigletGlyph> glyphs,
        FigletLimits limits)
    {
        Name = name;
        Height = height;
        Baseline = baseline;
        Hardblank = hardblank;
        Direction = direction;
        Layout = layout;
        _glyphs = glyphs;
        Limits = limits;
    }

    /// <summary>Gets the caller-supplied catalog or file name.</summary>
    public string Name { get; }

    /// <summary>Gets the fixed glyph height in text rows.</summary>
    public int Height { get; }

    /// <summary>Gets the baseline row counted from one.</summary>
    public int Baseline { get; }

    /// <summary>Gets the FIGfont hardblank character.</summary>
    public char Hardblank { get; }

    /// <summary>Gets the font's preferred scalar direction.</summary>
    public FigletDirection Direction { get; }

    /// <summary>Gets the font's FIGlet layout bits.</summary>
    public FigletLayout Layout { get; }

    /// <summary>Loads and validates a font using default finite limits.</summary>
    /// <param name="source">The readable non-null encoded font stream.</param>
    /// <param name="name">The non-empty logical font name.</param>
    /// <returns>An immutable fully parsed font.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="InvalidDataException">The input exceeds a configured limit.</exception>
    /// <exception cref="FormatException">The input is not a valid supported FIGfont.</exception>
    public static FigletFont Load(Stream source, string name) =>
        Load(source, name, FigletLimits.Default);

    /// <summary>Loads and validates a font using explicit finite limits.</summary>
    /// <param name="source">The readable non-null encoded font stream.</param>
    /// <param name="name">The non-empty logical font name.</param>
    /// <param name="limits">The finite parser and renderer limits.</param>
    /// <returns>An immutable fully parsed font.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="InvalidDataException">The input exceeds a configured limit.</exception>
    /// <exception cref="FormatException">The input is not a valid supported FIGfont.</exception>
    public static FigletFont Load(Stream source, string name, FigletLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A font name cannot be empty.", nameof(name));
        }

        if (limits.MaxInputBytes <= 0)
        {
            throw new ArgumentException("The limits must be explicitly initialized.", nameof(limits));
        }

        using var buffer = new MemoryStream();
        var rented = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            while (true)
            {
                var read = source.Read(rented, 0, rented.Length);

                if (read == 0)
                {
                    break;
                }

                if (read > limits.MaxInputBytes - buffer.Length)
                {
                    throw new InvalidDataException("The encoded font exceeds the input byte limit.");
                }

                buffer.Write(rented, 0, read);
            }

            return FigletParser.Parse(buffer.GetBuffer().AsSpan(0, checked((int) buffer.Length)), name, limits);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <summary>Renders text using font metadata and default overrides.</summary>
    /// <param name="text">The non-null Unicode text.</param>
    /// <returns>The composed multi-line UTF-16 output.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The output limit would be exceeded.</exception>
    public string Render(string text) => Render(text, default);

    /// <summary>Renders text using explicit direction or layout overrides.</summary>
    /// <param name="text">The non-null Unicode text.</param>
    /// <param name="options">The validated rendering overrides.</param>
    /// <returns>The composed multi-line UTF-16 output.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The output limit would be exceeded.</exception>
    public string Render(string text, FigletOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        return FigletRenderer.Render(this, text, options);
    }

    /// <summary>Gets a scalar glyph or the question-mark fallback.</summary>
    /// <param name="value">The Unicode scalar value.</param>
    /// <returns>The matching immutable glyph.</returns>
    internal FigletGlyph GetGlyph(int value) =>
        _glyphs.TryGetValue(value, out var glyph)
            ? glyph
            : _glyphs['?'];

    /// <summary>Gets the finite limits retained for rendering.</summary>
    internal FigletLimits Limits { get; }
}
