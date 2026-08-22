// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Embeds one detached, single-line control as an atomic inline flow token.</summary>
/// <remarks>The control becomes a retained descendant of the owning <see cref="Document"/>, so it
/// retains ordinary focus, routed input, styling, and lifetime behavior.</remarks>
[PublicAPI]
public sealed class DocumentInlineControl: DocumentInline
{
    /// <summary>Initializes an inline token around one detached control.</summary>
    /// <param name="control">The non-null detached control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> already has an owner or dispatcher.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="control"/> is disposed.</exception>
    public DocumentInlineControl(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        ObjectDisposedException.ThrowIf(control.IsDisposed, control);

        if (control.Parent is not null || control.Dispatcher is not null)
        {
            throw new ArgumentException("An embedded control must be detached.", nameof(control));
        }

        Control = control;
    }

    /// <summary>Gets the embedded control.</summary>
    public ControlBase Control { get; }
}
