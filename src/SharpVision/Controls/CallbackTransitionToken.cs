// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies one immutable commit in a synchronous logical callback stream.</summary>
internal readonly struct CallbackTransitionToken
{
    private readonly CallbackTransitionStream _stream;
    private readonly ControlBase _owner;
    private readonly object _epoch;
    private readonly ulong _generation;

    /// <summary>Initializes one validated stream identity.</summary>
    internal CallbackTransitionToken(
        CallbackTransitionStream stream,
        ControlBase owner,
        object epoch,
        ulong generation)
    {
        Debug.Assert(stream is not null, "A callback token requires its logical stream.");
        Debug.Assert(owner is not null, "A callback token requires its lifetime owner.");
        Debug.Assert(epoch is not null, "A callback token requires its overflow epoch.");
        _stream = stream;
        _owner = owner;
        _epoch = epoch;
        _generation = generation;
    }

    /// <summary>Gets whether no newer commit or owner disposal has superseded this token.</summary>
    public bool IsCurrent =>
        !_owner.IsDisposed && _stream.IsCurrent(_epoch, _generation);
}
