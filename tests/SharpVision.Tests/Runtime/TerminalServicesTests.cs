// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using Terminal.Kitty.Clipboard;

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
        await Task.Delay(50, TestContext.Current.CancellationToken);

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
        await Task.Delay(50, TestContext.Current.CancellationToken);

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
        await Task.Delay(50, TestContext.Current.CancellationToken);

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
        await Task.Delay(50, TestContext.Current.CancellationToken);

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
        var claimed = new Feature(Terminal.Capabilities.Support.Supported, Origin.Database);
        var programs = new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8),
            ["Ms"] = new DescriptionProgram(Encoding.ASCII.GetBytes(source))
        };
        var profile = new TerminalProfile(
            new Description("invalid-ms", DescriptionOrigin.Database, Suitability.Usable),
            Capabilities.Conservative with { Osc52 = claimed },
            new Programs(programs),
            KeyMap.Empty);
        var options = TerminalOptions.Minimal with { Profile = profile };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.Clipboard.IsSupported.ShouldBeFalse();
        application.Terminal.Clipboard.Write("blocked");
        application.Terminal.Clipboard.Request();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        terminal.Writes.Count.ShouldBe(before);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies a Kitty-only clipboard profile reports support and emits exact Kitty OSC 5522 write
    /// bytes, now that inbound routing and the Kitty transaction lifecycle exist (see #103).
    /// </summary>
    [Fact]
    public async Task Clipboard_WhenOnlyKittyIsSupported_EmitsExactKittyBytesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(Terminal.Capabilities.Support.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = Capabilities.Conservative with { KittyClipboard = supported }
        };
        List<string> written = [];
        terminal.Written += memory => written.Add(Encoding.ASCII.GetString(memory.Span));
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.IsSupported.ShouldBeTrue();
        application.Terminal.Clipboard.Write("hello");
        await Task.Delay(80, TestContext.Current.CancellationToken);

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
        var supported = new Feature(Terminal.Capabilities.Support.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = Capabilities.Conservative with { KittyClipboard = supported, Osc52 = supported }
        };
        List<string> written = [];
        terminal.Written += memory => written.Add(Encoding.ASCII.GetString(memory.Span));
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.IsSupported.ShouldBeTrue();
        application.Terminal.Clipboard.Write("hello");
        await Task.Delay(80, TestContext.Current.CancellationToken);

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
        var supported = new Feature(Terminal.Capabilities.Support.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = Capabilities.Conservative with { KittyClipboard = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Write("hello");
        await Task.Delay(30, TestContext.Current.CancellationToken);
        terminal.QueueInput("]5522;type=write:status=DONE:id=sv1\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSuccess.ShouldBeTrue();
        var result = args.KittyResult.ShouldNotBeNull();
        result.Items.ShouldBeEmpty();
        result.Dispose();
        args.Text.ShouldBeNull();
        args.Failure.ShouldBe(ReplyStatus.None);
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
        var supported = new Feature(Terminal.Capabilities.Support.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = Capabilities.Conservative with { KittyClipboard = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        await Task.Delay(30, TestContext.Current.CancellationToken);
        terminal.QueueInput("]5522;type=read:status=OK:id=sv1\\"u8);
        terminal.QueueInput(
            "]5522;type=read:status=DATA:mime=dGV4dC9wbGFpbg==:id=sv1;aGVsbG8=\\"u8);
        terminal.QueueInput("]5522;type=read:status=DONE:id=sv1\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSuccess.ShouldBeTrue();
        var item = args.KittyResult.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        Encoding.UTF8.GetString(item.Data.Span).ShouldBe("hello");
        args.KittyResult.Dispose();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a terminal permission denial surfaces its exact reply status.</summary>
    [Fact]
    public async Task Clipboard_WhenKittyRequestIsDenied_RaisesReplyWithFailureStatusAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(Terminal.Capabilities.Support.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = Capabilities.Conservative with { KittyClipboard = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        await Task.Delay(30, TestContext.Current.CancellationToken);
        terminal.QueueInput("]5522;type=read:status=EPERM:id=sv1\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSuccess.ShouldBeFalse();
        args.Failure.ShouldBe(ReplyStatus.Denied);
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
        var supported = new Feature(Terminal.Capabilities.Support.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = Capabilities.Conservative with { KittyClipboard = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();

        // The deadline itself is short (QueryLimits.Default.QueryTimeout, 750ms); the generous
        // outer wait only absorbs scheduling jitter under a heavily parallel test run.
        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        args.IsSuccess.ShouldBeFalse();
        args.Failure.ShouldBe(ReplyStatus.None);
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
        var supported = new Feature(Terminal.Capabilities.Support.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = Capabilities.Conservative with { KittyClipboard = supported }
        };
        List<KittyClipboardReplyEventArgs> replies = [];
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => replies.Add(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        await Task.Delay(30, TestContext.Current.CancellationToken);
        application.Terminal.Clipboard.Request();
        await Task.Delay(30, TestContext.Current.CancellationToken);

        // A late reply for the superseded first request (id sv1) must not complete anything.
        terminal.QueueInput("]5522;type=read:status=OK:id=sv1\\"u8);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        replies.ShouldBeEmpty();

        // The reply for the still-active second request (id sv2) completes it normally.
        terminal.QueueInput("]5522;type=read:status=EIO:id=sv2\\"u8);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        var completed = replies.ShouldHaveSingleItem();
        completed.Failure.ShouldBe(ReplyStatus.Io);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a completed OSC 52 request transfers its decoded text through the shared
    /// reply event.</summary>
    [Fact]
    public async Task Clipboard_WhenOsc52RequestCompletes_RaisesReplyWithTextAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var supported = new Feature(Terminal.Capabilities.Support.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = Capabilities.Conservative with { Osc52 = supported }
        };
        var reply = new TaskCompletionSource<KittyClipboardReplyEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        application.Terminal.Clipboard.KittyClipboardReplyReceived += (_, args) => reply.TrySetResult(args);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.Request();
        await Task.Delay(30, TestContext.Current.CancellationToken);
        terminal.QueueInput("]52;c;aGVsbG8=\\"u8);

        var args = await reply.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        args.IsSuccess.ShouldBeTrue();
        Encoding.UTF8.GetString(args.Text!.Value.Span).ShouldBe("hello");
        args.KittyResult.ShouldBeNull();
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
        var supported = new Feature(Terminal.Capabilities.Support.Supported, Origin.Override);
        var unknown = Feature.Unknown;
        var options = TerminalOptions.Minimal with
        {
            Capabilities = Capabilities.Conservative with
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
        var guessed = new Feature(Terminal.Capabilities.Support.Supported, Origin.Environment);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = Capabilities.Conservative with { Osc52 = guessed }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var before = terminal.Writes.Count;

        application.Terminal.Clipboard.IsSupported.ShouldBeFalse();
        application.Terminal.Clipboard.Write("hello");
        await Task.Delay(50, TestContext.Current.CancellationToken);

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
        var supported = new Feature(Terminal.Capabilities.Support.Supported, Origin.Override);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = Capabilities.Conservative with { Osc52 = supported }
        };
        List<string> written = [];
        terminal.Written += memory => written.Add(Encoding.ASCII.GetString(memory.Span));
        await using Application application = new(new ProbeControl(), terminal, terminal, options);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Terminal.Clipboard.IsSupported.ShouldBeTrue();
        application.Terminal.Clipboard.Write("hello");
        application.Terminal.Clipboard.Request();
        await Task.Delay(80, TestContext.Current.CancellationToken);

        // Out-of-band posts may be coalesced into a single transport write.
        var joined = string.Concat(written);
        joined.ShouldContain("\u001b]52;c;aGVsbG8=\u001b\\");
        joined.ShouldContain("\u001b]52;c;?\u001b\\");
        await application.StopAsync(TestContext.Current.CancellationToken);
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

    /// <summary>Verifies the terminal services facade and its members are non-null once constructed.</summary>
    [Fact]
    public async Task Terminal_WhenConstructed_IsNonNullAsync()
    {
        await using FakeTerminal terminal = new();
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);

        _ = application.Terminal.ShouldNotBeNull();
        _ = application.Terminal.Bell.ShouldNotBeNull();
        _ = application.Terminal.Clipboard.ShouldNotBeNull();
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
            Capabilities.Conservative,
            new Programs(programs),
            KeyMap.Empty);
    }
}
