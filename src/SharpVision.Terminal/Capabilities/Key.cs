namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies one query family and optional correlation ID.</summary>
/// <param name="Kind">The response family.</param>
/// <param name="Id">The optional correlation ID.</param>
internal readonly record struct Key(QueryKind Kind, string? Id);
