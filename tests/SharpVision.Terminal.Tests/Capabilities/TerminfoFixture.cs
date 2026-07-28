// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

/// <summary>Owns one temporary source and ncurses-compiled terminfo database.</summary>
internal sealed class TerminfoFixture: IDisposable
{
    private readonly DirectoryInfo? _directory;

    private TerminfoFixture(
        string name,
        string database,
        string availability,
        DirectoryInfo? directory)
    {
        Name = name;
        Database = database;
        Availability = availability;
        _directory = directory;
    }

    /// <summary>Gets the exact compiled terminal name.</summary>
    internal string Name { get; }

    /// <summary>Gets the temporary compiled database directory.</summary>
    internal string Database { get; }

    /// <summary>Gets the typed fixture/tool availability.</summary>
    internal string Availability { get; }

    /// <summary>Gets whether <c>tic</c> produced the fixture database.</summary>
    internal bool IsAvailable => Availability == "Available";

    /// <summary>Attempts to compile one deterministic fixture with the host <c>tic</c> tool.</summary>
    internal static TerminfoFixture TryCreate(string name, int colors, bool includeExtensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(colors);

        var directory = Directory.CreateTempSubdirectory("sharpvision-terminfo-");
        var database = Path.Combine(directory.FullName, "database");
        var source = Path.Combine(directory.FullName, "fixture.info");
        _ = Directory.CreateDirectory(database);
        var extensions = includeExtensions
            ? ", Ms=\\E]52;%p1%s;%p2%s\\007, Smulx=\\E[4\\:%p1%dm, setal=\\E[5\\:%p1%dm"
            : string.Empty;
        File.WriteAllText(
            source,
            $"{name}|SharpVision fixture, am, cols#91, lines#37, colors#{colors.ToString(CultureInfo.InvariantCulture)}, cup=\\E[%i%p1%d;%p2%dH, sgr0=\\E[0m, clear=\\E[H\\E[2J{extensions},{Environment.NewLine}",
            Encoding.ASCII);
        var start = new ProcessStartInfo
        {
            FileName = "tic",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("-x");
        start.ArgumentList.Add("-o");
        start.ArgumentList.Add(database);
        start.ArgumentList.Add(source);

        try
        {
            using var process = Process.Start(start);

            if (process is null)
            {
                return new TerminfoFixture(name, database, "ToolUnavailable", directory);
            }

            process.WaitForExit();
            return new TerminfoFixture(
                name,
                database,
                process.ExitCode == 0 ? "Available" : "ToolUnavailable",
                directory);
        }
        catch (Win32Exception)
        {
            return new TerminfoFixture(name, database, "ToolUnavailable", directory);
        }
    }

    /// <summary>Deletes the temporary fixture database.</summary>
    public void Dispose() => _directory?.Delete(recursive: true);
}
