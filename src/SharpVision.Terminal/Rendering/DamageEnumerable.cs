// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Provides an allocation-free damage enumerator over borrowed active frames.</summary>
[PublicAPI]
public readonly ref struct DamageEnumerable
{
    private readonly Frame? _front;
    private readonly Frame _back;
    private readonly bool _full;
    private readonly VerticalScrollDamage _scroll;
    private readonly GraphicsCellOverlay? _frontOverlay;
    private readonly GraphicsCellOverlay? _backOverlay;

    /// <summary>Initializes a borrowed damage enumerable.</summary>
    /// <param name="front">The optional committed frame.</param>
    /// <param name="back">The target frame.</param>
    /// <param name="full">Whether complete damage is required.</param>
    internal DamageEnumerable(Frame? front, Frame back, bool full)
        : this(front, back, full, default, null, null)
    {
    }

    /// <summary>Initializes damage after an optional already-emitted scroll transform.</summary>
    /// <param name="front">The optional committed frame.</param>
    /// <param name="back">The target frame.</param>
    /// <param name="full">Whether complete damage is required.</param>
    /// <param name="scroll">The optional scroll transform already applied to the terminal.</param>
    /// <param name="frontOverlay">The optional committed protocol cell projection.</param>
    /// <param name="backOverlay">The optional target protocol cell projection.</param>
    internal DamageEnumerable(
        Frame? front,
        Frame back,
        bool full,
        VerticalScrollDamage scroll,
        GraphicsCellOverlay? frontOverlay = null,
        GraphicsCellOverlay? backOverlay = null)
    {
        _front = front;
        _back = back;
        _full = full;
        _scroll = scroll;
        _frontOverlay = frontOverlay;
        _backOverlay = backOverlay;
    }

    /// <summary>Creates an enumerator positioned before the first changed run.</summary>
    /// <returns>The damage enumerator.</returns>
    public DamageEnumerator GetEnumerator() => new(
        _front,
        _back,
        _full,
        _scroll,
        _frontOverlay,
        _backOverlay);
}
