// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using Terminal.Capabilities;

/// <summary>Exposes implemented terminal output protocols to an application.</summary>
[PublicAPI]
public interface ITerminalServices
{
    /// <summary>Gets the active immutable terminal-description metadata.</summary>
    public Description Description { get; }

    /// <summary>Gets the terminal bell (BEL character or audible alert) for user notifications.</summary>
    public IBell Bell { get; }

    /// <summary>Gets the OSC 52 clipboard interface for reading and writing terminal clipboard content.</summary>
    public IClipboard Clipboard { get; }

    /// <summary>
    /// Gets whether the active profile proves title support through built-in OSC 2
    /// or a complete described <c>TS</c>/<c>fsl</c> pair.
    /// </summary>
    public bool IsTitleSupported { get; }

    /// <summary>
    /// Sets the window title through proven built-in OSC 2 or the active described
    /// <c>TS</c>/<c>fsl</c> pair.
    /// </summary>
    /// <remarks>
    /// A described pair is emitted as opaque terminal-description programs and is
    /// not assumed to encode OSC 2. An unsupported title request is a byte-quiet no-op.
    /// </remarks>
    /// <param name="title">The non-null title without terminal control characters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="title"/> contains a terminal control character.
    /// </exception>
    public void SetTitle(string title);
}
