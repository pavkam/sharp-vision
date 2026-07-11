using System.Diagnostics;

namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies one query family and optional correlation ID.</summary>
internal readonly record struct Key
{
    /// <summary>Initializes one validated query key.</summary>
    /// <param name="kind">The response family.</param>
    /// <param name="id">The optional non-empty correlation ID.</param>
    internal Key(QueryKind kind, string? id)
    {
        Debug.Assert(Enum.IsDefined(kind), "A query key kind must be defined.");
        Debug.Assert(id is null || id.Length > 0, "A present correlation ID cannot be empty.");

        Kind = kind;
        Id = id;
    }

    /// <summary>Gets the response family.</summary>
    internal QueryKind Kind { get; }

    /// <summary>Gets the optional correlation ID.</summary>
    internal string? Id { get; }
}
