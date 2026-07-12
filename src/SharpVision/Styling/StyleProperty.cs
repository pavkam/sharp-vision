using SharpVision.Controls;

namespace SharpVision.Styling;

/// <summary>Identifies one typed style property declared by a control type.</summary>
/// <typeparam name="T">The validated property value type.</typeparam>
public sealed class StyleProperty<T> : IStyleProperty
{
    private readonly Action<T>? _validate;
    private readonly Dictionary<Type, T> _classDefaults = [];

    private StyleProperty(
        Type declaringType,
        string name,
        T defaultValue,
        Impact impact,
        Action<T>? validate)
    {
        DeclaringType = declaringType;
        Name = name;
        DefaultValue = defaultValue;
        Impact = impact;
        _validate = validate;
    }

    /// <inheritdoc/>
    public Type DeclaringType { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>Gets the registered default value.</summary>
    public T DefaultValue { get; }

    /// <inheritdoc/>
    object IStyleProperty.DefaultValue => DefaultValue!;

    /// <inheritdoc/>
    public Impact Impact { get; }

    /// <summary>Registers one style property on a declaring control type.</summary>
    /// <typeparam name="TControl">The control type that owns the property.</typeparam>
    /// <param name="name">The stable non-empty serialized name.</param>
    /// <param name="defaultValue">The default value used before theme resolution.</param>
    /// <param name="impact">The earliest affected control phase.</param>
    /// <param name="validate">Optional validation invoked before publication or assignment.</param>
    /// <returns>The registered metadata instance.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty, <typeparamref name="TControl"/> does not derive from
    /// <see cref="Control"/>, or the name is already registered on the declaring type.
    /// </exception>
    public static StyleProperty<T> Register<TControl>(
        string name,
        T defaultValue,
        Impact impact,
        Action<T>? validate = null)
        where TControl : Control
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A style property name cannot be empty.", nameof(name));
        }

        if (!Enum.IsDefined(impact))
        {
            throw new ArgumentOutOfRangeException(nameof(impact), impact, "The style impact is unknown.");
        }

        validate?.Invoke(defaultValue);

        var property = new StyleProperty<T>(typeof(TControl), name, defaultValue, impact, validate);
        StylePropertyRegistry.Register(property);
        return property;
    }

    /// <summary>Registers a class-default override for one derived control type.</summary>
    /// <typeparam name="TDerived">The derived control type receiving the default.</typeparam>
    /// <param name="value">The validated default value.</param>
    /// <returns>This metadata instance.</returns>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TDerived"/> does not derive from the declaring type or a default
    /// is already registered for the type.
    /// </exception>
    public StyleProperty<T> RegisterClassDefault<TDerived>(T value)
        where TDerived : Control
    {
        if (!DeclaringType.IsAssignableFrom(typeof(TDerived)))
        {
            throw new ArgumentException(
                $"{typeof(TDerived).Name} does not derive from {DeclaringType.Name}.",
                nameof(value));
        }

        ValidateValue(value);

        if (_classDefaults.ContainsKey(typeof(TDerived)))
        {
            throw new ArgumentException(
                $"A class default for {typeof(TDerived).Name} is already registered on '{Name}'.",
                nameof(value));
        }

        _classDefaults[typeof(TDerived)] = value;
        return this;
    }

    /// <inheritdoc/>
    public bool TryGetClassDefault(Type controlType, out object? value)
    {
        ArgumentNullException.ThrowIfNull(controlType);

        T? resolved = default;
        var found = false;

        for (var current = controlType; current is not null; current = current.BaseType)
        {
            if (_classDefaults.TryGetValue(current, out var candidate))
            {
                resolved = candidate;
                found = true;
            }
        }

        value = resolved;
        return found;
    }

    /// <inheritdoc/>
    public bool AppliesTo(Type controlType) =>
        DeclaringType.IsAssignableFrom(controlType);

    /// <summary>Validates one candidate value.</summary>
    /// <param name="value">The candidate value.</param>
    /// <exception cref="ArgumentException">The value is rejected by property validation.</exception>
    public void ValidateValue(T value) => _validate?.Invoke(value);

    /// <inheritdoc/>
    public void Validate(object? value)
    {
        if (value is null && default(T) is not null)
        {
            throw new ArgumentException("The value cannot be null.", nameof(value));
        }

        ValidateValue((T) value!);
    }
}
