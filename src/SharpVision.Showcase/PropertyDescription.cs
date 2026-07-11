namespace SharpVision.Showcase;

/// <summary>Stores immutable user-facing documentation for one meaningful control property.</summary>
internal readonly struct PropertyDescription
{
    /// <summary>Initializes one complete property description.</summary>
    /// <param name="name">The non-empty public property name.</param>
    /// <param name="type">The non-empty value shape shown to users.</param>
    /// <param name="defaultValue">The non-empty default or initial-state description.</param>
    /// <param name="description">The non-empty observable behavior description.</param>
    /// <exception cref="ArgumentException">A value is empty or whitespace.</exception>
    internal PropertyDescription(
        string name,
        string type,
        string defaultValue,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Type = type;
        Default = defaultValue;
        Description = description;
    }

    /// <summary>Gets the exact public property name.</summary>
    internal string Name { get; }

    /// <summary>Gets the concise public value shape.</summary>
    internal string Type { get; }

    /// <summary>Gets the documented default or initial state.</summary>
    internal string Default { get; }

    /// <summary>Gets the observable effect and intended use.</summary>
    internal string Description { get; }
}
