// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Transport;

/// <summary>Smoke-tests installed multiplexers through a real script-owned pseudoterminal.</summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class MultiplexerPseudoterminalTests
{
    /// <summary>Verifies an installed tmux starts on a PTY and relays pane output.</summary>
    [Fact]
    public async Task RunAsync_WhenTmuxIsInstalled_RelaysPaneOutputThroughPseudoterminalAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "tmux pseudoterminal smoke requires Linux or macOS.");
        var executable = OperatingSystem.IsMacOS()
            ? "/opt/homebrew/bin/tmux"
            : "/usr/bin/tmux";
        Assert.SkipUnless(File.Exists(executable), $"tmux executable is not installed at {executable}.");
        var socket = $"sharpvision-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var configuration = Path.Combine(Path.GetTempPath(), $"{socket}.conf");
        await File.WriteAllTextAsync(
            configuration,
            "set -g allow-passthrough all\nset -g status off\n",
            TestContext.Current.CancellationToken);

        try
        {
            const string command =
                "/bin/sleep 0.1; " +
                "/usr/bin/printf '\\033Ptmux;\\033\\033]777;SV_TMUX_SMOKE\\033\\033\\\\\\033\\\\'; " +
                "/bin/sleep 0.1";
            var output = await RunScriptAsync(
                executable,
                ["-L", socket, "-f", configuration, "new-session", command]);

            output.ShouldContain("SV_TMUX_SMOKE");
        }
        finally
        {
            File.Delete(configuration);
        }
    }

    /// <summary>Verifies an installed GNU screen starts on a PTY and relays window output.</summary>
    [Fact]
    public async Task RunAsync_WhenScreenIsInstalled_RelaysWindowOutputThroughPseudoterminalAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "GNU screen pseudoterminal smoke requires Linux or macOS.");
        const string executable = "/usr/bin/screen";
        Assert.SkipUnless(File.Exists(executable), $"GNU screen executable is not installed at {executable}.");
        var session = $"sharpvision-{Environment.ProcessId}-{Guid.NewGuid():N}";

        var output = await RunScriptAsync(
            executable,
            ["-S", session, "/bin/sh", "-c", "/usr/bin/printf SV_SCREEN_SMOKE; /bin/sleep 0.1"]);

        output.ShouldContain("SV_SCREEN_SMOKE");
    }

    /// <summary>Verifies real Screen relays CSI exactly but removes nested XTGETTCAP and DECRQSS terminators.</summary>
    [Fact]
    public async Task RunAsync_WhenScreenRelaysQueries_PreservesCsiAndTruncatesDcsTerminatorsAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "GNU screen pseudoterminal smoke requires Linux or macOS.");
        const string executable = "/usr/bin/screen";
        Assert.SkipUnless(File.Exists(executable), $"GNU screen executable is not installed at {executable}.");
        var session = $"sharpvision-wire-{Environment.ProcessId}-{Guid.NewGuid():N}";
        const string command =
            "/bin/sleep 0.1; " +
            "/usr/bin/printf '\\033P\\033[c\\033\\\\'; " +
            "/usr/bin/printf '\\033P\\033P+q524742\\033\\\\\\033\\\\'; " +
            "/usr/bin/printf '\\033P\\033P$q>m\\033\\\\\\033\\\\'; " +
            "/bin/sleep 0.2";

        var output = await RunScriptAsync(
            executable,
            ["-S", session, "/bin/sh", "-c", command]);

        output.ShouldContain("\u001b[c");
        output.ShouldContain("\u001bP+q524742");
        output.ShouldNotContain("\u001bP+q524742\u001b\\");
        output.ShouldContain("\u001bP$q>m");
        output.ShouldNotContain("\u001bP$q>m\u001b\\");
    }

    private static async Task<string> RunScriptAsync(
        string executable,
        IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("/usr/bin/script")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.Environment["TERM"] = "xterm-256color";
        start.ArgumentList.Add("-q");

        if (OperatingSystem.IsLinux())
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(string.Join(' ', [Quote(executable), .. arguments.Select(Quote)]));
            start.ArgumentList.Add("/dev/null");
        }
        else
        {
            start.ArgumentList.Add("/dev/null");
            start.ArgumentList.Add(executable);

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }
        }

        using var process = Process.Start(start)
                            ?? throw new IOException("The pseudoterminal script process could not start.");
        var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        process.ExitCode.ShouldBe(0, error);
        return output;
    }

    private static string Quote(string value) => $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
}
