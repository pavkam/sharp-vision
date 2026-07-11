namespace SharpVision.Terminal.Protocols;

/// <summary>Contains owned typed values from one terminal query response.</summary>
public readonly record struct Response
{
    /// <summary>Initializes one recognized response.</summary>
    /// <param name="kind">The response family.</param>
    /// <param name="values">Owned numeric response values.</param>
    /// <param name="isSupported">Whether a mode report proves support.</param>
    internal Response(ResponseKind kind, int[] values, bool isSupported = false)
    {
        Kind = kind;
        Values = values;
        IsSupported = isSupported;
    }

    /// <summary>Gets the response family.</summary>
    public ResponseKind Kind { get; }

    /// <summary>Gets owned response values in wire order.</summary>
    public ReadOnlyMemory<int> Values { get; }

    /// <summary>Gets whether a private mode state 1 or 2 proves support.</summary>
    public bool IsSupported { get; }
}
