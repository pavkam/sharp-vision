// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Provides dispatcher-affine selected text for application-owned clipboard publication.</summary>
/// <remarks>
/// Copying is synchronous and returns independently owned text without publishing clipboard or
/// terminal protocol state. Implementations preserve their text and selection state.
/// </remarks>
[PublicAPI]
public interface IClipboardCopySource
{
    /// <summary>Copies the current selection without publishing it to a clipboard.</summary>
    /// <returns>An independently owned string, or empty when no text may be copied.</returns>
    /// <exception cref="InvalidOperationException">
    /// The attached source is accessed away from its owning dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    public string CopySelection();
}
