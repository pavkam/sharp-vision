// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Probe;

using System.Globalization;
using System.Runtime.Versioning;

using Capabilities;

using Terminfo.Ncurses;

/// <summary>Runs one process-isolated terminal-description lookup for integration tests.</summary>
internal static class Program
{
    /// <summary>
    /// Dispatches to the terminal-description probe, or, when the first argument is
    /// <c>conpty</c>, to <see cref="ConPtyProbe"/> for the Windows pseudo-console fixture
    /// (see <see cref="ConPtyProbe"/> and issue #35).
    /// </summary>
    /// <param name="arguments">An optional exact terminal name; otherwise <c>TERM</c> is used.</param>
    /// <returns>Zero after a typed result is written, including unavailable and failed results.</returns>
    internal static int Main(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Length > 0 && string.Equals(arguments[0], "conpty", StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("The conpty probe scenario requires Windows.");
                return 1;
            }

            return RunConPtyProbe(arguments[1..]);
        }

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

    [SupportedOSPlatform("windows")]
    private static int RunConPtyProbe(string[] arguments) =>
        ConPtyProbe.RunAsync(arguments).GetAwaiter().GetResult();
}
