using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;

using RuntimeOptions = SharpVision.Terminal.Runtime.Options;

namespace SharpVision.Showcase;

/// <summary>Creates the explicit terminal-session policy required by the interactive showcase.</summary>
internal static class StartupOptions
{
    #region Construction

    /// <summary>
    /// Produces options that request SGR cell mouse input with passive pointer motion for the showcase.
    /// </summary>
    /// <param name="environment">The non-null process environment copied by the executable host.</param>
    /// <param name="negotiate">Whether to query bounded runtime evidence before first layout.</param>
    /// <returns>An immutable session policy with explicit app-level cell mouse evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is null.</exception>
    /// <remarks>
    /// The terminal library remains conservative for ordinary hosts. The showcase
    /// deliberately overrides only cell mouse support because clicking controls is
    /// its advertised interactive behavior; terminals that ignore the DEC modes
    /// remain completely keyboard navigable.
    /// </remarks>
    internal static RuntimeOptions Create(
        IReadOnlyDictionary<string, string?> environment,
        bool negotiate = false)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var overrides = new Settings { CellMouse = true };
        var capabilities = negotiate
            ? Detector.Detect(new Dictionary<string, string?>(), overrides: overrides)
            : Detector.Detect(environment, overrides: overrides);

        return new RuntimeOptions
        {
            Capabilities = capabilities,
            Negotiation = negotiate
                ? new NegotiationOptions(environment, overrides)
                : null,
            Tracking = MouseTracking.Any,
            Coordinates = MouseCoordinates.Sgr,
        };
    }

    #endregion
}
