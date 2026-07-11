using SharpVision.Terminal.Unicode;

namespace SharpVision.Terminal.Capabilities;

/// <summary>
/// Defines explicit nullable caller overrides applied after all other evidence.
/// </summary>
public sealed record Settings
{
    /// <summary>Gets an optional color-depth override.</summary>
    public ColorDepth? ColorDepth { get; init; }

    /// <summary>Gets an optional East Asian Ambiguous width override.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned policy is unknown.</exception>
    public Ambiguous? AmbiguousWidth
    {
        get;
        init
        {
            if (value.HasValue && !Enum.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The ambiguous-width policy is unknown.");
            }

            field = value;
        }
    }

    /// <summary>Gets an optional synchronized-output override.</summary>
    public bool? SynchronizedOutput { get; init; }

    /// <summary>Gets an optional focus-reporting override.</summary>
    public bool? FocusReporting { get; init; }

    /// <summary>Gets an optional bracketed-paste override.</summary>
    public bool? BracketedPaste { get; init; }

    /// <summary>Gets an optional pixel-mouse override.</summary>
    public bool? PixelMouse { get; init; }

    /// <summary>Gets an optional cell-mouse override.</summary>
    public bool? CellMouse { get; init; }

    /// <summary>Gets an optional Kitty keyboard override.</summary>
    public bool? KittyKeyboard { get; init; }

    /// <summary>Gets an optional OSC 52 override.</summary>
    public bool? Osc52 { get; init; }

    /// <summary>Gets an optional Kitty clipboard override.</summary>
    public bool? KittyClipboard { get; init; }

    /// <summary>Gets an optional Kitty graphics override.</summary>
    public bool? KittyGraphics { get; init; }

    /// <summary>Gets an optional sixel override.</summary>
    public bool? Sixel { get; init; }

    /// <summary>Gets an optional iTerm2 image override.</summary>
    public bool? ItermImages { get; init; }
}

/// <summary>
/// Contains nullable results from bounded capability queries.
/// </summary>
public sealed record Queries
{
    /// <summary>Gets a synchronized-output query result.</summary>
    public bool? SynchronizedOutput { get; init; }

    /// <summary>Gets a focus-reporting query result.</summary>
    public bool? FocusReporting { get; init; }

    /// <summary>Gets a bracketed-paste query result.</summary>
    public bool? BracketedPaste { get; init; }

    /// <summary>Gets a pixel-mouse query result.</summary>
    public bool? PixelMouse { get; init; }

    /// <summary>Gets a cell-mouse query result.</summary>
    public bool? CellMouse { get; init; }

    /// <summary>Gets a Kitty keyboard query result.</summary>
    public bool? KittyKeyboard { get; init; }

    /// <summary>Gets an OSC 52 query result.</summary>
    public bool? Osc52 { get; init; }

    /// <summary>Gets a Kitty clipboard query result.</summary>
    public bool? KittyClipboard { get; init; }

    /// <summary>Gets a Kitty graphics query result.</summary>
    public bool? KittyGraphics { get; init; }

    /// <summary>Gets a sixel query result.</summary>
    public bool? Sixel { get; init; }

    /// <summary>Gets an iTerm2 image query result.</summary>
    public bool? ItermImages { get; init; }
}
