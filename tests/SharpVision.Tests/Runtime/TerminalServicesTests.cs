// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using System.Buffers;

using SharpVision.Runtime;

using Terminal.Clipboard;
using Terminal.Kitty.Clipboard;
using Terminal.Multiplexing;

// SharpVision.Input.Selection is in scope through the test global usings; the clipboard's own
// Selection is a different type entirely.
using ClipboardSelection = Terminal.Clipboard.Selection;

/// <summary>Verifies the terminal output services facade exposes a working bell and clipboard.</summary>
public sealed class TerminalServicesTests
{
    /// <summary>Verifies the application and facade publish their immutable terminal description.</summary>
    [Fact]
    public async Task Description_WhenApplicationIsConstructed_PublishesSelectedProfileAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);

        application.TerminalProfile.ShouldBeSameAs(TerminalOptions.Minimal.Profile);
        application.Terminal.Description.ShouldBeSameAs(application.TerminalProfile.Description);
    }

    /// <summary>
    /// Verifies clipboard cleanup during shutdown runs on the owning dispatcher thread, matching
    /// the invariant every other mutator of this pending clipboard state already asserts with
    /// <see cref="Dispatcher.VerifyAccess"/>. A live query's deadline timer ticks on that same
    /// thread; disposing from off-thread instead would race those ticks on the same in-flight,
    /// single-threaded transaction with no lock between them.
    /// </summary>
    [Fact]
    public async Task Dispose_WhenApplicationStops_RunsOnTheOwningDispatcherThreadAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);

        ((TerminalServices) application.Terminal).DisposedOnDispatcherThreadForTests.ShouldBeTrue();
    }

    /// <summary>Verifies bell bytes come from the selected description program.</summary>
    [Fact]
    public async Task Bell_WhenDescriptionSuppliesProgram_EmitsExactDescribedBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var profile = CreateProfile(new Dictionary<string, DescriptionProgram>
        {
            ["bel"] = new DescriptionProgram("DESCRIBED-BELL"u8)
        });
        var options = TerminalOptions.Minimal with { Profile = profile };
        var bell = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            if (memory.Span.SequenceEqual("DESCRIBED-BELL"u8))
            {
                _ = bell.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Bell.IsSupported.ShouldBeTrue();
        application.Terminal.Bell.Ring();

        await bell.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies unsupported bell and title operations are byte-quiet no-ops.</summary>
    [Fact]
    public async Task OutputServices_WhenDescriptionDoesNotSupportThem_AreByteQuietAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var options = TerminalOptions.Minimal with
        {
            Profile = CreateProfile(new Dictionary<string, DescriptionProgram>())
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.Bell.IsSupported.ShouldBeFalse();
        application.Terminal.IsTitleSupported.ShouldBeFalse();
        application.Terminal.Bell.Ring();
        application.Terminal.SetTitle("ignored");
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an exact described TS/fsl pair surrounds UTF-8 title bytes.</summary>
    [Fact]
    public async Task SetTitle_WhenDescriptionSuppliesTsAndFsl_EmitsExactPairedBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var profile = CreateProfile(new Dictionary<string, DescriptionProgram>
        {
            ["TS"] = new DescriptionProgram("PREFIX:"u8),
            ["fsl"] = new DescriptionProgram(":SUFFIX"u8)
        });
        var options = TerminalOptions.Minimal with { Profile = profile };
        var title = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            if (memory.Span.SequenceEqual("PREFIX:Olá:SUFFIX"u8))
            {
                _ = title.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.IsTitleSupported.ShouldBeTrue();
        application.Terminal.SetTitle("Olá");

        await title.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies described titles reject control-bearing payloads before expanding or queuing bytes.</summary>
    /// <param name="title">The title containing a forbidden terminal control.</param>
    [Theory]
    [InlineData("bad\a title")]
    [InlineData("bad\u001b]2;injected")]
    public async Task SetTitle_WhenDescribedPayloadContainsControl_ThrowsWithoutWritingAsync(string title)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var profile = CreateProfile(new Dictionary<string, DescriptionProgram>
        {
            ["TS"] = new DescriptionProgram("PREFIX:"u8),
            ["fsl"] = new DescriptionProgram(":SUFFIX"u8)
        });
        var options = TerminalOptions.Minimal with { Profile = profile };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        _ = Should.Throw<ArgumentException>(() => application.Terminal.SetTitle(title));
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies incomplete, parameterized, and mixed title pairs are unsupported and byte-quiet.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SetTitle_WhenDescriptionPairIsInvalid_IsByteQuietAsync(int scenario)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var programs = scenario switch
        {
            0 => new Dictionary<string, DescriptionProgram>
            {
                ["TS"] = new DescriptionProgram("PREFIX"u8)
            },
            1 => new Dictionary<string, DescriptionProgram>
            {
                ["TS"] = new DescriptionProgram("%p1%s"u8),
                ["fsl"] = new DescriptionProgram("SUFFIX"u8)
            },
            2 => new Dictionary<string, DescriptionProgram>
            {
                ["TS"] = DescriptionProgram.Intrinsic,
                ["fsl"] = new DescriptionProgram("SUFFIX"u8)
            },
            _ => new Dictionary<string, DescriptionProgram>
            {
                ["TS"] = new DescriptionProgram("%{1}%PA"u8),
                ["fsl"] = new DescriptionProgram("SUFFIX"u8)
            }
        };
        var options = TerminalOptions.Minimal with { Profile = CreateProfile(programs) };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.IsTitleSupported.ShouldBeFalse();
        application.Terminal.SetTitle("ignored");
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the OSC 2 title path emits unwrapped bytes when no multiplexer route is
    /// configured, matching today's default behavior for a route-free session.</summary>
    [Fact]
    public async Task SetTitle_WhenNoMultiplexerRoute_EmitsUnwrappedAnsiTitleBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var title = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            if (memory.Span.SequenceEqual("]2;hi\\"u8))
            {
                _ = title.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.IsTitleSupported.ShouldBeTrue();
        application.Terminal.SetTitle("hi");

        await title.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an explicitly authorized tmux title route wraps the OSC 2 string in DCS
    /// passthrough before the ordered out-of-band write reaches the transport, matching the
    /// existing clipboard and notification routing precedent instead of posting bare bytes that
    /// tmux would otherwise swallow.</summary>
    [Fact]
    public async Task SetTitle_WhenTmuxRouteApprovesTitleFamily_WrapsAnsiTitleBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Title);
        var options = TerminalOptions.Minimal with { Multiplexing = policy };
        var written = new TaskCompletionSource<ReadOnlyMemory<byte>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += bytes =>
        {
            if (bytes.Span.IndexOf("Ptmux;"u8) >= 0)
            {
                _ = written.TrySetResult(bytes.ToArray());
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.SetTitle("hi");

        var bytes = await written.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        bytes.ToArray().ShouldBe(
            "Ptmux;]2;hi\\\\"u8.ToArray());
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a configured multiplexer route that has not approved the title family
    /// suppresses the OSC 2 title entirely rather than posting an unwrapped OSC that the
    /// multiplexer would otherwise consume and never forward.</summary>
    [Fact]
    public async Task SetTitle_WhenTmuxRouteDoesNotApproveTitleFamily_AnsiPathIsSuppressedAndByteQuietAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Clipboard);
        var options = TerminalOptions.Minimal with { Multiplexing = policy };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.IsTitleSupported.ShouldBeTrue();
        application.Terminal.SetTitle("hi");
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an explicitly authorized tmux title route also wraps the described TS/fsl
    /// title program's bytes, matching the OSC 2 path's routing exactly.</summary>
    [Fact]
    public async Task SetTitle_WhenTmuxRouteApprovesTitleFamily_WrapsDescribedTitleBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var profile = CreateProfile(new Dictionary<string, DescriptionProgram>
        {
            ["TS"] = new DescriptionProgram("PREFIX:"u8),
            ["fsl"] = new DescriptionProgram(":SUFFIX"u8)
        });
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            profile,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Title);
        var options = TerminalOptions.Minimal with { Profile = profile, Multiplexing = policy };
        var written = new TaskCompletionSource<ReadOnlyMemory<byte>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += bytes =>
        {
            if (bytes.Span.IndexOf("Ptmux;"u8) >= 0)
            {
                _ = written.TrySetResult(bytes.ToArray());
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.IsTitleSupported.ShouldBeTrue();
        application.Terminal.SetTitle("hi");

        var bytes = await written.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        bytes.ToArray().ShouldBe("Ptmux;PREFIX:hi:SUFFIX\\"u8.ToArray());
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a configured multiplexer route that has not approved the title family
    /// suppresses the described TS/fsl title program's bytes as well, matching the OSC 2 path.</summary>
    [Fact]
    public async Task SetTitle_WhenTmuxRouteDoesNotApproveTitleFamily_DescribedPathIsSuppressedAndByteQuietAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var profile = CreateProfile(new Dictionary<string, DescriptionProgram>
        {
            ["TS"] = new DescriptionProgram("PREFIX:"u8),
            ["fsl"] = new DescriptionProgram(":SUFFIX"u8)
        });
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            profile,
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Clipboard);
        var options = TerminalOptions.Minimal with { Profile = profile, Multiplexing = policy };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.IsTitleSupported.ShouldBeTrue();
        application.Terminal.SetTitle("hi");
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies non-executable described bell programs are unsupported and byte-quiet.</summary>
    /// <param name="source">The bell program with a broken zero-parameter contract.</param>
    [Theory]
    [InlineData("%p1%d")]
    [InlineData("%{1}%PA")]
    public async Task Bell_WhenProgramCannotProduceZeroParameterOutput_IsUnsupportedAsync(string source)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var profile = CreateProfile(new Dictionary<string, DescriptionProgram>
        {
            ["bel"] = new DescriptionProgram(Encoding.ASCII.GetBytes(source))
        });
        var options = TerminalOptions.Minimal with { Profile = profile };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.Bell.IsSupported.ShouldBeFalse();
        Should.NotThrow(application.Terminal.Bell.Ring);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies invalid described OSC 52 programs cannot publish clipboard support or bytes.</summary>
    /// <param name="source">The outputless or wrong-arity <c>Ms</c> program.</param>
    [Theory]
    [InlineData("%p1%s")]
    [InlineData("%p1%Pa%p2%Pb")]
    public async Task Clipboard_WhenMsContractIsInvalid_IsUnsupportedAndByteQuietAsync(string source)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var claimed = new Feature(CapabilitySupport.Supported, Origin.Database);
        var programs = new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8),
            ["Ms"] = new DescriptionProgram(Encoding.ASCII.GetBytes(source))
        };
        var profile = new TerminalProfile(
            new Description("invalid-ms", DescriptionOrigin.Database, Suitability.Usable),
            TerminalCapabilities.Conservative with { Osc52 = claimed },
            new Programs(programs),
            KeyMap.Empty);
        var options = TerminalOptions.Minimal with { Profile = profile };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.Clipboard.IsSupported.ShouldBeFalse();
        application.Terminal.Clipboard.Write("blocked");
        application.Terminal.Clipboard.Request();
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies a Kitty-only clipboard profile reports support and emits exact Kitty OSC 5522 write
    /// bytes, now that inbound routing and the Kitty transaction lifecycle exist.
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenOnlyKittyIsSupported_EmitsExactKittyBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported }
        };
        List<string> written = [];
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            written.Add(Encoding.ASCII.GetString(memory.Span));
            var joined = string.Concat(written);

            if (joined.Contains("]5522;type=write:id=sv1\\", StringComparison.Ordinal) &&
                joined.Contains("type=wdata:mime=dGV4dC9wbGFpbg==;aGVsbG8=", StringComparison.Ordinal) &&
                joined.Contains("]5522;type=wdata\\", StringComparison.Ordinal))
            {
                _ = complete.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.IsSupported.ShouldBeTrue();
        application.Terminal.Clipboard.Write("hello");
        await complete.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var joined = string.Concat(written);
        joined.ShouldContain("]5522;type=write:id=sv1\\");
        joined.ShouldContain("type=wdata:mime=dGV4dC9wbGFpbg==;aGVsbG8=");
        joined.ShouldContain("]5522;type=wdata\\");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies Kitty is preferred over OSC 52 when both are authoritatively proven, per the
    /// fallback-ladder rule in the safe-degradation contract.
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenBothProtocolsAreAuthoritative_PrefersKittyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported, Osc52 = supported }
        };
        List<string> written = [];
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            written.Add(Encoding.ASCII.GetString(memory.Span));
            var joined = string.Concat(written);

            if (joined.Contains("]5522;type=write:id=sv1\\", StringComparison.Ordinal) &&
                joined.Contains("type=wdata:mime=dGV4dC9wbGFpbg==;aGVsbG8=", StringComparison.Ordinal) &&
                joined.Contains("]5522;type=wdata\\", StringComparison.Ordinal))
            {
                _ = complete.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.IsSupported.ShouldBeTrue();
        application.Terminal.Clipboard.Write("hello");
        await complete.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var joined = string.Concat(written);
        joined.ShouldContain("]5522;");
        joined.ShouldNotContain("]52;");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a completed Kitty write raises the reply event with a null result (no
    /// MIME data on a write acknowledgement) and no failure.</summary>
    [Fact]
    public async Task Clipboard_WhenKittyWriteCompletes_RaisesSuccessfulReplyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Write("hello");
        // The Kitty id is assigned inside the posted callback, so a dispatcher round-trip is
        // needed before the reply below can carry a matching id.
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        terminal.QueueInput("]5522;type=write:status=DONE:id=sv1\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSucceeded.ShouldBeTrue();
        var result = args.KittyResult.ShouldNotBeNull();
        result.Items.ShouldBeEmpty();
        result.Dispose();
        args.Text.ShouldBeNull();
        args.Failure.ShouldBe(KittyClipboardReplyStatus.None);
        args.Diagnostic.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a completed Kitty read transfers the owned MIME result through the reply
    /// event.</summary>
    [Fact]
    public async Task Clipboard_WhenKittyReadCompletes_RaisesReplyWithOwnedResultAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        // The Kitty id is assigned inside the posted callback, so a dispatcher round-trip is
        // needed before the reply below can carry a matching id.
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        terminal.QueueInput("]5522;type=read:status=OK:id=sv1\\"u8);
        terminal.QueueInput(
            "]5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=sv1;aGVsbG8=\\"u8);
        terminal.QueueInput("]5522;type=read:status=DONE:id=sv1\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSucceeded.ShouldBeTrue();
        var item = args.KittyResult.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        Encoding.UTF8.GetString(item.Data.Span).ShouldBe("hello");
        args.KittyResult.Dispose();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the default clipboard deadline leaves enough time for a user to approve
    /// Kitty's required interactive read-permission prompt.</summary>
    [Fact]
    public async Task Clipboard_WhenKittyReadWaitsForPermission_RemainsPendingPastCapabilityQueryTimeoutAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new ManualTimeProvider();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        clock.Advance(QueryLimits.Default.QueryTimeout);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        reply.Task.IsCompleted.ShouldBeFalse();

        terminal.QueueInput("\u001b]5522;type=read:status=OK:id=sv1\u001b\\"u8);
        terminal.QueueInput(
            "\u001b]5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=sv1;aGVsbG8=\u001b\\"u8);
        terminal.QueueInput("\u001b]5522;type=read:status=DONE:id=sv1\u001b\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSucceeded.ShouldBeTrue();
        Encoding.UTF8.GetString(args.KittyResult.ShouldNotBeNull().Items.ShouldHaveSingleItem().Data.Span)
            .ShouldBe("hello");
        args.KittyResult.Dispose();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an id-less Kitty paste notification publishes its owned MIME inventory
    /// and one-time password separately from application-issued clipboard replies.</summary>
    [Fact]
    public async Task Clipboard_WhenPasteEventsAreLeased_RaisesTerminalInitiatedPasteAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported },
            ClipboardPasteEvents = true
        };
        var paste = new TaskCompletionSource<ClipboardPasteEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replies = 0;
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.ClipboardPasteReceived += (_, args) => paste.TrySetResult(args);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, _) => replies++;
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.QueueInput("\u001b]5522;type=read:status=OK:pw=c2VjcmV0\u001b\\"u8);
        terminal.QueueInput(
            "\u001b]5522;type=read:status=DATA:mime=Lg==:pw=c2VjcmV0;dGV4dC9wbGFpbiBpbWFnZS9wbmcK\u001b\\"u8);
        terminal.QueueInput("\u001b]5522;type=read:status=DONE:pw=c2VjcmV0\u001b\\"u8);

        var args = await paste.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.Selection.ShouldBe(ClipboardSelection.Clipboard);
        args.MimeTypes.ShouldBe(["text/plain", "image/png"]);
        Encoding.UTF8.GetString(args.Password.Span).ShouldBe("secret");
        replies.ShouldBe(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies id-less Kitty packets remain ignored when the application did not lease
    /// mode 5522, even if clipboard transfer support itself is authoritative.</summary>
    [Fact]
    public async Task Clipboard_WhenPasteEventsAreNotLeased_IgnoresTerminalInitiatedPasteAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported }
        };
        var received = 0;
        var doneRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.InputRead += bytes =>
        {
            if (bytes.Span.IndexOf("status=DONE"u8) >= 0)
            {
                _ = doneRead.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.ClipboardPasteReceived += (_, _) => received++;
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.QueueInput("\u001b]5522;type=read:status=OK:pw=c2VjcmV0\u001b\\"u8);
        terminal.QueueInput(
            "\u001b]5522;type=read:status=DATA:mime=Lg==:pw=c2VjcmV0;dGV4dC9wbGFpbg==\u001b\\"u8);
        terminal.QueueInput("\u001b]5522;type=read:status=DONE:pw=c2VjcmV0\u001b\\"u8);
        await doneRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        received.ShouldBe(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a paste notification without one consistent non-empty one-time password
    /// is discarded instead of publishing an unauthenticated partial offer.</summary>
    /// <param name="mismatched">Whether the DATA packet changes a present password.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clipboard_WhenPastePasswordIsMissingOrChanges_DoesNotPublishAsync(bool mismatched)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported },
            ClipboardPasteEvents = true
        };
        var received = 0;
        var doneRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.InputRead += bytes =>
        {
            if (bytes.Span.IndexOf("status=DONE"u8) >= 0)
            {
                _ = doneRead.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.ClipboardPasteReceived += (_, _) => received++;
        await application.StartAsync(TestContext.Current.CancellationToken);
        var okPassword = mismatched ? ":pw=c2VjcmV0" : string.Empty;
        var dataPassword = mismatched ? ":pw=b3RoZXI=" : string.Empty;
        var donePassword = mismatched ? ":pw=c2VjcmV0" : string.Empty;

        terminal.QueueInput(Encoding.ASCII.GetBytes($"\u001b]5522;type=read:status=OK{okPassword}\u001b\\"));
        terminal.QueueInput(Encoding.ASCII.GetBytes(
            $"\u001b]5522;type=read:status=DATA:mime=Lg=={dataPassword};dGV4dC9wbGFpbg==\u001b\\"));
        terminal.QueueInput(Encoding.ASCII.GetBytes($"\u001b]5522;type=read:status=DONE{donePassword}\u001b\\"));
        await doneRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        received.ShouldBe(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an early relative timer callback replaces rather than duplicates its
    /// underlying timer, then complete paste delivery releases the replacement.</summary>
    [Fact]
    public async Task Clipboard_WhenPasteTimerFiresBeforeUtcDeadline_ReplacesAndReleasesTimerAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported },
            ClipboardPasteEvents = true
        };
        var clock = new ManualTimeProvider();
        var okRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paste = new TaskCompletionSource<ClipboardPasteEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.InputRead += bytes =>
        {
            if (bytes.Span.IndexOf("status=OK"u8) >= 0)
            {
                _ = okRead.TrySetResult();
            }
        };
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        application.Terminal.Clipboard.ClipboardPasteReceived += (_, args) => paste.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var baselineTimers = clock.ActiveTimerCount;

        terminal.QueueInput("\u001b]5522;type=read:status=OK:pw=c2VjcmV0\u001b\\"u8);
        await okRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 1_000 && clock.ActiveTimerCount == baselineTimers; attempt++)
        {
            await Task.Delay(1, TestContext.Current.CancellationToken);
            await FlushAsync(application);
        }

        clock.ActiveTimerCount.ShouldBe(baselineTimers + 1);

        clock.AdjustUtc(TimeSpan.FromSeconds(-1));
        clock.Advance(options.ClipboardOperationTimeout);
        await FlushAsync(application);
        clock.ActiveTimerCount.ShouldBe(baselineTimers + 1);

        terminal.QueueInput(
            "\u001b]5522;type=read:status=DATA:mime=Lg==:pw=c2VjcmV0;dGV4dC9wbGFpbg==\u001b\\"u8);
        terminal.QueueInput("\u001b]5522;type=read:status=DONE:pw=c2VjcmV0\u001b\\"u8);
        _ = await paste.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await FlushAsync(application);

        clock.ActiveTimerCount.ShouldBe(baselineTimers);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies requesting paste events cannot bypass an explicitly rejected clipboard
    /// route: GNU screen receives neither the lease nor clipboard services, and raw id-less packets
    /// cannot synthesize an event behind that gate.</summary>
    [Fact]
    public async Task Clipboard_WhenPasteRouteIsRejected_DoesNotLeaseOrPublishAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported };
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Screen],
            TerminalProfile.CreateAnsi(capabilities),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Clipboard);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = capabilities,
            ClipboardPasteEvents = true,
            Multiplexing = policy
        };
        var received = 0;
        var doneRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.InputRead += bytes =>
        {
            if (bytes.Span.IndexOf("status=DONE"u8) >= 0)
            {
                _ = doneRead.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.ClipboardPasteReceived += (_, _) => received++;
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.IsSupported.ShouldBeFalse();
        terminal.QueueInput("\u001b]5522;type=read:status=OK:pw=c2VjcmV0\u001b\\"u8);
        terminal.QueueInput(
            "\u001b]5522;type=read:status=DATA:mime=Lg==:pw=c2VjcmV0;dGV4dC9wbGFpbg==\u001b\\"u8);
        terminal.QueueInput("\u001b]5522;type=read:status=DONE:pw=c2VjcmV0\u001b\\"u8);
        await doneRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        received.ShouldBe(0);
        terminal.Writes.SelectMany(static write => write.ToArray())
            .ToArray()
            .AsSpan()
            .IndexOf("\u001b[?5522h"u8)
            .ShouldBe(-1);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies a configured transfer limit tighter than the Kitty transaction's own hardcoded 16
    /// MiB default governs an incoming read reply too, so a DATA packet whose payload sits under the
    /// hardcoded default but above the configured limit is rejected instead of being silently
    /// accepted under the transaction's own default.
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenConfiguredLimitIsBelowTheHardcodedKittyDefault_RejectsReadDataAboveItAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported },
            Input = InputOptions.Default with
            {
                TransferLimits = TransferLimits.Default with { MaxClipboardBytes = 10 }
            }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        // The Kitty id is assigned inside the posted callback, so a dispatcher round-trip is
        // needed before the reply below can carry a matching id.
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        terminal.QueueInput("]5522;type=read:status=OK:id=sv1\\"u8);
        // Two 6-byte DATA chunks: each individually decodes under the 10-byte configured
        // limit (so the packet parser's own single-chunk base64 bound never rejects either one
        // on its own), but their 12-byte cumulative total exceeds it - only the transaction's
        // own running total across chunks can catch this, proving AcceptData enforces the
        // configured limit cumulatively, not just per packet.
        terminal.QueueInput(
            "]5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=sv1;YWFhYWFh\\"u8);
        terminal.QueueInput(
            "]5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=sv1;YmJiYmJi\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSucceeded.ShouldBeFalse();
        args.Failure.ShouldBe(KittyClipboardReplyStatus.None);
        args.Diagnostic.ShouldNotBeNull().Code.ShouldBe(DiagnosticCode.StringLimit);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a terminal permission denial surfaces its exact reply status.</summary>
    [Fact]
    public async Task Clipboard_WhenKittyRequestIsDenied_RaisesReplyWithFailureStatusAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        // The Kitty id is assigned inside the posted callback, so a dispatcher round-trip is
        // needed before the reply below can carry a matching id.
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        terminal.QueueInput("]5522;type=read:status=EPERM:id=sv1\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSucceeded.ShouldBeFalse();
        args.Failure.ShouldBe(KittyClipboardReplyStatus.Denied);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies an unanswered Kitty request raises a failed reply once its deadline elapses,
    /// distinct from success (no result) and from a terminal failure (no diagnostic or status).
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenKittyRequestTimesOut_RaisesFailedReplyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var requestWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += bytes =>
        {
            if (bytes.Span.IndexOf("\u001b]5522;type=read"u8) >= 0)
            {
                _ = requestWritten.TrySetResult();
            }
        };
        var clock = new ManualTimeProvider();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        await requestWritten.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(options.ClipboardOperationTimeout);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSucceeded.ShouldBeFalse();
        args.Failure.ShouldBe(KittyClipboardReplyStatus.None);
        args.Diagnostic.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies an unanswered Kitty request honors the clipboard-specific deadline independently
    /// of the shorter startup capability-query deadline.
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenKittyRequestTimesOutUnderConfiguredClipboardDeadline_RaisesFailedReplyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var configuredTimeout = TimeSpan.FromMilliseconds(100);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported },
            ClipboardOperationTimeout = configuredTimeout,
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                limits: QueryLimits.Default with { QueryTimeout = TimeSpan.FromMilliseconds(25) })
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var requestWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var negotiationQueryWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += bytes =>
        {
            if (bytes.Span.IndexOf("]5522;type=read"u8) >= 0)
            {
                _ = requestWritten.TrySetResult();
            }

            if (bytes.Span.IndexOf("[?u"u8) >= 0)
            {
                _ = negotiationQueryWritten.TrySetResult();
            }
        };
        var clock = new ManualTimeProvider();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);

        // A non-null Negotiation enables active startup probing (TerminalOptions.Negotiation's own
        // remarks), so StartAsync only completes once the terminal answers the capability query it
        // sends - it must be driven forward with a real reply here rather than awaited directly,
        // the same shape CapabilityNegotiationTests and ProtocolRoutingTests use for this exact
        // combination of a non-null Negotiation and a ManualTimeProvider.
        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();
        await negotiationQueryWritten.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        // The transport write can become observable just before the negotiation thread arms its
        // final deadline. Advance and yield within a finite loop so this test synchronizes with the
        // clock-owned state rather than relying on scheduler timing.
        for (var attempt = 0; attempt < 10 && !starting.IsCompleted; attempt++)
        {
            clock.Advance(QueryLimits.Default.QueryTimeout);
            await Task.Yield();
        }
        await starting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        await requestWritten.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        // The clipboard deadline is independent of the 25 ms startup query deadline configured above.
        clock.Advance(configuredTimeout);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSucceeded.ShouldBeFalse();
        args.Failure.ShouldBe(KittyClipboardReplyStatus.None);
        args.Diagnostic.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies a reply carrying a superseded request's correlation ID is silently ignored by the
    /// active transaction, and only the latest request's reply completes it.
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenEarlierRequestIsSuperseded_OnlyLatestReplyCompletesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported }
        };
        List<KittyClipboardReplyEventArgs> replies = [];
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) =>
        {
            replies.Add(args);
            _ = reply.TrySetResult(args);
        };
        await application.StartAsync(TestContext.Current.CancellationToken);

        // The Kitty id is assigned inside the posted callback, so each Request() below must
        // round-trip the dispatcher before the next one is issued, or both could still be waiting
        // to claim an id when the second call runs and end up racing for the same one.
        application.Terminal.Clipboard.Request();
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        application.Terminal.Clipboard.Request();
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // A late reply for the superseded first request (id sv1) must not complete anything. Wait
        // for the transport to actually hand the bytes to the input reader, then round-trip the
        // dispatcher so any (absent) resulting completion has had a chance to run before asserting.
        var sv1Read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.InputRead += bytes =>
        {
            if (bytes.Span.IndexOf("id=sv1"u8) >= 0)
            {
                _ = sv1Read.TrySetResult();
            }
        };
        terminal.QueueInput("]5522;type=read:status=OK:id=sv1\\"u8);
        await sv1Read.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        replies.ShouldBeEmpty();

        // The reply for the still-active second request (id sv2) completes it normally.
        terminal.QueueInput("]5522;type=read:status=EIO:id=sv2\\"u8);
        _ = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var completed = replies.ShouldHaveSingleItem();
        completed.Failure.ShouldBe(KittyClipboardReplyStatus.Io);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a completed OSC 52 request transfers its decoded text through the shared
    /// reply event.</summary>
    [Fact]
    public async Task Clipboard_WhenOsc52RequestCompletes_RaisesReplyWithTextAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        // A manual clock never advances on its own, so the request deadline cannot fire and turn a
        // reply assertion into a timeout outcome on a slow machine.
        var clock = new ManualTimeProvider();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        // The pending OSC 52 request is armed inside the posted callback, so a dispatcher
        // round-trip is needed before the reply below is recognized.
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        terminal.QueueInput("]52;c;aGVsbG8=\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSucceeded.ShouldBeTrue();
        Encoding.UTF8.GetString(args.Text!.Value.Span).ShouldBe("hello");
        args.KittyResult.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies an unanswered OSC 52 request ends in a timeout outcome rather than staying armed
    /// forever.
    ///
    /// <para>The OSC 52 branch had a bool and a Selection field where the Kitty branch has a
    /// request object with a deadline, so nothing ever ended a request the terminal ignored. That
    /// is the <em>ordinary</em> outcome, not an edge case: OSC 52 clipboard reads are denied by
    /// default on stock terminals - xterm's <c>disallowedWindowOps</c> blocks them and kitty's
    /// default <c>clipboard_control</c> omits <c>read-clipboard</c> - so a consumer awaiting the
    /// reply event waited indefinitely, with no exception, no diagnostic, and no Failure.</para>
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenOsc52RequestIsNeverAnswered_RaisesATimeoutReplyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var requestWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += bytes =>
        {
            if (bytes.Span.IndexOf("\u001b]52;c;?"u8) >= 0)
            {
                _ = requestWritten.TrySetResult();
            }
        };
        var clock = new ManualTimeProvider();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        await requestWritten.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // The deadline is armed inside the posted callback, so the write reaching the transport is
        // not by itself proof the timer exists; round-trip the dispatcher before advancing.
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        clock.Advance(options.ClipboardOperationTimeout);

        // The same shape the Kitty TimedOut arm reports: no data, no terminal-reported failure.
        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSucceeded.ShouldBeFalse();
        args.Failure.ShouldBe(KittyClipboardReplyStatus.None);
        args.Diagnostic.ShouldBeNull();
        args.Selection.ShouldBe(ClipboardSelection.Clipboard);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies an OSC 52 reply is labelled from the wire rather than from whatever request happens
    /// to be pending.
    ///
    /// <para>The handler read <c>_pendingOsc52Selection</c> and never consulted
    /// <c>reply.Selection</c>, which is decoded from the wire and is a public property. The
    /// divergence arises in practice when a second Request supersedes the first before the terminal
    /// answers: the earlier selection's data then arrives under the later selection's label. For a
    /// consumer filling a clipboard pane and a primary-selection pane that is silent data
    /// misattribution - the reply arrives, looks successful, and carries the wrong text.</para>
    ///
    /// <para>Driven here as one request answered with a different selection, which is the same
    /// state that reaches the handler without spending the request deadline setting it up.</para>
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenOsc52ReplyCarriesADifferentSelection_LabelsItFromTheWireAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        // A manual clock never advances on its own, so the request deadline cannot fire and turn a
        // reply assertion into a timeout outcome on a slow machine.
        var clock = new ManualTimeProvider();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request(ClipboardSelection.Clipboard);
        // The pending OSC 52 request is armed inside the posted callback, so a dispatcher
        // round-trip is needed before the reply below is recognized.
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        terminal.QueueInput("]52;p;cHJpbWFyeQ==\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSucceeded.ShouldBeTrue();
        Encoding.UTF8.GetString(args.Text!.Value.Span).ShouldBe("primary");
        args.Selection.ShouldBe(
            ClipboardSelection.Primary,
            "the reply carries the selection the terminal answered, not the one that was requested");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The counter-case that keeps the label honest: an ordinary single request still
    /// reports the selection it asked for.</summary>
    [Fact]
    public async Task Clipboard_WhenOsc52PrimaryRequestCompletes_ReportsPrimaryAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        // A manual clock never advances on its own, so the request deadline cannot fire and turn a
        // reply assertion into a timeout outcome on a slow machine.
        var clock = new ManualTimeProvider();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request(ClipboardSelection.Primary);
        // The pending OSC 52 request is armed inside the posted callback, so a dispatcher
        // round-trip is needed before the reply below is recognized.
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        terminal.QueueInput("]52;p;cHJpbWFyeQ==\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.Selection.ShouldBe(ClipboardSelection.Primary);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an answered request disarms its deadline, so a completed request cannot
    /// also report a timeout afterwards.</summary>
    [Fact]
    public async Task Clipboard_WhenOsc52RequestCompletes_DisarmsTheDeadlineAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported }
        };
        List<KittyClipboardReplyEventArgs> replies = [];
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new ManualTimeProvider();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) =>
        {
            replies.Add(args);
            _ = reply.TrySetResult(args);
        };
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        terminal.QueueInput("]52;c;aGVsbG8=\\"u8);
        _ = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(options.ClipboardOperationTimeout * 2);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        replies.Count.ShouldBe(1, "a completed request must not also time out");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies concurrent OSC 52 requests serialize each pending state transition with
    /// its wire query, so the active timeout always names the selection written last.</summary>
    [Fact]
    public async Task Request_WhenOsc52CallersRace_KeepsPendingSelectionAlignedWithLastQueryAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported }
        };
        var clock = new ManualTimeProvider();
        var timeout = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ClipboardSelection? lastQuery = null;
        var queryCount = 0;
        terminal.Written += bytes =>
        {
            var clipboardIndex = bytes.Span.LastIndexOf("\u001b]52;c;?"u8);
            var primaryIndex = bytes.Span.LastIndexOf("\u001b]52;p;?"u8);

            if (clipboardIndex >= 0)
            {
                queryCount++;
                lastQuery = ClipboardSelection.Clipboard;
            }

            if (primaryIndex >= 0)
            {
                queryCount++;

                if (primaryIndex > clipboardIndex)
                {
                    lastQuery = ClipboardSelection.Primary;
                }
            }
        };
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => timeout.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var start = new Barrier(3);

        var clipboard = Task.Run(() =>
        {
            _ = start.SignalAndWait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            application.Terminal.Clipboard.Request(ClipboardSelection.Clipboard);
        }, TestContext.Current.CancellationToken);
        var primary = Task.Run(() =>
        {
            _ = start.SignalAndWait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            application.Terminal.Clipboard.Request(ClipboardSelection.Primary);
        }, TestContext.Current.CancellationToken);
        _ = start.SignalAndWait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Task.WhenAll(clipboard, primary);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        clock.Advance(options.ClipboardOperationTimeout);

        var result = await timeout.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        queryCount.ShouldBe(2);
        result.Selection.ShouldBe(lastQuery!.Value);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a superseded request's stale reply, arriving in receipt order ahead of
    /// the live request's own genuine reply, is dropped silently instead of consuming the live
    /// request's slot. Before the fix, the stale <c>Clipboard</c> reply completed whatever was
    /// currently pending - labelling itself onto the live <c>Primary</c> request, disarming its
    /// deadline, and leaving its own later, genuine reply with nothing pending to complete against,
    /// so it was discarded forever with no event and no timeout.</summary>
    [Fact]
    public async Task Request_WhenStaleReplyArrivesAfterSupersession_StillDeliversTheLiveRequestsOwnReplyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported }
        };
        List<KittyClipboardReplyEventArgs> replies = [];
        var liveReply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new ManualTimeProvider();
        await using Application application = new(new ProbeControl(), terminal, terminal, options, timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) =>
        {
            replies.Add(args);
            _ = liveReply.TrySetResult(args);
        };
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Request Clipboard first, then supersede it with Primary before either is answered.
        application.Terminal.Clipboard.Request(ClipboardSelection.Clipboard);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        application.Terminal.Clipboard.Request(ClipboardSelection.Primary);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // The terminal answers the FIRST (now-superseded) query first - the ordinary outcome, since
        // terminals answer OSC queries in receipt order. It must be dropped silently: no event.
        terminal.QueueInput("]52;c;c3RhbGU=\\"u8); // "c3RhbGU=" == base64("stale")
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        // The terminal's genuine, on-time answer to the LIVE (Primary) request arrives next, and
        // must complete the live request with its own data.
        terminal.QueueInput("]52;p;bGl2ZQ==\\"u8); // "bGl2ZQ==" == base64("live")
        var live = await liveReply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // The live request's own deadline was disarmed by completing normally, so advancing well
        // past the ordinary query timeout must not raise a second, spurious timeout event.
        clock.Advance(options.ClipboardOperationTimeout * 2);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        replies.Count.ShouldBe(1, "the stale reply must be dropped silently, not raise its own event");
        live.Selection.ShouldBe(ClipboardSelection.Primary);
        Encoding.UTF8.GetString(live.Text!.Value.Span).ShouldBe("live");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a request that times out - the ordinary outcome for OSC 52 reads on stock
    /// terminals - does not accrue reply debt: a later, unrelated request must still receive its
    /// own genuine reply rather than have it silently dropped as if it were a stale answer owed by
    /// the timed-out request.</summary>
    [Fact]
    public async Task Request_WhenAPriorRequestTimesOutThenAnotherIsMade_StillDeliversTheNewRequestsReplyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported }
        };
        List<KittyClipboardReplyEventArgs> replies = [];
        var firstReply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new ManualTimeProvider();
        await using Application application = new(new ProbeControl(), terminal, terminal, options, timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) =>
        {
            replies.Add(args);
            _ = (replies.Count == 1 ? firstReply : secondReply).TrySetResult(args);
        };
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request(ClipboardSelection.Clipboard);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        clock.Advance(options.ClipboardOperationTimeout * 2);
        var first = await firstReply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        first.IsSucceeded.ShouldBeFalse("the first request must time out on its own");

        application.Terminal.Clipboard.Request(ClipboardSelection.Primary);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        terminal.QueueInput("]52;p;bGl2ZQ==\\"u8); // "bGl2ZQ==" == base64("live")
        var second = await secondReply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        replies.Count.ShouldBe(2, "a timed-out request must not accrue debt that swallows a later reply");
        second.Selection.ShouldBe(ClipboardSelection.Primary);
        Encoding.UTF8.GetString(second.Text!.Value.Span).ShouldBe("live");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a relative timer callback that arrives before the transaction's UTC
    /// deadline reschedules the remainder instead of abandoning the live request.</summary>
    [Fact]
    public async Task Request_WhenKittyTimerFiresBeforeUtcDeadline_KeepsTransactionAliveAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var clock = new ManualTimeProvider();
        var replies = new List<KittyClipboardReplyEventArgs>();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            KittyOptions(),
            timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => replies.Add(args);
        await application.StartAsync(TestContext.Current.CancellationToken);
        application.Terminal.Clipboard.Request();
        await FlushAsync(application);

        clock.AdjustUtc(TimeSpan.FromSeconds(-1));
        clock.Advance(TerminalOptions.Minimal.ClipboardOperationTimeout);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        replies.ShouldBeEmpty();

        clock.AdjustUtc(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        replies.Count.ShouldBe(1);
        replies[0].IsSucceeded.ShouldBeFalse();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies every clipboard support combination: neither, OSC 52 only, Kitty only, and both.
    /// </summary>
    /// <param name="kittySupported">Whether Kitty evidence is authoritative.</param>
    /// <param name="osc52Supported">Whether OSC 52 evidence is authoritative.</param>
    /// <param name="expected">The expected combined support result.</param>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task IsSupported_ReflectsEveryCapabilityCombinationAsync(
        bool kittySupported,
        bool osc52Supported,
        bool expected)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var unknown = Feature.Unknown;
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with
            {
                KittyClipboard = kittySupported ? supported : unknown,
                Osc52 = osc52Supported ? supported : unknown
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.IsSupported.ShouldBe(expected);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies environment-only OSC 52 evidence never authorizes clipboard output, matching the
    /// authoritative-origin rule every other optional protocol follows.
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenOsc52EvidenceIsEnvironmentOnly_IsUnsupportedAndByteQuietAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var guessed = new Feature(CapabilitySupport.Supported, Origin.Environment);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = guessed }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.Clipboard.IsSupported.ShouldBeFalse();
        application.Terminal.Clipboard.Write("hello");
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies authoritative OSC 52 still writes and requests the exact documented bytes, so the
    /// tightened gate rejected the origin rather than disabling the protocol.
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenOsc52IsAuthoritative_EmitsExactBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported }
        };
        List<string> written = [];
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            written.Add(Encoding.ASCII.GetString(memory.Span));
            var joined = string.Concat(written);

            if (joined.Contains("52;c;aGVsbG8=", StringComparison.Ordinal) &&
                joined.Contains("52;c;?", StringComparison.Ordinal))
            {
                _ = complete.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.IsSupported.ShouldBeTrue();
        application.Terminal.Clipboard.Write("hello");
        application.Terminal.Clipboard.Request();
        await complete.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Out-of-band posts may be coalesced into a single transport write.
        var joined = string.Concat(written);
        joined.ShouldContain("\u001b]52;c;aGVsbG8=\u001b\\");
        joined.ShouldContain("\u001b]52;c;?\u001b\\");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an explicitly authorized tmux clipboard route wraps every OSC 52 string
    /// before the ordered out-of-band write reaches the transport.</summary>
    [Fact]
    public async Task Clipboard_WhenTmuxRouteApprovesFamily_WrapsOsc52BytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TerminalCapabilities.Conservative with { Osc52 = supported };
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(capabilities),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Clipboard);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = capabilities,
            Multiplexing = policy
        };
        var written = new TaskCompletionSource<ReadOnlyMemory<byte>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += bytes =>
        {
            if (bytes.Span.IndexOf("\u001bPtmux;"u8) >= 0)
            {
                _ = written.TrySetResult(bytes.ToArray());
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Write("hi");

        var bytes = await written.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        bytes.ToArray().ShouldBe(
            "\u001bPtmux;\u001b\u001b]52;c;aGk=\u001b\u001b\\\u001b\\"u8.ToArray());
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a Kitty transfer is budgeted and wrapped one OSC packet at a time, so a
    /// multi-packet command may exceed the route bound in aggregate while every independently
    /// forwarded tmux envelope remains within it.</summary>
    [Fact]
    public async Task Clipboard_WhenKittyWriteSpansPackets_RoutesEachPacketWithinEnvelopeBudgetAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported };
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(capabilities),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Clipboard,
            maxEnvelopeBytes: 6_000);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = capabilities,
            Multiplexing = policy
        };
        List<byte[]> writes = [];
        var complete = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += bytes =>
        {
            writes.Add(bytes.ToArray());
            byte[] allBytes = [.. writes.SelectMany(static write => write)];
            var joined = Encoding.UTF8.GetString(allBytes);

            if (joined.Split("\u001bPtmux;", StringSplitOptions.None).Length - 1 >= 4)
            {
                _ = complete.TrySetResult(allBytes);
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Write(new string('a', 5_000));

        var allBytes = await complete.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var envelopes = Encoding.UTF8.GetString(allBytes).Split("\u001bPtmux;", StringSplitOptions.None);
        envelopes.Length.ShouldBeGreaterThanOrEqualTo(5);
        allBytes.Length.ShouldBeGreaterThan(policy.MaxEnvelopeBytes);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the session unwraps a tmux-carried OSC 5522 reply under the clipboard
    /// family even when capability-query routing is not approved.</summary>
    [Fact]
    public async Task Clipboard_WhenTmuxRouteCarriesKittyReply_UnwrapsAndCompletesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported };
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(capabilities),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Clipboard);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = capabilities,
            Multiplexing = policy
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new ManualTimeProvider();
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options,
            timeProvider: clock);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);
        application.Terminal.Clipboard.Request();
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.QueueInput(
            "\u001bPtmux;\u001b\u001b]5522;type=read:status=EIO:id=sv1\u001b\u001b\\\u001b\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.Failure.ShouldBe(KittyClipboardReplyStatus.Io);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies a configured transfer limit wider than OSC 52's own hardcoded 1 MiB default is
    /// honored, so a write whose UTF-8 length sits between the old hardcoded default and the newly
    /// configured limit succeeds instead of throwing.
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenConfiguredLimitExceedsTheHardcodedDefault_WritesTextAboveItAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported },
            Input = InputOptions.Default with
            {
                TransferLimits = TransferLimits.Default with { MaxClipboardBytes = 2 * 1_048_576 }
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        var text = new string('a', 1_500_000);
        Should.NotThrow(() => application.Terminal.Clipboard.Write(text));
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBeGreaterThan(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies a configured transfer limit tighter than OSC 52's own hardcoded 1 MiB default is
    /// honored, proving the configured value governs the OSC 52 fallback path rather than the
    /// hardcoded default that previously applied regardless of configuration.
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenConfiguredLimitIsBelowTheHardcodedDefault_ThrowsForTextAboveItAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported },
            Input = InputOptions.Default with
            {
                TransferLimits = TransferLimits.Default with { MaxClipboardBytes = 100 }
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        var text = new string('a', 200);
        _ = Should.Throw<ArgumentException>(() => application.Terminal.Clipboard.Write(text));

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies a configured transfer limit wider than the Kitty writer's own hardcoded 16 MiB
    /// default is honored on the Kitty clipboard path too, so a write whose UTF-8 length sits
    /// between the hardcoded default and the newly configured limit reaches the terminal instead
    /// of being silently capped at the writer's own default.
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenConfiguredLimitExceedsTheHardcodedKittyDefault_WritesTextAboveItAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        const int configuredLimit = 20 * 1_048_576;
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported },
            Input = InputOptions.Default with
            {
                TransferLimits = TransferLimits.Default with { MaxClipboardBytes = configuredLimit }
            }
        };

        // Above the writer's own hardcoded 16 MiB default, below the configured limit.
        var text = new string('a', 17_000_000);
        var textBytes = Encoding.UTF8.GetBytes(text);

        // The expected bytes come from the same production encoder TerminalServices itself calls,
        // used here as a reference to avoid hand-duplicating its chunking and padding behavior.
        var expectedDestination = new ArrayBufferWriter<byte>(textBytes.Length + 64);
        var expectedWriter = new ProtocolWriter(expectedDestination);
        KittyClipboardWriter.WriteStart(expectedWriter, ClipboardSelection.Clipboard, id: "sv1"u8);
        KittyClipboardWriter.WriteData(expectedWriter, "text/plain"u8, textBytes, configuredLimit);
        KittyClipboardWriter.WriteEnd(expectedWriter);
        var expectedBytes = expectedDestination.WrittenSpan.ToArray();

        List<byte> received = [];
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            received.AddRange(memory.Span);

            if (received.Count >= expectedBytes.Length)
            {
                _ = complete.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.IsSupported.ShouldBeTrue();
        application.Terminal.Clipboard.Write(text);
        await complete.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // The captured stream also carries the initial render frame (cursor positioning, SGR
        // resets) ahead of the clipboard write, so assert the clipboard bytes appear intact as a
        // contiguous run rather than requiring the whole stream to match exactly.
        received.ToArray().AsSpan().IndexOf(expectedBytes).ShouldBeGreaterThanOrEqualTo(0);
        application.Failure.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies a configured transfer limit tighter than the Kitty writer's own hardcoded 16 MiB
    /// default is honored on the Kitty clipboard path too, proving the configured value governs
    /// Kitty writes rather than only the OSC 52 fallback. Unlike the OSC 52 write, the Kitty write
    /// is dispatched via Dispatcher.Post, so the over-limit ArgumentException surfaces through
    /// Application.Failure once the posted work runs, not synchronously from Write itself.
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenConfiguredLimitIsBelowTheHardcodedKittyDefault_FailsForTextAboveItAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported },
            Input = InputOptions.Default with
            {
                TransferLimits = TransferLimits.Default with { MaxClipboardBytes = 100 }
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        var text = new string('a', 200);
        application.Terminal.Clipboard.Write(text);

        // The write is posted rather than run inline, so the thrown ArgumentException surfaces
        // through Application.Failure only once the posted work has actually executed.
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        _ = application.Failure.ShouldNotBeNull().ShouldBeOfType<ArgumentException>();

        // StopAsync rethrows a recorded Failure once cleanup completes, mirroring every other test
        // in this file that leaves Application.Failure set before stopping.
        _ = await Should.ThrowAsync<ArgumentException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies ringing the bell posts the BEL byte through the out-of-band write path.</summary>
    [Fact]
    public async Task Bell_WhenRung_EmitsBelByteAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var bell = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            if (memory.Span.IndexOf((byte) 0x07) >= 0)
            {
                _ = bell.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Bell.Ring();
        await bell.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies notifications are wired through <see cref="INotifications"/> and gated on an
    /// explicit opt-in override: since there is no reliable environment or query signal for OSC 9
    /// / OSC 777 support, an authoritative override is the only way to make it supported, and once
    /// it is, both the body-only and titled overloads emit the exact documented bytes.
    /// </summary>
    [Fact]
    public async Task Notify_WhenOverrideIsAuthoritative_EmitsExactBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Notifications = supported }
        };
        List<string> written = [];
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            written.Add(Encoding.ASCII.GetString(memory.Span));
            var joined = string.Concat(written);

            if (joined.Contains("]9;hello", StringComparison.Ordinal) &&
                joined.Contains("]777;notify;title;hello", StringComparison.Ordinal))
            {
                _ = complete.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Notifications.IsSupported.ShouldBeTrue();
        application.Terminal.Notifications.Notify("hello");
        application.Terminal.Notifications.Notify("title", "hello");
        await complete.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies desktop notifications are unsupported and byte-quiet without any override, matching
    /// the authoritative-origin gate every other optional protocol follows: the conservative
    /// profile's default unknown evidence never authorizes notification output.
    /// </summary>
    [Fact]
    public async Task Notify_WhenNoOverrideIsSet_IsUnsupportedAndByteQuietAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.Notifications.IsSupported.ShouldBeFalse();
        application.Terminal.Notifications.Notify("ignored");
        application.Terminal.Notifications.Notify("ignored", "ignored");
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies an explicit override set to false still leaves notifications unsupported and
    /// byte-quiet, proving the gate checks the override's resulting support state rather than
    /// merely whether an override was supplied.
    /// </summary>
    [Fact]
    public async Task Notify_WhenOverrideIsExplicitlyFalse_IsUnsupportedAndByteQuietAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var unsupported = new Feature(CapabilitySupport.Unsupported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Notifications = unsupported }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.Notifications.IsSupported.ShouldBeFalse();
        application.Terminal.Notifications.Notify("ignored");
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies the titled overload rejects a literal ';' in the title but accepts one in the
    /// body, matching <see cref="Osc.Notify(ProtocolWriter, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>'s
    /// asymmetric field-injection guard: a ';' in the title would shift the OSC 777 title/body
    /// boundary the urxvt/foot receiver assumes, while one in the body is absorbed safely.
    /// </summary>
    [Fact]
    public async Task Notify_WhenTitleContainsSemicolon_ThrowsArgumentExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Notifications = supported }
        };
        List<string> written = [];
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            written.Add(Encoding.ASCII.GetString(memory.Span));

            if (string.Concat(written).Contains("]777;notify;title;bo;dy", StringComparison.Ordinal))
            {
                _ = complete.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Notifications.IsSupported.ShouldBeTrue();
        _ = Should.Throw<ArgumentException>(
            () => application.Terminal.Notifications.Notify("ti;tle", "body"));
        application.Terminal.Notifications.Notify("title", "bo;dy");
        await complete.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an explicitly authorized tmux notification route wraps the OSC 777 string
    /// in DCS passthrough before the ordered out-of-band write reaches the transport, matching the
    /// existing clipboard and graphics routing precedent instead of posting bare bytes that tmux
    /// would otherwise swallow.</summary>
    [Fact]
    public async Task Notify_WhenTmuxRouteApprovesFamily_WrapsNotificationBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TerminalCapabilities.Conservative with { Notifications = supported };
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(capabilities),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Notifications);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = capabilities,
            Multiplexing = policy
        };
        var written = new TaskCompletionSource<ReadOnlyMemory<byte>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += bytes =>
        {
            if (bytes.Span.IndexOf("\u001bPtmux;"u8) >= 0)
            {
                _ = written.TrySetResult(bytes.ToArray());
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Notifications.Notify("hi");

        var bytes = await written.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        bytes.ToArray().ShouldBe(
            "\u001bPtmux;\u001b\u001b]9;hi\u001b\u001b\\\u001b\\"u8.ToArray());
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a configured multiplexer route that has not approved the notification
    /// family suppresses the notification entirely rather than posting an unwrapped OSC that the
    /// multiplexer would otherwise consume and never forward.</summary>
    [Fact]
    public async Task Notify_WhenTmuxRouteDoesNotApproveFamily_IsSuppressedAndByteQuietAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TerminalCapabilities.Conservative with { Notifications = supported };
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(capabilities),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Clipboard);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = capabilities,
            Multiplexing = policy
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.Notifications.IsSupported.ShouldBeTrue();
        application.Terminal.Notifications.Notify("hi");
        application.Terminal.Notifications.Notify("title", "hi");
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies both notify overloads validate their string arguments unconditionally,
    /// even when notifications are unsupported, matching <c>SetTitle</c>'s null-check ordering.</summary>
    [Fact]
    public async Task Notify_WhenArgumentIsNull_ThrowsArgumentNullExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);

        _ = Should.Throw<ArgumentNullException>(() => application.Terminal.Notifications.Notify(null!));
        _ = Should.Throw<ArgumentNullException>(() => application.Terminal.Notifications.Notify(null!, "body"));
        _ = Should.Throw<ArgumentNullException>(() => application.Terminal.Notifications.Notify("title", null!));
    }

    /// <summary>Verifies the terminal services facade and its members are non-null once constructed.</summary>
    [Fact]
    public async Task Terminal_WhenConstructed_IsNonNullAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);

        _ = application.Terminal.ShouldNotBeNull();
        _ = application.Terminal.Bell.ShouldNotBeNull();
        _ = application.Terminal.Clipboard.ShouldNotBeNull();
        _ = application.Terminal.Notifications.ShouldNotBeNull();
    }

    /// <summary>The regression this file exists to pin: stopping with a transaction in flight must
    /// not leave a timer armed. A still-live periodic timer keeps posting after the dispatcher has
    /// stopped, which surfaces here as continued writes or a fault after shutdown.</summary>
    [Fact]
    public async Task StopAsync_WhenAKittyClipboardWriteIsInFlight_LeavesNoArmedTimerAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, KittyOptions());
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Opens a transaction and arms the deadline timer; no reply is ever fed back, so the
        // transaction is still outstanding when the application stops.
        application.Terminal.Clipboard.Write("hello");
        await FlushAsync(application);
        ((TerminalServices) application.Terminal).HasPendingKittyTimeoutForTests
            .ShouldBeTrue("the write must arm a deadline timer to test anything");

        await application.StopAsync(TestContext.Current.CancellationToken);

        // Observed directly rather than through side effects: a leaked timer posts to the stopped
        // dispatcher and swallows the ObjectDisposedException, so it emits nothing an assertion on
        // writes or faults could see. Only the field itself shows whether it is still armed.
        ((TerminalServices) application.Terminal).HasPendingKittyTimeoutForTests
            .ShouldBeFalse("a transaction outstanding at shutdown must not leave a timer armed");
    }

    /// <summary>Verifies the same for a read, which takes the other transaction entry point.</summary>
    [Fact]
    public async Task StopAsync_WhenAKittyClipboardRequestIsInFlight_LeavesNoArmedTimerAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, KittyOptions());
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        await FlushAsync(application);
        ((TerminalServices) application.Terminal).HasPendingKittyTimeoutForTests.ShouldBeTrue();

        await application.StopAsync(TestContext.Current.CancellationToken);

        ((TerminalServices) application.Terminal).HasPendingKittyTimeoutForTests.ShouldBeFalse();
    }

    /// <summary>Verifies disposal without any clipboard traffic is still clean, so the teardown
    /// cannot depend on a transaction existing.</summary>
    [Fact]
    public async Task StopAsync_WhenNoClipboardWorkHappened_StopsCleanlyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, KittyOptions());
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);

        application.Failure.ShouldBeNull();
    }

    /// <summary>Verifies a completed transaction still tears down cleanly, so the new disposal does
    /// not double-dispose what the completion path already released.</summary>
    [Fact]
    public async Task StopAsync_WhenAWriteWasSupersededBeforeShutdown_StopsCleanlyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, KittyOptions());
        await application.StartAsync(TestContext.Current.CancellationToken);

        // The second write supersedes the first, which disposes the first transaction and its
        // timer through the cancellation path rather than the completion one.
        application.Terminal.Clipboard.Write("first");
        application.Terminal.Clipboard.Write("second");
        await application.StopAsync(TestContext.Current.CancellationToken);

        application.Failure.ShouldBeNull();
    }

    /// <summary>Verifies an off-thread Kitty write propagates a full-queue failure instead of
    /// swallowing it. Write's Kitty branch posts <c>PerformKittyWrite</c> to the dispatcher from
    /// whatever thread calls <see cref="IClipboard.Write"/> under a guard that now only catches
    /// <see cref="ObjectDisposedException"/>, so <see cref="Dispatcher.Post(Action)"/>'s
    /// <see cref="InvalidOperationException"/> for a saturated queue propagates to the caller
    /// instead of being silently dropped.</summary>
    [Fact]
    public async Task Write_WhenKittyDispatcherQueueIsSaturated_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, KittyOptions());
        var replyReceived = false;
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, _) => replyReceived = true;
        await application.StartAsync(TestContext.Current.CancellationToken);
        var release = await SaturateDispatcherAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => application.Terminal.Clipboard.Write("x"));
        }
        finally
        {
            await DrainDispatcherAsync(application.Dispatcher, release, TestContext.Current.CancellationToken);
        }

        application.Failure.ShouldBeNull();
        replyReceived.ShouldBeFalse("a post dropped by a saturated queue must never reach PerformKittyWrite");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an off-thread Kitty read propagates a full-queue failure instead of
    /// swallowing it, matching Write's guard for the same reason.</summary>
    [Fact]
    public async Task Request_WhenKittyDispatcherQueueIsSaturated_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(new ProbeControl(), terminal, terminal, KittyOptions());
        var replyReceived = false;
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, _) => replyReceived = true;
        await application.StartAsync(TestContext.Current.CancellationToken);
        var release = await SaturateDispatcherAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => application.Terminal.Clipboard.Request());
        }
        finally
        {
            await DrainDispatcherAsync(application.Dispatcher, release, TestContext.Current.CancellationToken);
        }

        application.Failure.ShouldBeNull();
        replyReceived.ShouldBeFalse("a post dropped by a saturated queue must never reach PerformKittyRead");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an off-thread OSC 52 request propagates a full-queue failure instead of
    /// swallowing it. Request's OSC 52 branch posts <c>StartOsc52Request</c> to the dispatcher
    /// under its own local guard, which now only catches <see cref="ObjectDisposedException"/>,
    /// then unconditionally calls the equally narrowed <see cref="Application.PostOutOfBand"/>;
    /// either post can surface the saturated-queue failure here, since both now let
    /// <see cref="InvalidOperationException"/> propagate instead of masking it.</summary>
    [Fact]
    public async Task Request_WhenOsc52DispatcherQueueIsSaturated_PropagatesInvalidOperationExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { Osc52 = supported }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        var replyReceived = false;
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, _) => replyReceived = true;
        await application.StartAsync(TestContext.Current.CancellationToken);
        var release = await SaturateDispatcherAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        try
        {
            _ = Should.Throw<InvalidOperationException>(() => application.Terminal.Clipboard.Request());
        }
        finally
        {
            await DrainDispatcherAsync(application.Dispatcher, release, TestContext.Current.CancellationToken);
        }

        application.Failure.ShouldBeNull();
        replyReceived.ShouldBeFalse("a post dropped by a saturated queue must never arm a pending OSC 52 request");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Blocks <paramref name="dispatcher"/> on one in-flight callback and adaptively fills
    /// the rest of its queue until a further post is rejected. A mounted, started
    /// <see cref="Application"/> - required here for Kitty/OSC 52 support and
    /// <c>IClipboard.IsSupported</c> to report true at all - has already generated real background
    /// dispatcher traffic (negotiation, timers), so unlike a never-started application's provably
    /// empty queue, a fixed-iteration fill is not safe: it could under-fill if background traffic
    /// already consumed some capacity, leaving the queue not actually full. Filling adaptively until
    /// <see cref="Dispatcher.Post(Action)"/> itself reports <see cref="InvalidOperationException"/>
    /// is the only way to guarantee the queue is genuinely saturated regardless of that traffic.</summary>
    private static async Task<ManualResetEventSlim> SaturateDispatcherAsync(
        Dispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new ManualResetEventSlim();
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(cancellationToken);

        while (true)
        {
            try
            {
                dispatcher.Post(static () => { });
            }
            catch (InvalidOperationException)
            {
                break;
            }
        }

        return release;
    }

    /// <summary>Releases a handle returned by <see cref="SaturateDispatcherAsync"/> and waits for the
    /// dispatcher to drain the filler backlog before the caller proceeds to stop or dispose the
    /// application. A single awaited post succeeding is not by itself proof the whole backlog has
    /// drained under parallel-suite load - the queue can still be full at the moment a sentinel post
    /// is attempted, so this retries on <see cref="InvalidOperationException"/> with a short delay
    /// between attempts, up to an overall deadline, rather than trusting one attempt.</summary>
    private static async Task DrainDispatcherAsync(
        Dispatcher dispatcher,
        ManualResetEventSlim release,
        CancellationToken cancellationToken)
    {
        release.Set();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            try
            {
                await dispatcher.InvokeAsync(static () => { }, cancellationToken);

                return;
            }
            catch (InvalidOperationException)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
            }
        }
    }

    // Write and Request queue their transaction onto the dispatcher, so the timer is not armed on
    // the calling thread; a round-trip is required before the field means anything.
    private static async Task FlushAsync(Application application) =>
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

    private static TerminalOptions KittyOptions()
    {
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        return TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { KittyClipboard = supported }
        };
    }

    private static TerminalProfile CreateProfile(
        IReadOnlyDictionary<string, DescriptionProgram> additionalPrograms)
    {
        var programs = new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8),
            ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
            ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
        };

        foreach (var pair in additionalPrograms)
        {
            programs.Add(pair.Key, pair.Value);
        }

        return new TerminalProfile(
            new Description("service-test", DescriptionOrigin.Explicit, Suitability.Usable),
            TerminalCapabilities.Conservative,
            new Programs(programs),
            KeyMap.Empty);
    }
}
