using SharpVision.Controls;

namespace SharpVision.Styling;

/// <summary>Describes one registered style property without exposing its value type.</summary>
public interface IStyleProperty
{
    /// <summary>Gets the control type that declared this property.</summary>
    Type DeclaringType { get; }

    /// <summary>Gets the stable serialized property name scoped to the declaring type.</summary>
    string Name { get; }

    /// <summary>Gets the earliest control phase affected by this property.</summary>
    Impact Impact { get; }

    /// <summary>Gets the registered default value.</summary>
    object DefaultValue { get; }

    /// <summary>Gets the most-derived class-default override for one runtime control type.</summary>
    /// <param name="controlType">The concrete control type being styled.</param>
    /// <param name="value">The override when one exists.</param>
    /// <returns>Whether a class default applies.</returns>
    bool TryGetClassDefault(Type controlType, out object? value);

    /// <summary>Validates one candidate value before publication or assignment.</summary>
    /// <param name="value">The candidate value.</param>
    /// <exception cref="ArgumentException">The value is rejected by property validation.</exception>
    void Validate(object? value);

    /// <summary>Determines whether this property is assignable to one runtime control type.</summary>
    /// <param name="controlType">The concrete control type.</param>
    /// <returns>Whether the property may be read or written on the type.</returns>
    bool AppliesTo(Type controlType);
}
