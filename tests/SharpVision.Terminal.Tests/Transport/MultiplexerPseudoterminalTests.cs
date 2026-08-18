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

    /// <summary>
    /// Verifies a raw, unwrapped DECRQSS-shaped reply delivered into a real tmux 3.7 pane reaches
    /// that pane's stdin exactly as tmux forwards it - with no `tmux;` passthrough envelope,
    /// contradicting the symmetric-rewrap assumption a passthrough writer might make - and that
    /// <see cref="ProtocolRouter"/>'s incidental ordinary-decoder fallback still recognizes that real
    /// byte shape correctly once the wrapped-envelope candidate match fails. The reply is delivered
    /// through <c>tmux send-keys -l</c>, tmux's own literal-key injection targeting the pane's real
    /// pty: the pane cannot distinguish this from bytes a real outer terminal typed, since both cross
    /// the same internal write-to-pane path, and passthrough wrapping only ever applies to the
    /// opposite (pane-to-outer) direction. Matches the forwarding behavior described in upstream
    /// <see href="https://github.com/tmux/tmux/issues/4386">tmux/tmux#4386</see>.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenTmuxDeliversRawReply_ReachesPaneAndFallbackDecoderAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "tmux pseudoterminal smoke requires Linux or macOS.");
        var executable = OperatingSystem.IsMacOS()
            ? "/opt/homebrew/bin/tmux"
            : "/usr/bin/tmux";
        Assert.SkipUnless(File.Exists(executable), $"tmux executable is not installed at {executable}.");
        var socket = $"sharpvision-reply-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var capture = Path.Combine(Path.GetTempPath(), $"{socket}.bin");
        var ready = Path.Combine(Path.GetTempPath(), $"{socket}.ready");
        var reply = "\u001bP1$r0m\u001b\\"u8.ToArray();

        try
        {
            // The pane touches a marker file immediately after "stty raw -echo" so the test can wait
            // for that exact readiness signal instead of guessing a fixed delay - a canonical pty
            // would otherwise buffer these newline-free bytes until a line ending arrives if
            // injection raced ahead of the mode switch.
            var paneCommand = $"/bin/stty raw -echo; /usr/bin/touch {ready}; " +
                               $"/usr/bin/head -c 9 > {capture}; /bin/sleep 2";
            await RunTmuxAsync(
                executable,
                ["-L", socket, "new-session", "-d", "-x", "80", "-y", "24", "-s", "sv-target", paneCommand]);

            try
            {
                var readyWatch = Stopwatch.StartNew();

                while (!File.Exists(ready) && readyWatch.Elapsed < TimeSpan.FromSeconds(5))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
                }

                File.Exists(ready).ShouldBeTrue("the pane did not reach \"stty raw -echo\" within 5 seconds.");

                // DECRQSS is not itself a printable key, so it is delivered as two literal segments
                // split around a named "Escape" key - "send-keys -l" alone cannot express ESC.
                await RunTmuxAsync(executable, ["-L", socket, "send-keys", "-t", "sv-target", "Escape"]);
                await RunTmuxAsync(executable, ["-L", socket, "send-keys", "-l", "-t", "sv-target", "--", "P1$r0m"]);
                await RunTmuxAsync(executable, ["-L", socket, "send-keys", "-t", "sv-target", "Escape"]);
                await RunTmuxAsync(executable, ["-L", socket, "send-keys", "-l", "-t", "sv-target", "--", "\\"]);

                var watch = Stopwatch.StartNew();
                var captured = Array.Empty<byte>();

                while (watch.Elapsed < TimeSpan.FromSeconds(5))
                {
                    if (File.Exists(capture))
                    {
                        captured = await File.ReadAllBytesAsync(capture, TestContext.Current.CancellationToken);

                        if (captured.Length >= reply.Length)
                        {
                            break;
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
                }

                captured.ShouldBe(reply);
            }
            finally
            {
                await RunTmuxAsync(executable, ["-L", socket, "kill-server"], allowFailure: true);
            }
        }
        finally
        {
            File.Delete(capture);
            File.Delete(ready);
        }

        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.CapabilityQueries);
        var route = new MultiplexerRoute(policy);
        var sink = new RecordingProtocolSink();
        using var router = new ProtocolRouter(sink, route: route);

        router.Route(reply);

        var response = sink.StatusResponses.ShouldHaveSingleItem();
        response.Name.ShouldBe(StatusName.Rendition);
        response.Valid.ShouldBeTrue();
        response.Value.ToArray().ShouldBe("0m"u8.ToArray());
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
        var start = CreateScriptStartInfo(executable, arguments);
        using var process = Process.Start(start)
                            ?? throw new IOException("The pseudoterminal script process could not start.");
        var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        process.ExitCode.ShouldBe(0, error);
        return output;
    }

    /// <summary>Runs one tmux client invocation directly (no pty of its own is needed - tmux owns a
    /// real pty per pane internally regardless of client attachment) and waits for it to exit.</summary>
    private static async Task RunTmuxAsync(
        string executable,
        IReadOnlyList<string> arguments,
        bool allowFailure = false)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
                            ?? throw new IOException("The tmux client process could not start.");
        var error = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        _ = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        if (!allowFailure)
        {
            process.ExitCode.ShouldBe(0, error);
        }
    }

    private static ProcessStartInfo CreateScriptStartInfo(
        string executable,
        IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("/usr/bin/script")
        {
            // Redirecting (rather than inheriting) stdin keeps the host test process's own stdin -
            // which is not a terminal under a test runner - from leaking stray bytes into the pty.
            RedirectStandardInput = true,
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

        return start;
    }

    private static string Quote(string value) => $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
}
