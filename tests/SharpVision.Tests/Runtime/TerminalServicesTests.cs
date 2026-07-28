// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using DescriptionProgram = Terminal.Terminfo.Program;

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
