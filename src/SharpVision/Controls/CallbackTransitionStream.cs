// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Produces opaque identities for one synchronous logical callback stream.</summary>
/// <remarks>
/// A derived control declares one field of this type per logical multi-property transition - such
/// as a compound state change that raises several <see cref="ControlBase.PropertyChanged"/>
/// notifications that must all stop publishing together once a reentrant callback supersedes the
/// transition - and passes it to <c>SetTransitionProperty</c>/<c>BeginPropertyTransition</c> and
/// <c>PublishTransitionProperty</c> on <see cref="ControlBase"/>. A stream is retained by its owner,
/// so ordinary commits allocate no identity object. The epoch changes only when the numeric
/// generation wraps, keeping every earlier token permanently stale.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "This type identifies a logical callback sequence, not the System.IO.Stream contract CA1711 guards against.")]
public sealed class CallbackTransitionStream
{
    private object _epoch = new();
    private ulong _generation;

    /// <summary>Initializes a stream before its first commit.</summary>
    public CallbackTransitionStream()
    {
    }

    /// <summary>Initializes a stream at a specific generation for deterministic overflow proof.</summary>
    /// <param name="generation">The initial numeric generation.</param>
    internal CallbackTransitionStream(ulong generation) => _generation = generation;

    /// <summary>Advances the stream and returns the new immutable commit identity.</summary>
    /// <param name="owner">The non-null control whose disposal invalidates the identity.</param>
    /// <returns>The new current token.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public CallbackTransitionToken Commit(ControlBase owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (_generation == ulong.MaxValue)
        {
            _epoch = new object();
            _generation = 0;
        }
        else
        {
            _generation++;
        }

        return new CallbackTransitionToken(this, owner, _epoch, _generation);
    }

    /// <summary>Captures the current identity without advancing the stream.</summary>
    /// <param name="owner">The non-null control whose disposal invalidates the identity.</param>
    /// <returns>The current token.</returns>
    internal CallbackTransitionToken Capture(ControlBase owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new CallbackTransitionToken(this, owner, _epoch, _generation);
    }

    /// <summary>Gets whether one captured epoch and generation still identify this stream.</summary>
    /// <param name="epoch">The captured epoch identity.</param>
    /// <param name="generation">The captured numeric generation.</param>
    /// <returns>True only for the current stream identity.</returns>
    internal bool IsCurrent(object epoch, ulong generation) =>
        ReferenceEquals(_epoch, epoch) && _generation == generation;
}
