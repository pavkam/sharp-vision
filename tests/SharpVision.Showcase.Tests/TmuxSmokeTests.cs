namespace SharpVision.Showcase.Tests;

using System.Diagnostics;

using Shouldly;

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

        var session = $"sharpvision-geometry-{Environment.ProcessId}";
        var root = RepositoryRoot();
        var build = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build src/SharpVision.Showcase/SharpVision.Showcase.csproj --configuration Release --verbosity quiet /p:RunAnalyzersDuringBuild=false /p:EnforceCodeStyleInBuild=false",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var buildProcess = Process.Start(build).ShouldNotBeNull();
        await buildProcess.WaitForExitAsync(TestContext.Current.CancellationToken);
        buildProcess.ExitCode.ShouldBe(0);

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "tmux",
                Arguments =
                    $"new-session -d -x 120 -y 40 -s {session} -c \"{root}\" " +
                    "\"dotnet run --project src/SharpVision.Showcase/SharpVision.Showcase.csproj --configuration Release --no-build\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            var startProcess = Process.Start(start).ShouldNotBeNull();
            await startProcess.WaitForExitAsync(TestContext.Current.CancellationToken);
            startProcess.ExitCode.ShouldBe(0);

            _ = await WaitForPaneTextAsync(session, "Overview", TimeSpan.FromSeconds(15));

            var navigate = new ProcessStartInfo
            {
                FileName = "tmux",
                Arguments = $"send-keys -t {session} " + string.Join(' ', Enumerable.Repeat("Down", 19)),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            var navigateProcess = Process.Start(navigate).ShouldNotBeNull();
            await navigateProcess.WaitForExitAsync(TestContext.Current.CancellationToken);
            navigateProcess.ExitCode.ShouldBe(0);

            var pane = await WaitForPaneTextAsync(session, "Cell geometry specimen", TimeSpan.FromSeconds(5));
            pane.ShouldContain("Uneven pixel pointer grid");
        }
        finally
        {
            KillSession(session);
        }
    }

    private static async Task<string> ReadPaneAsync(string session)
    {
        var capture = new ProcessStartInfo
        {
            FileName = "tmux",
            Arguments = $"capture-pane -t {session} -p -J",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var process = Process.Start(capture).ShouldNotBeNull();
        var text = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        process.ExitCode.ShouldBe(0);
        return text;
    }

    private static async Task<string> WaitForPaneTextAsync(
        string session,
        string needle,
        TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long) timeout.TotalMilliseconds;

        while (Environment.TickCount64 < deadline)
        {
            if (!SessionExists(session))
            {
                throw new InvalidOperationException("The showcase terminated before tmux smoke completed.");
            }

            var text = await ReadPaneAsync(session);

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
        var probe = new ProcessStartInfo
        {
            FileName = "tmux",
            Arguments = $"has-session -t {session}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(probe);
        return process is not null && process.WaitForExit(1000) && process.ExitCode == 0;
    }

    private static void KillSession(string session)
    {
        var kill = new ProcessStartInfo
        {
            FileName = "tmux",
            Arguments = $"kill-session -t {session}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(kill);
        _ = process?.WaitForExit(1000);
    }

    private static string? FindExecutable(string name)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, name);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

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
