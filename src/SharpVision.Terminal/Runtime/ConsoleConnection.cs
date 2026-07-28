// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Capabilities;

/// <summary>
/// Bundles the transport and resize source opened for one interactive console and
/// owns the platform terminal-mode restore lease.
/// </summary>
/// <remarks>
/// The running session disposes Transport and Resize;
/// this connection restores the platform terminal mode when disposed, which the
/// host performs after the session's reverse mode cleanup.
/// </remarks>
[PublicAPI]
public sealed class ConsoleConnection: IAsyncDisposable
{
    private readonly IDisposable _restore;
    private readonly DescriptionLoader? _descriptionLoader;
    private readonly string? _terminalName;
    private int _disposed;

    /// <summary>Initializes a connection over opened console resources.</summary>
    /// <param name="transport">The non-null transport over the console streams.</param>
    /// <param name="resize">The non-null resize source.</param>
    /// <param name="restore">The non-null platform terminal-mode restore lease.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public ConsoleConnection(ITransport transport, IResizeSource resize, IDisposable restore)
        : this(
            transport,
            resize,
            restore,
            descriptionPlatform: null,
            outputFileDescriptor: null,
            windowsVirtualTerminal: false,
            descriptionLoader: null,
            terminalName: null)
    {
    }

    /// <summary>Initializes a connection with exact facts established by a platform console host.</summary>
    /// <param name="transport">The non-null transport over the console streams.</param>
    /// <param name="resize">The non-null resize source.</param>
    /// <param name="restore">The non-null platform terminal-mode restore lease.</param>
    /// <param name="descriptionPlatform">The established description-provider platform.</param>
    /// <param name="outputFileDescriptor">The non-negative descriptor used for terminal setup.</param>
    /// <param name="windowsVirtualTerminal">Whether Windows virtual-terminal processing was established.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="descriptionPlatform"/> is undefined or <paramref name="outputFileDescriptor"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="windowsVirtualTerminal"/> is true for a non-Windows platform.
    /// </exception>
    internal ConsoleConnection(
        ITransport transport,
        IResizeSource resize,
        IDisposable restore,
        DescriptionPlatform descriptionPlatform,
        int outputFileDescriptor,
        bool windowsVirtualTerminal)
        : this(
            transport,
            resize,
            restore,
            (DescriptionPlatform?) descriptionPlatform,
            outputFileDescriptor,
            windowsVirtualTerminal,
            descriptionLoader: null,
            terminalName: null)
    {
    }

    /// <summary>Initializes a connection with deterministic lower-layer description resolution.</summary>
    /// <param name="transport">The non-null transport over the console streams.</param>
    /// <param name="resize">The non-null resize source.</param>
    /// <param name="restore">The non-null platform terminal-mode restore lease.</param>
    /// <param name="descriptionPlatform">The established description-provider platform.</param>
    /// <param name="outputFileDescriptor">The non-negative descriptor used for terminal setup.</param>
    /// <param name="windowsVirtualTerminal">Whether Windows virtual-terminal processing was established.</param>
    /// <param name="descriptionLoader">The non-null deterministic lower-layer loader.</param>
    /// <param name="terminalName">The non-blank deterministic terminal name.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="terminalName"/> is blank or platform facts conflict.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A platform fact is outside its valid range.</exception>
    internal ConsoleConnection(
        ITransport transport,
        IResizeSource resize,
        IDisposable restore,
        DescriptionPlatform descriptionPlatform,
        int outputFileDescriptor,
        bool windowsVirtualTerminal,
        DescriptionLoader descriptionLoader,
        string terminalName)
        : this(
            transport,
            resize,
            restore,
            (DescriptionPlatform?) descriptionPlatform,
            outputFileDescriptor,
            windowsVirtualTerminal,
            RequireDescriptionLoader(descriptionLoader),
            terminalName)
    {
    }

    private ConsoleConnection(
        ITransport transport,
        IResizeSource resize,
        IDisposable restore,
        DescriptionPlatform? descriptionPlatform,
        int? outputFileDescriptor,
        bool windowsVirtualTerminal,
        DescriptionLoader? descriptionLoader,
        string? terminalName)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(resize);
        ArgumentNullException.ThrowIfNull(restore);

        if (descriptionLoader is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(terminalName);
        }
        else if (terminalName is not null)
        {
            throw new ArgumentNullException(
                nameof(descriptionLoader),
                "A deterministic terminal name requires a description loader.");
        }

        if (descriptionPlatform is { } platform && !Enum.IsDefined(platform))
        {
            throw new ArgumentOutOfRangeException(
                nameof(descriptionPlatform),
                descriptionPlatform,
                "The description platform is unknown.");
        }

