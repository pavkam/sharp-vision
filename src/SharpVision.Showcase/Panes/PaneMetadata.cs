namespace SharpVision.Showcase.Panes;

/// <summary>Builds immutable showcase property and interaction metadata.</summary>
internal static class PaneMetadata
{
    /// <summary>Creates one property description row.</summary>
    internal static PropertyDescription Property(
        string name,
        string type,
        string defaultValue,
        string description) => new(name, type, defaultValue, description);

    /// <summary>Creates one interaction description row.</summary>
    internal static InteractionDescription Interaction(
        string input,
        string behavior,
        string result) => new(input, behavior, result);
}
