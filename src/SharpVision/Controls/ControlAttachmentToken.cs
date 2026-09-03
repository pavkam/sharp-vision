// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies one exact dispatcher attachment of one control.</summary>
/// <remarks>
/// The identity is intentionally opaque. Consumers can retain and return it to
/// <see cref="ControlBase.IsCurrent(ControlAttachmentToken)"/>, but cannot infer lifecycle
/// ordering or perform generation arithmetic.
/// </remarks>
internal sealed class ControlAttachmentToken
{
    private ControlBase Control { get; }
    private object Identity { get; }

    /// <summary>Captures one validated attachment identity.</summary>
    /// <param name="control">The exact attached control.</param>
    /// <param name="dispatcher">The exact owning dispatcher.</param>
    /// <param name="identity">The control-owned opaque lifecycle identity.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal ControlAttachmentToken(ControlBase control, Dispatcher dispatcher, object identity)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(identity);

        Control = control;
        Dispatcher = dispatcher;
        Identity = identity;
    }

    /// <summary>Gets the dispatcher captured by this identity for framework marshalling.</summary>
    internal Dispatcher Dispatcher { get; }

    /// <summary>Checks exact owner, dispatcher, and lifecycle identity.</summary>
    /// <param name="control">The candidate owner.</param>
    /// <param name="dispatcher">The candidate dispatcher.</param>
    /// <param name="identity">The candidate opaque lifecycle identity.</param>
    /// <returns>True only when every identity component still matches.</returns>
    internal bool Matches(ControlBase control, Dispatcher? dispatcher, object identity) =>
        ReferenceEquals(Control, control) &&
        ReferenceEquals(Dispatcher, dispatcher) &&
        ReferenceEquals(Identity, identity);
}