        if (outputFileDescriptor is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputFileDescriptor),
                outputFileDescriptor,
                "The output file descriptor cannot be negative.");
        }

        if (windowsVirtualTerminal &&
            descriptionPlatform != Terminal.Capabilities.DescriptionPlatform.Windows)
        {
            throw new ArgumentException(
                "Windows virtual-terminal evidence requires the Windows description platform.",
                nameof(windowsVirtualTerminal));
        }

        Transport = transport;
        Resize = resize;
        DescriptionPlatform = descriptionPlatform;
        OutputFileDescriptor = outputFileDescriptor;
        WindowsVirtualTerminal = windowsVirtualTerminal;
        _descriptionLoader = descriptionLoader;
        _terminalName = terminalName;
        _restore = restore;
    }

    /// <summary>Gets the transport over the interactive console streams.</summary>
    public ITransport Transport { get; }

    /// <summary>Gets the resize source for the interactive console.</summary>
    public IResizeSource Resize { get; }

    /// <summary>Gets the established platform-description family, or null for a caller-built connection.</summary>
    internal DescriptionPlatform? DescriptionPlatform { get; }

    /// <summary>Gets the established terminal-setup output descriptor, or null when unknown.</summary>
    internal int? OutputFileDescriptor { get; }

    /// <summary>Gets whether the Windows console host established virtual-terminal processing.</summary>
    internal bool WindowsVirtualTerminal { get; }

    /// <summary>Resolves one terminal profile as a compatibility projection over <see cref="ResolveDescription"/>.</summary>
    /// <param name="explicitProfile">A complete caller-owned replacement profile, or null for platform discovery.</param>
    /// <param name="allowAnsiFallback">
    /// Whether an unavailable Unix provider may use the built-in ANSI profile. Missing,
    /// generic, hardcopy, incomplete, and padding-dependent descriptions never fall back.
    /// </param>
    /// <returns>The resolved owned profile, including an unsuitable profile, or null when no description is available.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The live Unix terminal name exceeds the configured UTF-8 bound.</exception>
    public TerminalProfile? ResolveProfile(
        TerminalProfile? explicitProfile = null,
        bool allowAnsiFallback = false) =>
        ResolveDescription(explicitProfile, allowAnsiFallback).Profile;

    /// <summary>
    /// Resolves one owned terminal-description result from an explicit replacement or
    /// this connection's immutable platform facts and the live Unix <c>TERM</c> value.
    /// </summary>
    /// <param name="explicitProfile">A complete caller-owned replacement profile, or null for platform discovery.</param>
    /// <param name="allowAnsiFallback">
    /// Whether an unavailable Unix provider may use the built-in ANSI profile. Missing,
    /// generic, hardcopy, incomplete, and padding-dependent descriptions never fall back.
    /// </param>
    /// <returns>The typed status, optional owned profile, and immutable redacted diagnostics.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The live Unix terminal name exceeds the configured UTF-8 bound.</exception>
    public DescriptionResult ResolveDescription(
        TerminalProfile? explicitProfile = null,
        bool allowAnsiFallback = false)
    {
        if (explicitProfile is not null)
        {
            return DescriptionResult.Loaded(
                explicitProfile,
                Array.Empty<DescriptionDiagnostic>());
        }

        if (DescriptionPlatform is not { } platform || OutputFileDescriptor is not { } descriptor)
        {
            return DescriptionResult.PlatformUnavailable();
        }

        var terminalName = _terminalName ?? (platform == Terminal.Capabilities.DescriptionPlatform.Windows
            ? "windows-vt"
            : Environment.GetEnvironmentVariable("TERM"));

        if (string.IsNullOrWhiteSpace(terminalName))
        {
            return DescriptionResult.MissingOrGeneric(
                [new DescriptionDiagnostic(DescriptionDiagnosticCode.MissingOrGeneric)]);
        }

        var request = new DescriptionRequest(
            terminalName,
            platform,
            descriptor,
            Limits.Default,
            allowAnsiFallback: allowAnsiFallback,
            windowsVirtualTerminal: WindowsVirtualTerminal);

        return (_descriptionLoader ?? new DescriptionLoader()).Load(request);
    }

    /// <summary>Restores the platform terminal mode once. Never disposes transport or resize.</summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _restore.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private static DescriptionLoader RequireDescriptionLoader(DescriptionLoader descriptionLoader)
    {
        ArgumentNullException.ThrowIfNull(descriptionLoader);
        return descriptionLoader;
    }
}
