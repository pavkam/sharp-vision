using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;

using RuntimeOptions = SharpVision.Terminal.Runtime.Options;

namespace SharpVision.Showcase;

/// <summary>Creates the explicit terminal-session policy required by the interactive showcase.</summary>
internal static class StartupOptions
{
    #region Construction

    /// <summary>
    /// Produces options that request SGR cell mouse input with held-button drag motion for the showcase.
    /// </summary>
    /// <param name="environment">The non-null process environment copied by the executable host.</param>
    /// <returns>An immutable session policy with explicit app-level cell mouse evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is null.</exception>
    /// <remarks>
    /// The terminal library remains conservative for ordinary hosts. The showcase
    /// deliberately overrides only cell mouse support because clicking controls is
    /// its advertised interactive behavior; terminals that ignore the DEC modes
    /// remain completely keyboard navigable.
    /// </remarks>
    internal static RuntimeOptions Create(IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var capabilities = Detector.Detect(
            environment,
            overrides: new Settings { CellMouse = true });

        return new RuntimeOptions
        {
            Capabilities = capabilities,
            Tracking = MouseTracking.Drag,
            Coordinates = MouseCoordinates.Sgr,
        };
    }

    #endregion
}
