// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests.Support;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;

using RuntimeOptions = Terminal.Runtime.Options;

/// <summary>Creates terminal session options for interactive showcase tests.</summary>
internal static class ShowcaseStartupOptions
{
    /// <summary>
    /// Produces options that request SGR cell mouse input with passive pointer motion.
    /// </summary>
    /// <param name="environment">The non-null process environment copied by the test host.</param>
    /// <param name="negotiate">Whether to query bounded runtime evidence before first layout.</param>
    /// <returns>An immutable session policy with explicit cell mouse evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is null.</exception>
    internal static RuntimeOptions Create(
        IReadOnlyDictionary<string, string?> environment,
        bool negotiate = false)
    {
        ArgumentNullException.ThrowIfNull(environment);
        Settings overrides = new Settings { CellMouse = true };
        Capabilities capabilities = negotiate
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
}
