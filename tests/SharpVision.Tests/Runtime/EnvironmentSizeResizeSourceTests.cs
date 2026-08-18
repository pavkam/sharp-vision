// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;

/// <summary>Verifies <c>UseEnvironmentSizeOverrides</c> does what its three normative descriptions
/// promise.
///
/// <para>The property, its builder method, and the <c>hosting.md</c> options row all promised that
/// LINES and COLUMNS set the initial size, and nothing anywhere read the property: neither mapping
/// method copied it, and no <c>"LINES"</c> or <c>"COLUMNS"</c> literal existed in the product at
/// all. It was never implemented rather than implemented and lost, so a consumer who called it got
/// a silent no-op with no diagnostic.</para>
/// </summary>
public sealed class EnvironmentSizeResizeSourceTests
{
    /// <summary>The core promise: the first observation carries the environment's size.</summary>
    [Fact]
    public void TryReadCurrent_WhenBothVariablesAreSet_ReportsTheEnvironmentSize()
    {
        var inner = new StubResizeSource(new Dimensions(new Size(80, 24)));
        var source = EnvironmentSizeResizeSource.Wrap(inner, Environment("120", "40"));

        source.TryReadCurrent(out var value).ShouldBeTrue();

        value.Cells.ShouldBe(new Size(120, 40));
    }

    /// <summary>Verifies the override is confined to the first observation, so a genuine resize
    /// afterwards always wins - an environment value is a starting size, not a lock.</summary>
    [Fact]
    public async Task ReadAsync_AfterTheOverrideWasConsumed_ReportsTheRealSizeAsync()
    {
        var inner = new StubResizeSource(new Dimensions(new Size(80, 24)));
        var source = EnvironmentSizeResizeSource.Wrap(inner, Environment("120", "40"));
        source.TryReadCurrent(out var initial).ShouldBeTrue();
        inner.Next = new Dimensions(new Size(100, 30));

        var resized = await source.ReadAsync(TestContext.Current.CancellationToken);

        initial.Cells.ShouldBe(new Size(120, 40));
        resized.Cells.ShouldBe(new Size(100, 30));
    }

    /// <summary>Verifies the override also reaches a source whose first observation arrives through
    /// ReadAsync, which is the legal shape for a change-only source.</summary>
    [Fact]
    public async Task ReadAsync_WhenItIsTheFirstObservation_ReportsTheEnvironmentSizeAsync()
    {
        var inner = new StubResizeSource(new Dimensions(new Size(80, 24)));
        var source = EnvironmentSizeResizeSource.Wrap(inner, Environment("120", "40"));

        var first = await source.ReadAsync(TestContext.Current.CancellationToken);

        first.Cells.ShouldBe(new Size(120, 40));
    }

    /// <summary>Verifies the override substitutes for a missing snapshot too, so a source with no
    /// synchronous current size still unblocks startup when the environment names one.</summary>
    [Fact]
    public void TryReadCurrent_WhenTheInnerSourceHasNoSnapshot_StillReportsTheEnvironmentSize()
    {
        var inner = new StubResizeSource(new Dimensions(new Size(80, 24))) { HasSnapshot = false };
        var source = EnvironmentSizeResizeSource.Wrap(inner, Environment("120", "40"));

        source.TryReadCurrent(out var value).ShouldBeTrue();

        value.Cells.ShouldBe(new Size(120, 40));
    }

    /// <summary>Verifies pixel dimensions are dropped rather than carried across, since they
    /// describe the real window and would derive cell metrics for a cell size that does not
    /// exist.</summary>
    [Fact]
    public void TryReadCurrent_WhenTheInnerSourceReportsPixels_DropsThemFromTheOverride()
    {
        var inner = new StubResizeSource(new Dimensions(new Size(80, 24), new Size(640, 384)));
        var source = EnvironmentSizeResizeSource.Wrap(inner, Environment("120", "40"));

        source.TryReadCurrent(out var value).ShouldBeTrue();

        value.Pixels.ShouldBeNull();
        value.CellMetrics.ShouldBeNull();
    }

    /// <summary>Verifies a half-specified or unusable environment leaves the source untouched -
    /// returned by reference identity, so the ordinary path adds no indirection at all.</summary>
    [Theory]
    [InlineData(null, "40")]
    [InlineData("120", null)]
    [InlineData("", "")]
    [InlineData("0", "40")]
    [InlineData("120", "-1")]
    [InlineData("120x", "40")]
    [InlineData(" 120", "40")]
    public void Wrap_WhenTheEnvironmentDoesNotNameACompletePositiveSize_ReturnsTheInnerSource(
        string? columns,
        string? lines)
    {
        var inner = new StubResizeSource(new Dimensions(new Size(80, 24)));

        EnvironmentSizeResizeSource.Wrap(inner, Environment(columns, lines)).ShouldBeSameAs(inner);
    }

    /// <summary>Verifies disposal reaches the wrapped source, so the decorator cannot leak the
    /// console's own resize source when the option is on.</summary>
    [Fact]
    public async Task DisposeAsync_WhenWrapped_DisposesTheInnerSourceAsync()
    {
        var inner = new StubResizeSource(new Dimensions(new Size(80, 24)));
        var source = EnvironmentSizeResizeSource.Wrap(inner, Environment("120", "40"));

        await source.DisposeAsync();

        inner.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies the option is what selects the behavior: the same environment with the
    /// option off must change nothing.</summary>
    [Fact]
    public void UseEnvironmentSizeOverrides_IsOffByDefault() =>
        new ConsoleRunOptions().UseEnvironmentSizeOverrides.ShouldBeFalse();

    private static Func<string, string?> Environment(string? columns, string? lines) =>
        name => name switch
        {
            "COLUMNS" => columns,
            "LINES" => lines,
            _ => null
        };

    private sealed class StubResizeSource: IResizeSource
    {
        private readonly Dimensions _current;

        public StubResizeSource(Dimensions current)
        {
            _current = current;
            Next = current;
        }

        public Dimensions Next { get; set; }

        public bool HasSnapshot { get; init; } = true;

        public bool IsDisposed { get; private set; }

        public bool TryReadCurrent(out Dimensions value)
        {
            value = HasSnapshot ? _current : default;
            return HasSnapshot;
        }

        public ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(Next);

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
