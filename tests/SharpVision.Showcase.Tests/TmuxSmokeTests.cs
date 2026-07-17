// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;



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

        using var gallery = new Gallery();
        var textPageIndex = PageIndex(gallery.Pages, "Text");
        var session = $"sharpvision-geometry-{Environment.ProcessId}";
        var root = RepositoryRoot();
        var build = new ProcessStartInfo()
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
            var createSession = new ProcessStartInfo()
            {
                FileName = "tmux",
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            createSession.ArgumentList.Add("new-session");
            createSession.ArgumentList.Add("-d");
            createSession.ArgumentList.Add("-x");
            createSession.ArgumentList.Add("120");
            createSession.ArgumentList.Add("-y");
            createSession.ArgumentList.Add("40");
            createSession.ArgumentList.Add("-s");
            createSession.ArgumentList.Add(session);
            createSession.ArgumentList.Add("-c");
            createSession.ArgumentList.Add(root);
            await RunTmuxAsync(createSession);

            var retainExitedPane = new ProcessStartInfo()
            {
                FileName = "tmux",
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            retainExitedPane.ArgumentList.Add("set-option");
            retainExitedPane.ArgumentList.Add("-t");
            retainExitedPane.ArgumentList.Add(session);
            retainExitedPane.ArgumentList.Add("remain-on-exit");
            retainExitedPane.ArgumentList.Add("on");
            await RunTmuxAsync(retainExitedPane);

            var startShowcase = new ProcessStartInfo()
            {
                FileName = "tmux",
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startShowcase.ArgumentList.Add("send-keys");
            startShowcase.ArgumentList.Add("-t");
            startShowcase.ArgumentList.Add(session);
            startShowcase.ArgumentList.Add(
                "exec dotnet run --project src/SharpVision.Showcase/SharpVision.Showcase.csproj --configuration Release --no-build");
            startShowcase.ArgumentList.Add("Enter");
            await RunTmuxAsync(startShowcase);

            _ = await WaitForPaneTextAsync(session, "Primary action", TimeSpan.FromSeconds(15));

            var navigate = new ProcessStartInfo()
            {
                FileName = "tmux",
                Arguments =
                    $"send-keys -t {session} " +
                    string.Join(' ', Enumerable.Repeat("Down", textPageIndex)),
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
        var capture = new ProcessStartInfo()
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

    private static async Task<string> ReadPaneStatusAsync(string session)
    {
        var query = new ProcessStartInfo()
        {
            FileName = "tmux",
            Arguments = $"list-panes -t {session} -F \"#{{pane_dead}}:#{{pane_dead_status}}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(query).ShouldNotBeNull();
        var outputTask = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        var output = await outputTask;
        var standardError = await standardErrorTask;
        process.ExitCode.ShouldBe(0, standardError);
        return output.Trim();
    }

    /// <summary>Runs a configured tmux operation and reports its standard error when it fails.</summary>
    /// <param name="startInfo">The fully configured tmux process start information.</param>
    /// <returns>A task that completes after the tmux operation exits successfully.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="startInfo"/> is <see langword="null"/>.</exception>
    private static async Task RunTmuxAsync(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        using var process = Process.Start(startInfo).ShouldNotBeNull();
        var standardError = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        process.ExitCode.ShouldBe(0, standardError);
    }

    private static async Task<string> WaitForPaneTextAsync(
        string session,
        string needle,
        TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long) timeout.TotalMilliseconds;
        var latestPane = string.Empty;

        while (Environment.TickCount64 < deadline)
        {
            latestPane = await ReadPaneAsync(session);
            var paneStatus = await ReadPaneStatusAsync(session);

            if (paneStatus.StartsWith("1:", StringComparison.Ordinal))
            {
                var exitStatus = paneStatus[2..];

                throw new InvalidOperationException(
                    $"The showcase pane exited with status {exitStatus}. Latest pane:{Environment.NewLine}{latestPane}");
            }

            if (latestPane.Contains(needle, StringComparison.Ordinal))
            {
                return latestPane;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException(
            $"Timed out waiting for tmux pane text containing '{needle}'. Latest pane:{Environment.NewLine}{latestPane}");
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

    private static int PageIndex(IReadOnlyList<string> pages, string page)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentException.ThrowIfNullOrWhiteSpace(page);

        for (var index = 0; index < pages.Count; index++)
        {
            if (string.Equals(pages[index], page, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException($"The {page} page is not registered.");
    }

    private static string RepositoryRoot()
    {
        var current = (DirectoryInfo?) new DirectoryInfo(AppContext.BaseDirectory);

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
