// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DataBinding;

using System.Linq.Expressions;

/// <summary>Stores one immutable validated public-property path with cached accessors.</summary>
internal sealed class PropertyPath
{
    private readonly PropertyPathSegment[] _segments;

    private PropertyPath(PropertyPathSegment[] segments) => _segments = segments;

    /// <summary>Gets the number of property segments.</summary>
    public int Count => _segments.Length;

    /// <summary>Gets the final property name.</summary>
    public string LeafName => _segments[^1].Property.Name;

    /// <summary>Creates one validated property-only path.</summary>
    public static PropertyPath Create(LambdaExpression expression, bool requireWritable)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (expression.Parameters.Count != 1)
        {
            throw new ArgumentException("A binding path requires exactly one root parameter.", nameof(expression));
        }

        var properties = new Stack<PropertyInfo>();
        var current = Unwrap(expression.Body);
        var isLeaf = true;

        while (current is MemberExpression { Member: PropertyInfo property, Expression: { } owner })
        {
            if (property.GetMethod is not { IsPublic: true, IsStatic: false } ||
                property.GetIndexParameters().Length != 0)
            {
                throw new ArgumentException(
                    "Binding paths contain only readable public instance properties.",
                    nameof(expression));
            }

            if (!isLeaf && property.PropertyType.IsValueType)
            {
                throw new ArgumentException(
                    "A binding path's intermediate properties must be reference types; only the leaf property may be a value type.",
                    nameof(expression));
            }

            properties.Push(property);
            current = Unwrap(owner);
            isLeaf = false;
        }

        if (!ReferenceEquals(current, expression.Parameters[0]) || properties.Count == 0)
        {
            throw new ArgumentException(
                "A binding path must be rooted in its lambda parameter and contain only properties.",
                nameof(expression));
        }

        var segments = properties.Select(CreateSegment).ToArray();

        return requireWritable && segments[^1].Setter is null
            ? throw new ArgumentException("The binding path leaf must have a public setter.", nameof(expression))
            : new PropertyPath(segments);
    }

    /// <summary>Gets one segment by zero-based position.</summary>
    public PropertyPathSegment SegmentAt(int index) => _segments[index];

    /// <summary>Reads the current leaf or reports an unavailable null intermediate.</summary>
    public bool TryRead(object root, out object? value)
    {
        ArgumentNullException.ThrowIfNull(root);
        var current = root;

        foreach (var segment in _segments)
        {
            if (current is null)
            {
                value = null;
                return false;
            }

            current = segment.Getter(current);
        }

        value = current;
        return true;
    }

    /// <summary>Writes the leaf if every intermediate owner is non-null.</summary>
    /// <returns>
    /// <see langword="true"/> if the leaf was written; <see langword="false"/> if a null
    /// intermediate made the path unreachable. A null intermediate is an ordinary transient data
    /// state - most often a model graph populated asynchronously - not a binding contract
    /// violation, so it is reported as a skipped write rather than thrown.
    /// </returns>
    /// <exception cref="InvalidOperationException">The leaf property has no public setter.</exception>
    public bool Write(object root, object? value)
    {
        ArgumentNullException.ThrowIfNull(root);
        var owner = root;

        for (var index = 0; index < _segments.Length - 1 && owner is not null; index++)
        {
            owner = _segments[index].Getter(owner);
        }

        if (owner is null)
        {
            return false;
        }

        var setter = _segments[^1].Setter ??
                     throw new InvalidOperationException("The binding path leaf is read-only.");
        setter(owner, value);
        return true;
    }

    private static PropertyPathSegment CreateSegment(PropertyInfo property)
    {
        var owner = Expression.Parameter(typeof(object), "owner");
        var access = Expression.Property(Expression.Convert(owner, property.DeclaringType!), property);
        var getter = Expression.Lambda<Func<object, object?>>(
            Expression.Convert(access, typeof(object)),
            owner).Compile();
        Action<object, object?>? setter = null;

        if (property.SetMethod is { IsPublic: true, IsStatic: false })
        {
            var value = Expression.Parameter(typeof(object), "value");
            var assign = Expression.Assign(access, Expression.Convert(value, property.PropertyType));
            setter = Expression.Lambda<Action<object, object?>>(assign, owner, value).Compile();
        }

        return new PropertyPathSegment(property, getter, setter);
    }

    private static Expression Unwrap(Expression expression)
    {
        while (expression is UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked,
                Operand: var operand
            })
        {
            expression = operand;
        }

        return expression;
    }
}
