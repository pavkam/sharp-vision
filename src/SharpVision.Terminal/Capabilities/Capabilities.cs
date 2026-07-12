using SharpVision.Terminal.Unicode;

namespace SharpVision.Terminal.Capabilities;

/// <summary>
/// Publishes an immutable terminal feature profile.
/// </summary>
public sealed record Capabilities
{
    /// <summary>Gets the conservative profile used before detection.</summary>
    public static Capabilities Conservative { get; } = new();

    /// <summary>Gets the safe color fidelity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned color depth is unknown.</exception>
    public ColorDepth ColorDepth
    {
        get;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The color depth is unknown.");
            }

            field = value;
        }
    } = ColorDepth.Basic16;

    /// <summary>Gets the color-depth evidence origin.</summary>
    public Origin ColorOrigin { get; init; } = Origin.Default;

    /// <summary>Gets the pinned Unicode Character Database version.</summary>
    public string UnicodeVersion { get; } = Info.Version;

    /// <summary>Gets the East Asian Ambiguous cell-width policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned policy is unknown.</exception>
    public Ambiguous AmbiguousWidth
    {
        get;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The ambiguous-width policy is unknown.");
            }

            field = value;
        }
    }

    /// <summary>Gets synchronized-output support.</summary>
    public Feature SynchronizedOutput { get; init; } = Feature.Unknown;

    /// <summary>Gets focus-reporting support.</summary>
    public Feature FocusReporting { get; init; } = Feature.Unknown;

    /// <summary>Gets bracketed-paste support.</summary>
    public Feature BracketedPaste { get; init; } = Feature.Unknown;

    /// <summary>Gets pixel-coordinate mouse support.</summary>
    public Feature PixelMouse { get; init; } = Feature.Unknown;

    /// <summary>Gets cell-coordinate mouse support.</summary>
    public Feature CellMouse { get; init; } = Feature.Unknown;

    /// <summary>Gets Kitty keyboard protocol support.</summary>
    public Feature KittyKeyboard { get; init; } = Feature.Unknown;

    /// <summary>Gets OSC 52 clipboard support.</summary>
    public Feature Osc52 { get; init; } = Feature.Unknown;

    /// <summary>Gets Kitty OSC 5522 clipboard support.</summary>
    public Feature KittyClipboard { get; init; } = Feature.Unknown;

    /// <summary>Gets Kitty graphics extension support.</summary>
    public Feature KittyGraphics { get; init; } = Feature.Unknown;

    /// <summary>Gets sixel graphics support.</summary>
    public Feature Sixel { get; init; } = Feature.Unknown;

    /// <summary>Gets iTerm2 inline image support.</summary>
    public Feature ItermImages { get; init; } = Feature.Unknown;

    /// <summary>
    /// Gets a snapshot of every optional feature for diagnostics and tests.
    /// </summary>
    public IReadOnlyList<Feature> OptionalFeatures =>
    [
        SynchronizedOutput,
        FocusReporting,
        BracketedPaste,
        PixelMouse,
        CellMouse,
        KittyKeyboard,
        Osc52,
        KittyClipboard,
        KittyGraphics,
        Sixel,
        ItermImages,
    ];
}
