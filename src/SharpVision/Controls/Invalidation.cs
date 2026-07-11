namespace SharpVision.Controls;

/// <summary>Identifies dirty UI phases retained until a successful transaction.</summary>
[Flags]
internal enum Invalidation
{
    /// <summary>No phase is dirty.</summary>
    None = 0,

    /// <summary>Semantic cell output must be regenerated.</summary>
    Render = 1,

    /// <summary>Committed bounds must be recalculated.</summary>
    Arrange = 2,

    /// <summary>Intrinsic desired size must be recalculated.</summary>
    Measure = 4,

    /// <summary>Every UI phase is dirty.</summary>
    All = Render | Arrange | Measure,
}
