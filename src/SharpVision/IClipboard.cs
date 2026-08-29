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
    /// <summary>Raised for a terminal-initiated Kitty OSC 5522 paste notification while the
    /// application owns the corresponding mode lease.</summary>
    /// <remarks>
    /// Raised on the application dispatcher after the complete bounded notification is validated.
    /// Event arguments own their MIME inventory and one-time password.
    /// </remarks>
    public event EventHandler<ClipboardPasteEventArgs>? ClipboardPasteReceived;

    /// <summary>Gets whether the active terminal advertises clipboard access through Kitty OSC 5522
    /// or OSC 52.</summary>
    public bool IsSupported { get; }

    /// <summary>
    /// Raised on the completion of every <see cref="Request"/>, and of a <see cref="Write"/> served
    /// by Kitty OSC 5522, that actually reached the terminal — success, terminal-reported failure,
    /// or timeout. A call that was a byte-quiet no-op never raises it.
    /// </summary>
    /// <remarks>
    /// A <see cref="Write"/> that fell back to OSC 52 is fire-and-forget and raises nothing: the
    /// protocol defines no acknowledgement for a write, so there is no outcome to report. Anything
    /// this event could raise there would mean "bytes were queued", which is a categorically weaker
    /// fact than the Kitty event's terminal-reported status. Check <see cref="IsSupported"/> together
    /// with the profile's Kitty clipboard capability before making completion of a write depend on
    /// this event. A <see cref="Request"/> or Kitty-served <see cref="Write"/> that is still pending
    /// when a newer call to either targets the same selection is silently superseded instead: the
    /// stale transaction is cancelled without ever raising this event, and only the newer call's
    /// outcome is reported.
    /// </remarks>
    public event EventHandler<KittyClipboardReplyEventArgs>? KittyClipboardReplyReceived;

    /// <summary>Writes text to a selection; a no-op when unsupported.</summary>
    /// <remarks>
    /// When the write instead goes through Kitty OSC 5522, the equivalent over-limit condition is not
    /// thrown from here: encoding happens on a posted continuation, so it surfaces asynchronously as
    /// <see cref="Application.Failure"/> and a forced shutdown unless the host observes and handles it.
    /// </remarks>
    /// <param name="text">The text to copy.</param>
    /// <param name="selection">The target selection.</param>
    /// <exception cref="ArgumentException">
    /// The write falls back to OSC 52 and <paramref name="text"/>'s UTF-8 byte length exceeds the
    /// configured clipboard transfer limit.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The dispatcher's bounded post queue was full at the moment of the call, whether the write
    /// went through Kitty OSC 5522 or fell back to OSC 52.
    /// </exception>
    public void Write(ReadOnlySpan<char> text, Selection selection = Selection.Clipboard);

    /// <summary>
    /// Requests a selection's text; a no-op when unsupported. The reply arrives through
    /// <see cref="KittyClipboardReplyReceived"/>.
    /// </summary>
    /// <param name="selection">The target selection.</param>
    /// <exception cref="InvalidOperationException">
    /// The dispatcher's bounded post queue was full at the moment of the call, whether the
    /// request went through Kitty OSC 5522 or fell back to OSC 52.
    /// </exception>
    public void Request(Selection selection = Selection.Clipboard);
}
