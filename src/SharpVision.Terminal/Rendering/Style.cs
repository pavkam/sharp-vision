using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Represents immutable semantic cell colors, attributes, and hyperlink.
/// </summary>
public readonly record struct Style
{
    private const Attributes _allAttributes =
        Attributes.Bold |
        Attributes.Dim |
        Attributes.Italic |
        Attributes.Underline |
        Attributes.Blink |
        Attributes.Reverse |
        Attributes.Hidden |
        Attributes.Strike;

    /// <summary>Initializes a validated semantic style.</summary>
    /// <param name="foreground">The terminal foreground color.</param>
    /// <param name="background">The terminal background color.</param>
    /// <param name="attributes">The semantic rendition flags.</param>
    /// <param name="hyperlink">An optional immutable hyperlink target.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attributes"/> contains unknown flags.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="hyperlink"/> is empty or contains a control code unit.
    /// </exception>
    public Style(
        Color foreground = default,
        Color background = default,
        Attributes attributes = Attributes.None,
        string? hyperlink = null)
    {
        if ((attributes & ~_allAttributes) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attributes),
                attributes,
                "The style contains an unknown attribute flag.");
        }

        if (hyperlink is not null)
        {
            if (hyperlink.Length == 0)
            {
                throw new ArgumentException("A hyperlink cannot be empty.", nameof(hyperlink));
            }

            foreach (var value in hyperlink)
            {
                if (char.IsControl(value))
                {
                    throw new ArgumentException(
                        "A hyperlink cannot contain control code units.",
                        nameof(hyperlink));
                }
            }
        }

        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Hyperlink = hyperlink;
    }

    /// <summary>Gets the default terminal style.</summary>
    public static Style Default => default;

    /// <summary>Gets the foreground color.</summary>
    public Color Foreground { get; }

    /// <summary>Gets the background color.</summary>
    public Color Background { get; }

    /// <summary>Gets the rendition attributes.</summary>
    public Attributes Attributes { get; }

    /// <summary>Gets the optional immutable hyperlink target.</summary>
    public string? Hyperlink { get; }
}
