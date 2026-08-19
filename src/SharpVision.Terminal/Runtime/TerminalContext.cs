// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Backends;

using Capabilities;

/// <summary>
/// Binds one immutable terminal profile to the fixed terminal backend identity selected for an
/// application lifetime.
/// </summary>
internal sealed class TerminalContext
{
    /// <summary>
    /// Initializes a context that owns immutable profile state and a canonical backend identity.
    /// </summary>
    /// <param name="profile">The non-null immutable terminal profile.</param>
    /// <param name="backend">The non-null fixed terminal backend identity.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="profile"/> or <paramref name="backend"/> is <see langword="null"/>.
    /// </exception>
    public TerminalContext(TerminalProfile profile, TerminalBackend backend)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(backend);

        Profile = profile;
        Backend = backend;
    }

    /// <summary>Gets the immutable profile currently authorized for runtime behavior.</summary>
    public TerminalProfile Profile { get; }

    /// <summary>Gets the fixed canonical terminal backend identity.</summary>
    public TerminalBackend Backend { get; }

    /// <summary>
    /// Creates a context with refined capability evidence while preserving the exact backend
    /// identity reference selected at initialization.
    /// </summary>
    /// <param name="capabilities">The non-null immutable replacement capability snapshot.</param>
    /// <returns>A new context with the refined profile and the same backend reference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is <see langword="null"/>.</exception>
    [Pure]
    public TerminalContext WithCapabilities(TerminalCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return new TerminalContext(Profile.WithCapabilities(capabilities), Backend);
    }
}
