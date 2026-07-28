// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Writes and requests terminal clipboard selections when supported.</summary>
[PublicAPI]
public interface IClipboard
{
    /// <summary>Gets whether the active terminal advertises clipboard access.</summary>
    public bool IsSupported { get; }

    /// <summary>Writes text to a selection; a no-op when unsupported.</summary>
    /// <param name="text">The text to copy.</param>
    /// <param name="selection">The target selection.</param>
    public void Write(ReadOnlySpan<char> text, Selection selection = Selection.Clipboard);

    /// <summary>Requests a selection's text; replies arrive through ResponseReceived. A no-op when unsupported.</summary>
    /// <param name="selection">The target selection.</param>
    public void Request(Selection selection = Selection.Clipboard);
}
