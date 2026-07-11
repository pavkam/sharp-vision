namespace SharpVision.Showcase;

/// <summary>
/// Provides the startup text used until the interactive showcase runtime lands.
/// </summary>
internal static class StartupMessage
{
    /// <summary>
    /// Gets a message that describes the repository's current implementation phase.
    /// </summary>
    /// <returns>The message written by the showcase shell.</returns>
    internal static string Get() =>
        "SharpVision repository foundation is ready. Product specifications start at docs/index.md.";
}
