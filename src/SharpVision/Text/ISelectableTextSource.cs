// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>
/// Projects a control's complete semantic text and currently visible grapheme geometry without
/// exposing selection or input state.
/// </summary>
/// <remarks>
/// Snapshot glyph rectangles use the source control's local cell coordinates. Clipping may omit
/// glyph rectangles, but it does not remove their semantic text. When the source is attached, the
/// snapshot must be requested on its owning dispatcher.
/// </remarks>
[PublicAPI]
public interface ISelectableTextSource
{
    /// <summary>Gets a cheap wrapping generation that changes when the source may need reprojection.</summary>
    /// <remarks>
    /// The default follows retained-control invalidation automatically, so custom controls do not
    /// implement or manually maintain a second change-notification mechanism. Consumers compare
    /// equality only and request an exact snapshot after a change; a generation change does not by
    /// itself imply semantic text changed. Non-control implementations return zero and may expose a
    /// more precise generation when they own mutable projection state.
    /// </remarks>
    public ulong SelectableTextVersion => this is ControlBase control
        ? control.SelectableTextInvalidationVersion
        : 0;

    /// <summary>Creates an immutable, independently owned snapshot of the current projection.</summary>
    /// <returns>
    /// A snapshot whose semantic text survives clipping and whose glyph storage cannot be changed
    /// through the source or caller.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The attached source is accessed away from its owning dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    public SelectableTextSnapshot GetSelectableTextSnapshot();
}
