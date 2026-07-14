// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using System.Diagnostics;


/// <summary>Runs the Release showcase under tmux when the host provides it.</summary>
public sealed class TmuxSmokeTests
{
    /// <summary>Verifies the showcase reaches the geometry specimen under tmux without hanging.</summary>
    [Fact]
    public async Task Showcase_WhenRunUnderTmux_ReachesUnicodeGeometrySpecimenAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "tmux smoke tests require Linux or macOS.");
        Assert.SkipUnless(
            FindExecutable("tmux") is not null,
            "tmux is not installed.");

        string session = $"sharpvision-geometry-{Environment.ProcessId}";
        string root = RepositoryRoot();
        ProcessStartInfo build = new()
        {
            FileName = "dotnet",
            Arguments = "build src/SharpVision.Showcase/SharpVision.Showcase.csproj --configuration Release --verbosity quiet /p:RunAnalyzersDuringBuild=false /p:EnforceCodeStyleInBuild=false",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        Process buildProcess = Process.Start(build).ShouldNotBeNull();
        await buildProcess.WaitForExitAsync(TestContext.Current.CancellationToken);
        buildProcess.ExitCode.ShouldBe(0);

        try
        {
            ProcessStartInfo start = new()
            {
                FileName = "tmux",
                Arguments =
                    $"new-session -d -x 120 -y 40 -s {session} -c \"{root}\" " +
                    "\"dotnet run --project src/SharpVision.Showcase/SharpVision.Showcase.csproj --configuration Release --no-build\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            Process startProcess = Process.Start(start).ShouldNotBeNull();
            await startProcess.WaitForExitAsync(TestContext.Current.CancellationToken);
            startProcess.ExitCode.ShouldBe(0);

            _ = await WaitForPaneTextAsync(session, "Overview", TimeSpan.FromSeconds(15));

            ProcessStartInfo navigate = new()
            {
                FileName = "tmux",
                Arguments = $"send-keys -t {session} " + string.Join(' ', Enumerable.Repeat("Down", 18)),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            Process navigateProcess = Process.Start(navigate).ShouldNotBeNull();
            await navigateProcess.WaitForExitAsync(TestContext.Current.CancellationToken);
            navigateProcess.ExitCode.ShouldBe(0);

            string pane = await WaitForPaneTextAsync(session, "Cell geometry specimen", TimeSpan.FromSeconds(5));
            pane.ShouldContain("Uneven pixel pointer grid");
        }
        finally
        {
            KillSession(session);
        }
    }

    private static async Task<string> ReadPaneAsync(string session)
    {
        ProcessStartInfo capture = new()
        {
            FileName = "tmux",
            Arguments = $"capture-pane -t {session} -p -J",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        Process process = Process.Start(capture).ShouldNotBeNull();
        string text = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        process.ExitCode.ShouldBe(0);
        return text;
    }

    private static async Task<string> WaitForPaneTextAsync(
        string session,
        string needle,
        TimeSpan timeout)
    {
        long deadline = Environment.TickCount64 + (long) timeout.TotalMilliseconds;

        while (Environment.TickCount64 < deadline)
        {
            if (!SessionExists(session))
            {
                throw new InvalidOperationException("The showcase terminated before tmux smoke completed.");
            }

            string text = await ReadPaneAsync(session);

            if (text.Contains(needle, StringComparison.Ordinal))
            {
                return text;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for tmux pane text containing '{needle}'.");
    }

    private static bool SessionExists(string session)
    {
        ProcessStartInfo probe = new()
        {
            FileName = "tmux",
            Arguments = $"has-session -t {session}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process? process = Process.Start(probe);
        return process is not null && process.WaitForExit(1000) && process.ExitCode == 0;
    }

    private static void KillSession(string session)
    {
        ProcessStartInfo kill = new()
        {
            FileName = "tmux",
            Arguments = $"kill-session -t {session}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process? process = Process.Start(kill);
        _ = process?.WaitForExit(1000);
    }

    private static string? FindExecutable(string name)
    {
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, name);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SharpVision.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("The repository root could not be resolved.");
    }
}
