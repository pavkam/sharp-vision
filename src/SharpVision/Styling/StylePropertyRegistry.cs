using System.Runtime.CompilerServices;

namespace SharpVision.Styling;

/// <summary>Read-only catalog of registered style-property metadata keyed by declaring type and name.</summary>
/// <remarks>
/// Registration flows exclusively through <see cref="StyleProperty{T}.Register{TControl}"/>; this
/// type exposes only introspection so tooling (theme editors, serializers, inspectors) can discover
/// the properties a control type participates in without a live control instance.
/// </remarks>
public static class StylePropertyRegistry
{
    private static readonly Lock _gate = new();
    private static readonly Dictionary<Type, Dictionary<string, IStyleProperty>> _properties = [];

    /// <summary>Gets every property declared on one type and its control base types.</summary>
    /// <param name="controlType">The concrete control type.</param>
    /// <returns>Properties from base to derived declaration order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="controlType"/> is null.</exception>
    public static IReadOnlyList<IStyleProperty> GetProperties(Type controlType)
    {
        ArgumentNullException.ThrowIfNull(controlType);
        EnsureRegistered(controlType);

        var result = new List<IStyleProperty>();

        lock (_gate)
        {
            foreach (var type in ControlHierarchy.BaseToDerived(controlType))
            {
                if (_properties.TryGetValue(type, out var byName))
                {
                    result.AddRange(byName.Values);
                }
            }
        }

        return result;
    }

    /// <summary>Finds one property by declaring type and stable serialized name.</summary>
    /// <param name="declaringType">The type that declared the property.</param>
    /// <param name="name">The stable serialized name.</param>
    /// <returns>The metadata instance when registered; otherwise null.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static IStyleProperty? FindProperty(Type declaringType, string name)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(name);
        EnsureRegistered(declaringType);

        lock (_gate)
        {
            return _properties.TryGetValue(declaringType, out var byName) &&
                byName.TryGetValue(name, out var property)
                    ? property
                    : null;
        }
    }

    /// <summary>Registers one validated property, or ignores an identical re-registration.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The validated metadata instance.</param>
    /// <exception cref="ArgumentException">
    /// A different property with the same name is already registered on the declaring type.
    /// </exception>
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

            if (byName.TryGetValue(property.Name, out var existing))
            {
                if (ReferenceEquals(existing, property))
                {
                    return;
                }

                throw new ArgumentException(
                    $"A style property named '{property.Name}' is already registered on {property.DeclaringType.Name}.",
                    nameof(property));
            }

            byName[property.Name] = property;
        }
    }

    /// <summary>Forces the type initializers along a control's chain so registration has run.</summary>
    /// <param name="controlType">The concrete control type being introspected.</param>
    /// <remarks>
    /// Style properties and class defaults are declared in static initializers; introspecting a type
    /// that has never been touched would otherwise observe an empty registration. Running the class
    /// constructors makes registration deterministic regardless of prior access.
    /// </remarks>
    internal static void EnsureRegistered(Type controlType)
    {
        foreach (var type in ControlHierarchy.BaseToDerived(controlType))
        {
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        }
    }
}
