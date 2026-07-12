using SharpVision.Terminal.Protocols;

using InputOptions = SharpVision.Terminal.Input.Options;
using TerminalCapabilities = SharpVision.Terminal.Capabilities.Capabilities;
using TerminalNegotiationOptions = SharpVision.Terminal.Capabilities.NegotiationOptions;

namespace SharpVision.Terminal.Runtime;

/// <summary>Defines validated terminal session modes, bounds, and cleanup policy.</summary>
public sealed record Options
{
    /// <summary>Gets a session profile that enables no terminal modes.</summary>
    public static Options Minimal { get; } = new()
    {
        AlternateScreen = false,
        HideCursor = false,
        Focus = false,
        Paste = false,
        Tracking = null,
        Keyboard = null,
    };

    /// <summary>Gets the immutable capability snapshot.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public TerminalCapabilities Capabilities
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = TerminalCapabilities.Conservative;

    /// <summary>Gets optional bounded startup negotiation policy.</summary>
    public TerminalNegotiationOptions? Negotiation { get; init; }

    /// <summary>Gets whether to enter the alternate screen.</summary>
    public bool AlternateScreen { get; init; } = true;

    /// <summary>Gets whether to hide the terminal cursor.</summary>
    public bool HideCursor { get; init; } = true;

    /// <summary>Gets whether to enable proven focus reporting.</summary>
    public bool Focus { get; init; } = true;

    /// <summary>Gets whether to enable proven bracketed paste.</summary>
    public bool Paste { get; init; } = true;

    /// <summary>Gets the optional proven mouse tracking level.</summary>
    public MouseTracking? Tracking
    {
        get;
        init
        {
            if (value.HasValue && !Enum.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The mouse tracking level is unknown.");
            }

            field = value;
        }
    } = MouseTracking.Press;

    /// <summary>Gets the mouse coordinate encoding.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public MouseCoordinates Coordinates
    {
        get;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The mouse coordinate encoding is unknown.");
            }

            field = value;
        }
    } = MouseCoordinates.Sgr;

    /// <summary>Gets optional Kitty keyboard flags to push when proven supported.</summary>
    public Enhancement? Keyboard { get; init; } =
        Enhancement.Disambiguate | Enhancement.EventTypes;

    /// <summary>Gets finite input decoder policy.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public InputOptions Input
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = InputOptions.Default;

    /// <summary>Gets the positive transport read-buffer size.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int ReadBufferSize
    {
        get;
        init => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The read buffer size must be positive.");
    } = 16 * 1024;

    /// <summary>Gets the positive finite reverse-cleanup timeout.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive and finite.</exception>
    public TimeSpan CleanupTimeout
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The cleanup timeout must be positive and finite.");
            }

            field = value;
        }
    } = TimeSpan.FromSeconds(1);
}
