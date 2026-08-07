// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

/// <summary>Writes and requests terminal clipboard selections when supported.</summary>
/// <remarks>
/// A write or request prefers Kitty OSC 5522 MIME transfer when authoritatively proven, falls back
/// to OSC 52 plain text when only that is authoritatively proven, and is a byte-quiet no-op when
/// neither is proven. See the
/// <a href="../../docs/concepts/safe-degradation.md">safe-degradation contract</a> for the general
/// fallback rule this follows.
/// </remarks>
[PublicAPI]
public interface IClipboard
{
    /// <summary>Gets whether the active terminal advertises clipboard access through Kitty OSC 5522
    /// or OSC 52.</summary>
    public bool Supported { get; }

    /// <summary>
    /// Raised on the completion of every <see cref="Request"/>, and of a <see cref="Write"/> served
    /// by Kitty OSC 5522, that actually reached the terminal — success, terminal-reported failure,
    /// cancellation, or timeout. A call that was a byte-quiet no-op never raises it.
    /// </summary>
    /// <remarks>
    /// A <see cref="Write"/> that fell back to OSC 52 is fire-and-forget and raises nothing: the
    /// protocol defines no acknowledgement for a write, so there is no outcome to report. Anything
    /// this event could raise there would mean "bytes were queued", which is a categorically weaker
    /// fact than the Kitty event's terminal-reported status. Check <see cref="Supported"/> together
    /// with the profile's Kitty clipboard capability before making completion of a write depend on
    /// this event.
    /// </remarks>
    public event EventHandler<KittyClipboardReplyEventArgs>? KittyClipboardReplyReceived;

    /// <summary>Writes text to a selection; a no-op when unsupported.</summary>
    /// <param name="text">The text to copy.</param>
    /// <param name="selection">The target selection.</param>
    public void Write(ReadOnlySpan<char> text, Selection selection = Selection.Clipboard);

    /// <summary>
    /// Requests a selection's text; a no-op when unsupported. The reply arrives through
    /// <see cref="KittyClipboardReplyReceived"/>.
    /// </summary>
    /// <param name="selection">The target selection.</param>
    public void Request(Selection selection = Selection.Clipboard);
}
