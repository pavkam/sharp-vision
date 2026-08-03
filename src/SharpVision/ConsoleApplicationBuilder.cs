// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using SharpVision.Runtime;
using SharpVision.Terminal.Runtime;

using Terminal.Capabilities;
using Terminal.Kitty.Keyboard;

/// <summary>Configures and builds one interactive console <see cref="Application"/> fluently.</summary>
[PublicAPI]
public sealed class ConsoleApplicationBuilder
{
    private readonly Screen _screen;
    private readonly Func<bool> _isInteractive;
    private readonly Func<ConsoleHostOptions, ConsoleConnection> _open;
    private readonly Action<string> _writeLine;

    /// <summary>Initializes a builder for one detached screen.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <exception cref="ArgumentNullException"><paramref name="screen"/> is null.</exception>
    public ConsoleApplicationBuilder(Screen screen) : this(
        screen,
        static () => ConsoleHost.Default.IsInteractive,
        static options => ConsoleHost.Default.Open(options),
        static message => ConsoleTextChannel.WriteLine(message))
    {
    }

    /// <summary>Initializes a builder over deterministic host-preflight seams.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <param name="isInteractive">The non-null interactive-console probe.</param>
    /// <param name="open">The non-null console connection factory.</param>
    /// <param name="writeLine">The non-null plain-message writer.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal ConsoleApplicationBuilder(
        Screen screen,
        Func<bool> isInteractive,
        Func<ConsoleHostOptions, ConsoleConnection> open,
        Action<string> writeLine)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(isInteractive);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(writeLine);
        _screen = screen;
        _isInteractive = isInteractive;
        _open = open;
        _writeLine = writeLine;
    }

    /// <summary>Gets the accumulated run options.</summary>
    public ConsoleRunOptions Options { get; private set; } = new();

    /// <summary>Publishes a theme to the tree.</summary>
    /// <param name="theme">The non-null theme.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is null.</exception>
    public ConsoleApplicationBuilder UseTheme(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        Options = Options with { Theme = theme };
        return this;
    }

    /// <summary>Sets whether to enter the alternate screen.</summary>
    /// <param name="enabled">Whether to enter the alternate screen.</param>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder UseAlternateScreen(bool enabled = true)
    {
        Options = Options with { AlternateScreen = enabled };
        return this;
    }

    /// <summary>Sets whether the cursor stays visible.</summary>
    /// <param name="visible">Whether the cursor stays visible.</param>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder ShowCursor(bool visible = true)
    {
        Options = Options with { ShowCursor = visible };
        return this;
    }

    /// <summary>Enables mouse input at the given level and coordinate encoding.</summary>
    /// <param name="tracking">The mouse tracking level.</param>
    /// <param name="coordinates">The mouse coordinate encoding.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A value is unknown.</exception>
    public ConsoleApplicationBuilder UseMouse(
        MouseTracking tracking = MouseTracking.Any,
        MouseCoordinates coordinates = MouseCoordinates.Sgr)
    {
        Options = Options with { MouseTracking = tracking, MouseCoordinates = coordinates };
        return this;
    }

    /// <summary>Disables mouse input.</summary>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder WithoutMouse()
    {
        Options = Options with { MouseTracking = null };
        return this;
    }

    /// <summary>Sets whether bracketed paste is enabled.</summary>
    /// <param name="enabled">Whether bracketed paste is enabled.</param>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder UseBracketedPaste(bool enabled = true)
    {
        Options = Options with { BracketedPaste = enabled };
        return this;
    }

    /// <summary>Sets whether focus reporting is enabled.</summary>
    /// <param name="enabled">Whether focus reporting is enabled.</param>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder UseFocusReporting(bool enabled = true)
    {
        Options = Options with { FocusReporting = enabled };
        return this;
    }

    /// <summary>Sets the Kitty keyboard enhancement flags, or null to disable.</summary>
    /// <param name="enhancement">The enhancement flags, or null to disable.</param>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder UseKeyboardEnhancement(Enhancement? enhancement)
    {
        Options = Options with { KeyboardEnhancement = enhancement };
        return this;
    }

    /// <summary>Overrides the color depth.</summary>
    /// <param name="depth">The color depth override.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is unknown.</exception>
    public ConsoleApplicationBuilder UseColorDepth(ColorDepth depth)
    {
        Options = Options with { ColorDepth = depth };
        return this;
    }

    /// <summary>Forces the minimal monochrome color depth.</summary>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder WithoutColors()
    {
        Options = Options with { ColorDepth = ColorDepth.Monochrome };
        return this;
    }

    /// <summary>Overrides semantic capabilities through a complete built-in ANSI profile.</summary>
    /// <param name="capabilities">The non-null capability profile.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is null.</exception>
    public ConsoleApplicationBuilder UseCapabilities(TerminalCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        Options = Options with { Profile = null, Capabilities = capabilities };
        return this;
    }

    /// <summary>Uses one complete terminal profile and bypasses native description discovery.</summary>
    /// <param name="profile">The non-null complete profile.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    public ConsoleApplicationBuilder UseTerminalProfile(TerminalProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Options = Options with { Profile = profile, Capabilities = null };
        return this;
    }

    /// <summary>Overrides startup negotiation.</summary>
    /// <param name="negotiation">The non-null negotiation policy.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="negotiation"/> is null.</exception>
    public ConsoleApplicationBuilder UseNegotiation(NegotiationOptions negotiation)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        Options = Options with { Negotiation = negotiation };
        return this;
    }

    /// <summary>Disables startup negotiation.</summary>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder WithoutNegotiation()
    {
        Options = Options with
        {
            Negotiation = null,
            Profile = Options.Profile ?? (Options.Capabilities is null
                ? TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative)
                : null)
        };
        return this;
    }

    /// <summary>Sets the reverse-cleanup timeout.</summary>
    /// <param name="timeout">The positive finite reverse-cleanup timeout.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive and finite.</exception>
    public ConsoleApplicationBuilder WithCleanupTimeout(TimeSpan timeout)
    {
        Options = Options with { CleanupTimeout = timeout };
        return this;
    }

    /// <summary>Sets the transport read-buffer size.</summary>
    /// <param name="size">The positive read-buffer size.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is not positive.</exception>
    public ConsoleApplicationBuilder WithReadBufferSize(int size)
    {
        Options = Options with { ReadBufferSize = size };
        return this;
    }

    /// <summary>Sets the resize poll interval.</summary>
    /// <param name="interval">The positive finite resize poll interval.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is not positive and finite.</exception>
    public ConsoleApplicationBuilder WithResizeInterval(TimeSpan interval)
    {
        Options = Options with { ResizeInterval = interval };
        return this;
    }

    /// <summary>Delivers Ctrl+C as input instead of requesting shutdown.</summary>
    /// <param name="enabled">Whether Ctrl+C is delivered as input.</param>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder TreatControlCAsInput(bool enabled = true)
    {
        Options = Options with { TreatControlCAsInput = enabled };
        return this;
    }

    /// <summary>Honors the LINES and COLUMNS environment variables for the initial size.</summary>
    /// <param name="enabled">Whether the environment variables override the initial size.</param>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder UseEnvironmentSizeOverrides(bool enabled = true)
    {
        Options = Options with { UseEnvironmentSizeOverrides = enabled };
        return this;
    }

    /// <summary>Sets the message written when the console is redirected.</summary>
    /// <param name="message">The message to write, or null for none.</param>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder WithRedirectedMessage(string? message)
    {
        Options = Options with { RedirectedMessage = message };
        return this;
    }

    /// <summary>Sets the plain message written when terminal-description preflight rejects the console.</summary>
    /// <param name="message">The message to write, or null for none.</param>
    /// <returns>This builder.</returns>
    public ConsoleApplicationBuilder WithUnsupportedTerminalMessage(string? message)
    {
        Options = Options with { UnsupportedTerminalMessage = message };
        return this;
    }

    /// <summary>Replaces the accumulated options wholesale.</summary>
    /// <param name="configure">The non-null transform over the current options.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="configure"/> returned null.</exception>
    public ConsoleApplicationBuilder ConfigureOptions(Func<ConsoleRunOptions, ConsoleRunOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Options = configure(Options) ?? throw new InvalidOperationException("ConfigureOptions returned null.");
        return this;
    }

    /// <summary>Opens the console and builds a wired application for advanced lifecycle control.</summary>
    /// <returns>The application; the caller runs and disposes it.</returns>
    /// <exception cref="IOException">The console is not interactive or cannot enter raw mode.</exception>
    /// <exception cref="NotSupportedException">No usable terminal description is available.</exception>
    /// <exception cref="ArgumentException">The screen is already attached to an application.</exception>
    /// <exception cref="ObjectDisposedException">The screen is disposed.</exception>
    public Application Build()
    {
        if (!_isInteractive())
        {
            throw new IOException("The console host is not interactive.");
        }

        var connection = _open(Options.ToHostOptions());
        Application? application = null;

        try
        {
            var explicitProfile = Options.Profile ??
                                  (Options.Capabilities is { } capabilities
                                      ? TerminalProfile.CreateAnsi(capabilities)
                                      : null);
            var resolution = connection.ResolveDescription(explicitProfile);
            var profile = resolution.Profile ??
                          throw new UnsupportedTerminalException(resolution);

            if (profile.Description.Suitability != Suitability.Usable)
            {
                throw new UnsupportedTerminalException(resolution);
            }

            application = new Application(
                _screen,
                connection.Transport,
                connection.Resize,
                Options.ToTerminalOptions(profile),
                hostLease: connection);
            // Attach (which runs the screen's OnAttach - the documented place for theme
            // publication, docs/concepts/screen.md) and the builder's own configured Theme both
            // now require the owning dispatcher thread (see #204). Build() itself always runs on
            // the caller's own thread, never the dispatcher thread the constructor just started.
            // Only reassign application.Theme when the caller explicitly configured one via
            // UseTheme(...) - otherwise leave whatever OnAttach published alone (see #255).
            var explicitTheme = Options.Theme;
            application.Dispatcher.InvokeAsync(() =>
                {
                    _screen.Attach(application);
                    if (explicitTheme is not null)
                    {
                        application.Theme = explicitTheme;
                    }
                })
                .AsTask()
                .GetAwaiter()
                .GetResult();
            return application;
        }
        catch
        {
            if (application is not null)
            {
                application.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            else
            {
                DisposeAfterPreflightFailure(connection);
            }

            throw;
        }
    }

    /// <summary>Builds and runs the configured interactive console application.</summary>
    /// <param name="cancellationToken">Requests shutdown, reported as <see cref="ConsoleRunStatus.Cancelled"/>.</param>
    /// <returns>The run status.</returns>
    /// <exception cref="IOException">The interactive console cannot enter raw mode.</exception>
    public ValueTask<ConsoleRunStatus> RunAsync(CancellationToken cancellationToken = default) =>
        ConsoleApplication.RunCoreAsync(this, cancellationToken);

    /// <summary>Gets whether the configured host seam reports an interactive console.</summary>
    internal bool IsInteractive => _isInteractive();

    /// <summary>Writes one non-null plain host message.</summary>
    /// <param name="message">The non-null message.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
    internal void WriteLine(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _writeLine(message);
    }

    private static void DisposeAfterPreflightFailure(ConsoleConnection connection)
    {
        Debug.Assert(connection is not null, "An opened console connection owns every preflight resource.");

        // Match ordinary Application ownership order. A cleanup failure cannot
        // replace preflight rejection or skip the remaining owned resources.
        DisposeIgnoringFailure(connection.Resize);
        DisposeIgnoringFailure(connection.Transport);
        DisposeIgnoringFailure(connection);
    }

    private static void DisposeIgnoringFailure(IAsyncDisposable resource)
    {
        Debug.Assert(resource is not null, "Preflight cleanup owns only non-null resources.");

        try
        {
            resource.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // The active preflight exception remains primary.
        }
    }
}
