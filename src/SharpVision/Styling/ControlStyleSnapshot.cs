// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Immutable committed values for one control style, keyed by property and visual state.</summary>
internal sealed class ControlStyleSnapshot
{
    internal static ControlStyleSnapshot Empty { get; } = new([], ChangeImpact.None);

    private readonly Dictionary<(IStyleProperty Property, State State), object> _values;

    internal ControlStyleSnapshot(
        Dictionary<(IStyleProperty Property, State State), object> values,
        ChangeImpact aggregateImpact)
    {
        _values = values;
        AggregateImpact = aggregateImpact;
    }

    internal ChangeImpact AggregateImpact { get; }

    internal bool TryGet(IStyleProperty property, State state, out object? value) =>
        _values.TryGetValue((property, state), out value);

    internal bool TryGet<T>(StyleProperty<T> property, State state, out object? value) =>
        _values.TryGetValue((property, state), out value);
}
