// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Backends;

using Capabilities;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using TerminalNegotiationOptions = NegotiationOptions;
using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Defines validated terminal session modes, bounds, and cleanup policy.</summary>
[PublicAPI]
public sealed record TerminalOptions
{
    private static readonly IReadOnlyDictionary<string, string?> _emptyEnvironment =
        System.Collections.Immutable.ImmutableDictionary<string, string?>.Empty;

    private const Kitty.Keyboard.KittyKeyboardEnhancement _allEnhancements =
        Kitty.Keyboard.KittyKeyboardEnhancement.Disambiguate |
        Kitty.Keyboard.KittyKeyboardEnhancement.EventTypes |
        Kitty.Keyboard.KittyKeyboardEnhancement.AlternateKeys |
        Kitty.Keyboard.KittyKeyboardEnhancement.AllKeys |
        Kitty.Keyboard.KittyKeyboardEnhancement.AssociatedText;

    /// <summary>Gets a session profile that enables no terminal modes.</summary>
    public static TerminalOptions Minimal { get; } = new()
    {
        AlternateScreen = false,
        HideCursor = false,
        Focus = false,
        Paste = false,
        Tracking = null,
        Keyboard = null,
        ModifyOtherKeys = null
    };

    private TerminalProfile _profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);

    /// <summary>Gets the complete immutable terminal profile used by the session.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public TerminalProfile Profile
    {
        get => _profile;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _profile = value;
        }
    }

    /// <summary>
    /// Gets or compatibility-sets the semantic capability snapshot. Setting this property
    /// replaces <see cref="Profile"/> with a built-in ANSI profile around the exact value.
    /// When both initializers are present, the last initializer wins.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public TerminalCapabilities Capabilities
    {
        get => _profile.Capabilities;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _profile = TerminalProfile.CreateAnsi(value);
        }
    }

    /// <summary>Gets optional bounded startup negotiation policy.</summary>
    public TerminalNegotiationOptions? Negotiation { get; init; }

    /// <summary>
    /// Gets an explicit multiplexer routing policy independent of <see cref="Negotiation"/>, or null to
    /// fall back to <see cref="TerminalNegotiationOptions.Multiplexing"/> when
    /// negotiation runs.
    /// </summary>
    /// <remarks>
    /// A host that pins <see cref="Profile"/> or <see cref="Capabilities"/> to avoid probing still owns a
    /// separate decision about whether graphics route through an approved multiplexer passthrough. Because
    /// <see cref="Negotiation"/> being non-null also drives whether startup negotiation actually probes the
    /// terminal, that policy cannot be carried on <see cref="Negotiation"/> alone once probing is suppressed.
    /// </remarks>
    public MultiplexingPolicy? Multiplexing { get; init; }

    /// <summary>Gets diagnostic families promoted to exceptions at completed recovery boundaries.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown bits.</exception>
    public DiagnosticPromotion DiagnosticPromotions
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefinedFlags(
                value,
                DiagnosticPromotion.All,
                nameof(value),
                "The diagnostic promotion selection contains unknown bits.");
            field = value;
        }
    }

    /// <summary>Gets whether to enter the alternate screen.</summary>
    public bool AlternateScreen { get; init; } = true;

    /// <summary>Gets whether to hide the terminal cursor.</summary>
    public bool HideCursor { get; init; } = true;

    /// <summary>Gets whether to enable proven focus reporting.</summary>
    public bool Focus { get; init; } = true;

    /// <summary>Gets whether to enable proven bracketed paste.</summary>
    public bool Paste { get; init; } = true;

    /// <summary>Gets whether to enable proven Kitty OSC 5522 clipboard paste notifications.</summary>
    public bool ClipboardPasteEvents { get; init; }

    /// <summary>Gets the positive finite deadline for one clipboard read or acknowledged write.</summary>
    /// <remarks>The default is 30 seconds so an interactive terminal permission prompt can be answered.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive and finite.</exception>
    public TimeSpan ClipboardOperationTimeout
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The clipboard operation timeout must be positive and finite.");
            }

            field = value;
        }
    } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the optional proven mouse tracking level.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public MouseTracking? Tracking
    {
        get;
        init
        {
            if (value.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNotDefined(value.Value, nameof(value), "The mouse tracking level is unknown.");
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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The mouse coordinate encoding is unknown.");

            field = value;
        }
    } = MouseCoordinates.Sgr;

    /// <summary>Gets optional Kitty keyboard flags to push when proven supported.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown bits.</exception>
    /// <exception cref="ArgumentException">Associated text is set without all-key reporting.</exception>
    public Kitty.Keyboard.KittyKeyboardEnhancement? Keyboard
    {
        get;
        init
        {
            if (value is { } flags)
            {
                ArgumentOutOfRangeException.ThrowIfUndefinedFlags(flags, _allEnhancements, nameof(value), "The keyboard enhancement flags contain unknown bits.");

                if ((flags & Kitty.Keyboard.KittyKeyboardEnhancement.AssociatedText) != 0 &&
                    (flags & Kitty.Keyboard.KittyKeyboardEnhancement.AllKeys) == 0)
                {
                    throw new ArgumentException("Associated text requires all-key reporting.", nameof(value));
                }
            }

            field = value;
        }
    } = Kitty.Keyboard.KittyKeyboardEnhancement.Disambiguate | Kitty.Keyboard.KittyKeyboardEnhancement.EventTypes;

    /// <summary>Gets the optional xterm modifyOtherKeys level used when Kitty keyboard is unavailable.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside one through three.</exception>
    [ValueRange(1, 3)]
    public int? ModifyOtherKeys
    {
        get;
        init
        {
            if (value is not null and (< 1 or > 3))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The modifyOtherKeys level must be one through three.");
            }

            field = value;
        }
    } = 2;

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
    [NonNegativeValue]
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

    /// <summary>
    /// Resolves one immutable runtime context from this options snapshot without reading process
    /// environment state.
    /// </summary>
    /// <returns>A context with this profile and its fixed canonical backend identity.</returns>
    [Pure]
    internal TerminalContext CreateContext()
    {
        var environment = Negotiation?.Environment ?? _emptyEnvironment;
        var resolution = Profile.Resolve(environment);

        return new TerminalContext(Profile, resolution.Backend, resolution.Evidence);
    }
}
