// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using SharpVision.Styling;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;

using TerminalCapabilities = Terminal.Capabilities.Capabilities;
using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Configures one interactive console screen run and the terminal policy it produces.</summary>
public sealed record ConsoleRunOptions
{
    /// <summary>Gets the theme published to the tree, or null for <see cref="Themes.Dark"/>.</summary>
    public Theme? Theme { get; init; }

    /// <summary>Gets whether to enter the alternate screen. Default is true.</summary>
    public bool AlternateScreen { get; init; } = true;

    /// <summary>Gets whether the cursor stays visible. Default is false.</summary>
    public bool ShowCursor { get; init; }

    /// <summary>Gets the mouse tracking level, or null to disable mouse input. Default is <see cref="MouseTracking.Any"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public MouseTracking? MouseTracking
    {
        get;
        init
        {
            if (value.HasValue && !Enum.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The mouse tracking level is unknown.");
            }

            field = value;
        }
    } = Terminal.Protocols.MouseTracking.Any;

    /// <summary>Gets the mouse coordinate encoding. Default is <see cref="MouseCoordinates.Sgr"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public MouseCoordinates MouseCoordinates
    {
        get;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The mouse coordinate encoding is unknown.");
            }

            field = value;
        }
    } = MouseCoordinates.Sgr;

    /// <summary>Gets whether bracketed paste is enabled. Default is true.</summary>
    public bool BracketedPaste { get; init; } = true;

    /// <summary>Gets whether focus reporting is enabled. Default is true.</summary>
    public bool FocusReporting { get; init; } = true;

    /// <summary>Gets the Kitty keyboard flags to push when supported, or null to disable.</summary>
    public Enhancement? KeyboardEnhancement { get; init; } =
        Enhancement.Disambiguate | Enhancement.EventTypes;

    /// <summary>Gets an explicit capability profile, or null to detect and negotiate.</summary>
    public TerminalCapabilities? Capabilities { get; init; }

    /// <summary>Gets an explicit color depth override, or null for the detected depth.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public ColorDepth? ColorDepth
    {
        get;
        init
        {
            if (value.HasValue && !Enum.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The color depth is unknown.");
            }

            field = value;
        }
    }

    /// <summary>Gets a negotiation override, or null for default startup negotiation.</summary>
    public NegotiationOptions? Negotiation { get; init; }

    /// <summary>Gets the positive finite reverse-cleanup timeout. Default is one second.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive and finite.</exception>
    public TimeSpan CleanupTimeout
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The cleanup timeout must be positive and finite.");
            }

            field = value;
        }
    } = TimeSpan.FromSeconds(1);

    /// <summary>Gets the positive transport read-buffer size. Default is 16 KiB.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int ReadBufferSize
    {
        get;
        init => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The read buffer size must be positive.");
    } = 16 * 1024;

    /// <summary>Gets the positive finite resize poll interval. Default is 100 ms.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive and finite.</exception>
    public TimeSpan ResizeInterval
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The resize interval must be positive and finite.");
            }

            field = value;
        }
    } = TimeSpan.FromMilliseconds(100);

    /// <summary>Gets whether Ctrl+C is delivered as input rather than requesting shutdown. Default is false.</summary>
    public bool TreatControlCAsInput { get; init; }

    /// <summary>Gets whether the LINES and COLUMNS environment variables override the initial size. Default is false.</summary>
    public bool UseEnvironmentSizeOverrides { get; init; }

    /// <summary>Gets the optional message written when standard input or output is redirected.</summary>
    public string? RedirectedMessage { get; init; }

    /// <summary>Resolves the theme, defaulting to <see cref="Themes.Dark"/>.</summary>
    /// <returns>The theme to publish.</returns>
    public Theme ResolveTheme() => Theme ?? Themes.Dark;

    /// <summary>Builds the host policy for <see cref="Terminal.Runtime.ConsoleHost.Open"/>.</summary>
    /// <returns>The validated host options.</returns>
    public Terminal.Runtime.ConsoleHostOptions ToHostOptions() => new()
    {
        ResizeInterval = ResizeInterval,
        CaptureControlKeys = TreatControlCAsInput,
    };

    /// <summary>Builds the terminal session policy from these options.</summary>
    /// <returns>The validated terminal options.</returns>
    public TerminalOptions ToTerminalOptions()
    {
        TerminalCapabilities capabilities = ResolveCapabilities();

        return new TerminalOptions
        {
            Capabilities = capabilities,
            Negotiation = Capabilities is null ? Negotiation ?? DefaultNegotiation() : null,
            AlternateScreen = AlternateScreen,
            HideCursor = !ShowCursor,
            Focus = FocusReporting,
            Paste = BracketedPaste,
            Tracking = MouseTracking,
            Coordinates = MouseCoordinates,
            Keyboard = KeyboardEnhancement,
            CleanupTimeout = CleanupTimeout,
            ReadBufferSize = ReadBufferSize,
        };
    }

    private TerminalCapabilities ResolveCapabilities()
    {
        if (Capabilities is { } profile)
        {
            return ColorDepth is { } depth ? profile with { ColorDepth = depth } : profile;
        }

        Settings overrides = new() { CellMouse = true };
        TerminalCapabilities detected = Detector.Detect(new Dictionary<string, string?>(), overrides: overrides);
        return ColorDepth is { } forced ? detected with { ColorDepth = forced } : detected;
    }

    private static NegotiationOptions DefaultNegotiation()
    {
        Dictionary<string, string?> environment = new(StringComparer.Ordinal);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
            {
                environment[key] = entry.Value?.ToString();
            }
        }

        return new NegotiationOptions(environment, new Settings { CellMouse = true });
    }
}
