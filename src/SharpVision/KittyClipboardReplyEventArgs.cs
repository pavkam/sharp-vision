// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using Terminal.Kitty.Clipboard;

/// <summary>
/// Provides one dispatcher-affine completed clipboard request/write outcome, for either protocol
/// the facade may have used.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="KittyResult"/> or <see cref="Text"/> is non-null on success. Both are
/// null on failure, cancellation, or timeout, in which case <see cref="Failure"/> and/or
/// <see cref="Diagnostic"/> describe the outcome. <see cref="KittyResult"/> must be disposed by the
/// handler once its owned MIME data is no longer needed.
/// </remarks>
[PublicAPI]
public sealed class KittyClipboardReplyEventArgs: EventArgs
{
    /// <summary>Initializes a completed clipboard operation outcome.</summary>
    /// <param name="selection">The selection the operation targeted.</param>
    /// <param name="kittyResult">The owned successful Kitty OSC 5522 MIME result, or null.</param>
    /// <param name="text">The owned successful OSC 52 UTF-8 text reply, or null.</param>
    /// <param name="failure">The terminal-reported Kitty failure status, or <see cref="ReplyStatus.None"/>.</param>
    /// <param name="diagnostic">The redacted local protocol diagnostic, or null.</param>
    public KittyClipboardReplyEventArgs(
        Selection selection,
        Result? kittyResult,
        ReadOnlyMemory<byte>? text,
        ReplyStatus failure,
        Diagnostic? diagnostic)
    {
        Selection = selection;
        KittyResult = kittyResult;
        Text = text;
        Failure = failure;
        Diagnostic = diagnostic;
    }

    /// <summary>Gets the selection the completed operation targeted.</summary>
    public Selection Selection { get; }

    /// <summary>
    /// Gets the owned successful Kitty OSC 5522 MIME result, or null when the completed operation
    /// used OSC 52 or did not succeed.
    /// </summary>
    public Result? KittyResult { get; }

    /// <summary>
    /// Gets the owned successful OSC 52 UTF-8 text reply, or null when the completed operation used
    /// Kitty OSC 5522 or did not succeed.
    /// </summary>
    public ReadOnlyMemory<byte>? Text { get; }

    /// <summary>
    /// Gets the terminal-reported Kitty failure status, or <see cref="ReplyStatus.None"/> for an
    /// OSC 52 outcome, a success, or a local timeout/cancellation/malformed reply.
    /// </summary>
    public ReplyStatus Failure { get; }

    /// <summary>Gets the redacted local protocol diagnostic for a structural failure, or null.</summary>
    public Diagnostic? Diagnostic { get; }

    /// <summary>Gets whether the operation transferred a usable result.</summary>
    public bool IsSuccess => KittyResult is not null || Text is not null;
}
