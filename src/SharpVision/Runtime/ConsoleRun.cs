using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;

using TerminalOptions = SharpVision.Terminal.Runtime.Options;

namespace SharpVision.Runtime;

/// <summary>Builds the default interactive console terminal policy.</summary>
internal static class ConsoleRun
{
    /// <summary>Creates negotiated terminal options for one interactive console host.</summary>
    /// <returns>The validated terminal session policy.</returns>
    internal static TerminalOptions CreateTerminalOptions()
    {
        var environment = CaptureEnvironment();
        var overrides = new Settings { CellMouse = true };
        return new TerminalOptions
        {
            Capabilities = Detector.Detect(new Dictionary<string, string?>(), overrides: overrides),
            Negotiation = new NegotiationOptions(environment, overrides),
            Tracking = MouseTracking.Any,
            Coordinates = MouseCoordinates.Sgr,
        };
    }

    /// <summary>Captures the current process environment for terminal negotiation.</summary>
    /// <returns>A mutable dictionary copy of the current environment.</returns>
    internal static Dictionary<string, string?> CaptureEnvironment()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
            {
                environment[key] = entry.Value?.ToString();
            }
        }

        return environment;
    }
}
