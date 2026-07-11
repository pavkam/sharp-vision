using SharpVision.Terminal.Unicode;

namespace SharpVision.Terminal.Capabilities;

/// <summary>
/// Identifies confidence in one optional terminal feature.
/// </summary>
public enum Support
{
    /// <summary>No reliable evidence is available.</summary>
    Unknown,

    /// <summary>Evidence says the feature is unavailable.</summary>
    Unsupported,

    /// <summary>An environment hint suggests support but must not enable it.</summary>
    Tentative,

    /// <summary>A query or explicit override proves support.</summary>
    Supported,
}

/// <summary>
/// Identifies the evidence that produced a capability value.
/// </summary>
public enum Origin
{
    /// <summary>The conservative built-in profile supplied the value.</summary>
    Default,

    /// <summary>Environment hints supplied tentative or narrowing evidence.</summary>
    Environment,

    /// <summary>A bounded terminal query supplied the value.</summary>
    Query,

    /// <summary>An explicit caller override supplied the value.</summary>
    Override,
}

/// <summary>
/// Represents one optional feature and the origin of its evidence.
/// </summary>
/// <param name="State">The support confidence.</param>
/// <param name="Origin">The evidence origin.</param>
public readonly record struct Feature(Support State, Origin Origin)
{
    /// <summary>Gets a conservative unknown feature.</summary>
    public static Feature Unknown { get; } = new(Support.Unknown, Origin.Default);

    /// <summary>Gets whether safe behavior may actively use the feature.</summary>
    public bool IsSupported => State == Support.Supported;
}

/// <summary>
/// Identifies the terminal's safe color fidelity.
/// </summary>
public enum ColorDepth
{
    /// <summary>Do not rely on color output.</summary>
    Monochrome,

    /// <summary>Use the basic 16-color palette.</summary>
    Basic16,

    /// <summary>Use the indexed 256-color palette.</summary>
    Indexed256,

    /// <summary>Use 24-bit RGB colors.</summary>
    TrueColor,
}

/// <summary>
/// Publishes an immutable terminal feature profile.
/// </summary>
public sealed record Capabilities
{
    /// <summary>Gets the conservative profile used before detection.</summary>
    public static Capabilities Conservative { get; } = new();

    /// <summary>Gets the safe color fidelity.</summary>
    public ColorDepth ColorDepth { get; init; } = ColorDepth.Basic16;

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
