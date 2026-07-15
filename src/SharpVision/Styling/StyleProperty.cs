// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;



/// <summary>Identifies one typed style property declared by a control type.</summary>
/// <typeparam name="T">The validated property value type.</typeparam>
public sealed class StyleProperty<T>: IStyleProperty
{
    private readonly Action<T>? _validate;
    private readonly Dictionary<Type, T> _classDefaults = [];

    private StyleProperty(
        Type declaringType,
        string name,
        string clrName,
        T defaultValue,
        Impact impact,
        Action<T>? validate)
    {
        DeclaringType = declaringType;
        Name = name;
        ClrName = clrName;
        DefaultValue = defaultValue;
        Impact = impact;
        _validate = validate;
    }

    /// <inheritdoc/>
    public Type DeclaringType { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string ClrName { get; }

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
    /// <param name="clrName">
    /// Optional CLR property name reported by change notifications. Defaults to the PascalCase form
    /// of <paramref name="name"/> (for example "border-color" becomes "BorderColor").
    /// </param>
    /// <returns>The registered metadata instance.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty, <typeparamref name="TControl"/> does not derive from
    /// <see cref="Control"/>, or the name is already registered on the declaring type.
    /// </exception>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Typed registration is the public style-property contract.")]
    public static StyleProperty<T> Register<TControl>(
        string name,
        T defaultValue,
        Impact impact,
        Action<T>? validate = null,
        string? clrName = null)
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

        var property = new StyleProperty<T>(
            typeof(TControl),
            name,
            clrName ?? DeriveClrName(name),
            defaultValue,
            impact,
            validate);
        StylePropertyRegistry.Register(property);
        return property;
    }

    private static string DeriveClrName(string name)
    {
        var builder = new StringBuilder(name.Length);
        var capitalizeNext = true;

        foreach (var character in name)
        {
            if (character is '-' or '_')
            {
                capitalizeNext = true;
                continue;
            }

            _ = builder.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
            capitalizeNext = false;
        }

        return builder.Length == 0 ? name : builder.ToString();
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

        for (var current = controlType; current is not null; current = current.BaseType)
        {
            if (_classDefaults.TryGetValue(current, out var candidate))
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
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
