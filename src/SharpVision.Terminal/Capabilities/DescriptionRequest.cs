// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Owns one bounded terminal-description lookup request.</summary>
internal sealed class DescriptionRequest
{
    /// <summary>Initializes one validated lookup request.</summary>
    /// <param name="terminalName">The exact non-blank database name.</param>
    /// <param name="platform">The requested platform description family.</param>
    /// <param name="outputFileDescriptor">The output descriptor passed to <c>setupterm</c>.</param>
    /// <param name="limits">The finite lookup and compilation limits.</param>
    /// <param name="explicitProfile">An optional complete caller-owned replacement profile.</param>
    /// <param name="allowAnsiFallback">Whether Unix provider absence may select the built-in ANSI profile.</param>
    /// <param name="windowsVirtualTerminal">Whether the Windows host established virtual-terminal processing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="terminalName"/> is blank, or <paramref name="windowsVirtualTerminal"/> is true for a
    /// non-Windows <paramref name="platform"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The platform is undefined, the descriptor is negative, or the terminal name exceeds its byte bound.
    /// </exception>
    public DescriptionRequest(
        string terminalName,
        DescriptionPlatform platform,
        int outputFileDescriptor,
        Limits limits,
        TerminalProfile? explicitProfile = null,
        bool allowAnsiFallback = false,
        bool windowsVirtualTerminal = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(terminalName);
        ArgumentNullException.ThrowIfNull(limits);

        if (!Enum.IsDefined(platform))
        {
            throw new ArgumentOutOfRangeException(nameof(platform), platform, "The description platform is unknown.");
        }

        if (windowsVirtualTerminal && platform != DescriptionPlatform.Windows)
        {
            throw new ArgumentException(
                "Windows virtual-terminal evidence requires the Windows description platform.",
                nameof(windowsVirtualTerminal));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(outputFileDescriptor);

        if (Encoding.UTF8.GetByteCount(terminalName) > limits.MaxTerminalNameBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(terminalName),
                terminalName.Length,
                "The UTF-8 terminal name exceeds the configured byte limit.");
        }

        TerminalName = terminalName;
        Platform = platform;
        OutputFileDescriptor = outputFileDescriptor;
        Limits = limits;
        ExplicitProfile = explicitProfile;
        AllowAnsiFallback = allowAnsiFallback;
        WindowsVirtualTerminal = windowsVirtualTerminal;
    }

    /// <summary>Gets the exact requested database name.</summary>
    public string TerminalName { get; }

    /// <summary>Gets the requested platform description family.</summary>
    public DescriptionPlatform Platform { get; }

    /// <summary>Gets the output descriptor passed to native setup.</summary>
    public int OutputFileDescriptor { get; }

    /// <summary>Gets the finite lookup and compilation limits.</summary>
    public Limits Limits { get; }

    /// <summary>Gets the complete caller-owned replacement profile, when supplied.</summary>
    public TerminalProfile? ExplicitProfile { get; }

    /// <summary>Gets whether Unix provider absence may select the built-in ANSI profile.</summary>
    public bool AllowAnsiFallback { get; }

    /// <summary>Gets whether the Windows host established virtual-terminal processing.</summary>
    public bool WindowsVirtualTerminal { get; }
}
