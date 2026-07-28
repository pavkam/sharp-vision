// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Backends;

using Capabilities;

using InputOptions = Input.Options;
using TerminalNegotiationOptions = Capabilities.NegotiationOptions;

/// <summary>Defines validated terminal session modes, bounds, and cleanup policy.</summary>
[PublicAPI]
public sealed record Options
{
    private static readonly IReadOnlyDictionary<string, string?> _emptyEnvironment =
        System.Collections.Immutable.ImmutableDictionary<string, string?>.Empty;

    /// <summary>Gets a session profile that enables no terminal modes.</summary>
    public static Options Minimal { get; } = new()
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
    public Kitty.KittyEnhancement? Keyboard { get; init; } =
        Kitty.KittyEnhancement.Disambiguate | Kitty.KittyEnhancement.EventTypes;

    /// <summary>Gets the optional xterm modifyOtherKeys level used when Kitty keyboard is unavailable.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside one through three.</exception>
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
    internal TerminalContext CreateContext()
    {
        var environment = Negotiation?.Environment ?? _emptyEnvironment;
        var resolution = TerminalBackendResolver.Resolve(Profile, environment);

        return new TerminalContext(Profile, resolution.Backend);
    }
}
