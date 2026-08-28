// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Layout;

/// <summary>Stores one weakly attached value and invalidates only an eligible current layout owner.</summary>
/// <typeparam name="TOwner">The parent type whose layout consumes the attached value.</typeparam>
/// <typeparam name="TValue">The attached value type.</typeparam>
internal sealed class AttachedLayoutProperty<TOwner, TValue>
    where TOwner : ControlBase
{
    private readonly TValue _defaultValue;
    private readonly IEqualityComparer<TValue> _equalityComparer;
    private readonly Invalidation _invalidation;
    private readonly ConditionalWeakTable<ControlBase, StrongBox<TValue>> _values = [];
    private readonly Action<ControlBase, TValue>? _validate;

    /// <summary>Initializes weak storage, validation, equality, and parent invalidation policy.</summary>
    /// <param name="defaultValue">The value returned when a control has no stored association.</param>
    /// <param name="impact">The earliest owner phase affected by a changed value.</param>
    /// <param name="validate">Optional validation performed after mutability preflight and before storage.</param>
    /// <param name="equalityComparer">Optional equality policy used for no-op suppression.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="impact"/> is unknown.</exception>
    public AttachedLayoutProperty(
        TValue defaultValue,
        InvalidationImpact impact,
        Action<ControlBase, TValue>? validate = null,
        IEqualityComparer<TValue>? equalityComparer = null)
    {
        _defaultValue = defaultValue;
        _invalidation = impact switch
        {
            InvalidationImpact.None => Invalidation.None,
            InvalidationImpact.Render => Invalidation.Render,
            InvalidationImpact.Arrange => Invalidation.Arrange,
            InvalidationImpact.Measure => Invalidation.Measure,
            _ => throw new ArgumentOutOfRangeException(nameof(impact), impact, "The invalidation impact is unknown.")
        };
        _validate = validate;
        _equalityComparer = equalityComparer ?? EqualityComparer<TValue>.Default;
    }

    /// <summary>Gets the stored value for one control, or the property default.</summary>
    /// <param name="control">The non-null attached-value owner.</param>
    /// <returns>The current stored or default value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public TValue Get(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _values.TryGetValue(control, out var box) ? box.Value! : _defaultValue;
    }

    /// <summary>Validates and stores one value, invalidating only a matching current parent.</summary>
    /// <param name="control">The non-null mutable attached-value owner.</param>
    /// <param name="value">The candidate value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void Set(ControlBase control, TValue value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.VerifyMutable();
        _validate?.Invoke(control, value);

        if (_equalityComparer.Equals(Get(control), value))
        {
            return;
        }

        _values.GetValue(control, CreateBox).Value = value;

        if (_invalidation != Invalidation.None && control.Parent is TOwner parent)
        {
            parent.Invalidate(_invalidation);
        }
    }

    private StrongBox<TValue> CreateBox(ControlBase control)
    {
        _ = control;
        return new StrongBox<TValue>(_defaultValue);
    }
}
