namespace SharpVision.Terminal.Input;

/// <summary>Represents a terminal focus transition.</summary>
/// <param name="Gained">Whether terminal focus was gained rather than lost.</param>
public readonly record struct Focus(bool Gained);
