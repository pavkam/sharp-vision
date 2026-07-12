using SharpVision.Controls;

namespace SharpVision.Styling;

/// <summary>Stores registered style-property metadata keyed by declaring type and name.</summary>
internal static class StylePropertyRegistry
{
    private static readonly Lock _gate = new();
    private static readonly Dictionary<Type, Dictionary<string, IStyleProperty>> _properties = [];

    /// <summary>Registers one validated property or rejects the attempt without mutation.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The validated metadata instance.</param>
    /// <exception cref="ArgumentException">The declaring type or name is already registered.</exception>
    internal static void Register<T>(StyleProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);

        lock (_gate)
        {
            if (!_properties.TryGetValue(property.DeclaringType, out var byName))
            {
                byName = new Dictionary<string, IStyleProperty>(StringComparer.Ordinal);
                _properties[property.DeclaringType] = byName;
            }

            if (byName.ContainsKey(property.Name))
            {
                throw new ArgumentException(
                    $"A style property named '{property.Name}' is already registered on {property.DeclaringType.Name}.",
                    nameof(property));
            }

            byName[property.Name] = property;
        }
    }

    /// <summary>Gets every property declared on one type and its control base types.</summary>
    /// <param name="controlType">The concrete control type.</param>
    /// <returns>Properties from base to derived declaration order.</returns>
    internal static IReadOnlyList<IStyleProperty> GetProperties(Type controlType)
    {
        ArgumentNullException.ThrowIfNull(controlType);

        var chain = new List<Type>();
        for (var current = controlType; current != typeof(object); current = current.BaseType)
        {
            if (!typeof(Control).IsAssignableFrom(current))
            {
                break;
            }

            chain.Add(current);
        }

        chain.Reverse();
        var result = new List<IStyleProperty>();

        lock (_gate)
        {
            foreach (var type in chain)
            {
                if (_properties.TryGetValue(type, out var byName))
                {
                    result.AddRange(byName.Values);
                }
            }
        }

        return result;
    }

    /// <summary>Gets one property by declaring type and stable name.</summary>
    /// <param name="declaringType">The type that declared the property.</param>
    /// <param name="name">The stable serialized name.</param>
    /// <returns>The metadata instance when registered.</returns>
    internal static IStyleProperty? TryGet(Type declaringType, string name)
    {
        lock (_gate)
        {
            return _properties.TryGetValue(declaringType, out var byName) &&
                byName.TryGetValue(name, out var property)
                    ? property
                    : null;
        }
    }
}
