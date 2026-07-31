// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Probe;

using System.Globalization;

using Capabilities;

using Terminfo.Ncurses;

/// <summary>Runs one process-isolated terminal-description lookup for integration tests.</summary>
internal static class Program
{
    /// <summary>Loads the requested description and writes only bounded semantic facts.</summary>
    /// <param name="arguments">An optional exact terminal name; otherwise <c>TERM</c> is used.</param>
    /// <returns>Zero after a typed result is written, including unavailable and failed results.</returns>
    internal static int Main(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var terminalName = arguments.Length > 0
            ? arguments[0]
            : Environment.GetEnvironmentVariable("TERM") ?? "dumb";
        var platform = OperatingSystem.IsWindows()
            ? DescriptionPlatform.Windows
            : DescriptionPlatform.Unix;
        var request = new DescriptionRequest(
            terminalName,
            platform,
            outputFileDescriptor: 1,
            DescriptionLimits.Default);
        var result = new Provider().Load(request);

        Console.WriteLine($"status={result.Status}");
        Console.WriteLine($"diagnostics={result.Diagnostics.Count.ToString(CultureInfo.InvariantCulture)}");

        if (result.Profile is { } profile)
        {
            Console.WriteLine($"name={profile.Description.Name}");
            Console.WriteLine($"suitability={profile.Description.Suitability}");
            Console.WriteLine($"colors={profile.Description.Colors?.ToString(CultureInfo.InvariantCulture) ?? "-"}");
            Console.WriteLine($"programs={profile.Programs.Count.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"keys={profile.KeyMap.Bindings.Count.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"cup={profile.Programs.Has("cup")}");
            Console.WriteLine($"sgr0={profile.Programs.Has("sgr0")}");
            Console.WriteLine($"Ms={profile.Programs.Has("Ms")}");
            Console.WriteLine($"Smulx={profile.Programs.Has("Smulx")}");
            Console.WriteLine($"setal={profile.Programs.Has("setal")}");
        }

        return 0;
    }
}
