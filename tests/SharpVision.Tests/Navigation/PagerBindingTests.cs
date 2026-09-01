// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

using SharpVision.DataBinding;
using SharpVision.Tests.DataBinding.Support;

/// <summary>Verifies Pager's target-owned two-way PageIndex binding.</summary>
public sealed class PagerBindingTests
{
    /// <summary>Verifies initial, model-to-control, and control-to-model synchronization.</summary>
    [Fact]
    public void Bind_WhenEitherSideChanges_SynchronizesPageIndexTwoWay()
    {
        var model = new BindingModel { Number = 2 };
        var pager = new Pager { PageCount = 5 };
        using var binding = pager.Bind(model, source => source.Number);

        pager.PageIndex.ShouldBe(2);

        model.Number = 3;
        pager.PageIndex.ShouldBe(3);

        pager.PageIndex = 4;
        model.Number.ShouldBe(4);
    }

    /// <summary>Verifies an invalid model value is rejected without clamping it back into the model.</summary>
    [Fact]
    public void Bind_WhenSourceIndexIsOutsidePageCount_ThrowsWithoutReverseWrite()
    {
        var model = new BindingModel { Number = 5 };
        var pager = new Pager { PageCount = 3 };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => pager.Bind(model, source => source.Number));

        model.Number.ShouldBe(5);
        pager.PageIndex.ShouldBe(0);
    }

    /// <summary>Verifies the empty natural value binds before PageCount and the later count repair
    /// flows back to the model through the same two-way lifetime.</summary>
    [Fact]
    public void Bind_WhenEmptyRangeBecomesNonempty_WritesEstablishedPageBackToSource()
    {
        var model = new BindingModel { Number = -1 };
        var pager = new Pager();
        using var binding = pager.Bind(model, source => source.Number);

        pager.PageCount = 4;

        pager.PageIndex.ShouldBe(0);
        model.Number.ShouldBe(0);
    }

    /// <summary>Verifies a later invalid source notification leaves Pager state untouched and does
    /// not recursively overwrite the model's rejected value.</summary>
    [Fact]
    public void Bind_WhenLiveSourceIndexBecomesInvalid_ThrowsWithoutReverseWrite()
    {
        var model = new BindingModel { Number = 1 };
        var pager = new Pager { PageCount = 3 };
        using var binding = pager.Bind(model, source => source.Number);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => model.Number = 8);

        pager.PageIndex.ShouldBe(1);
        model.Number.ShouldBe(8);
    }

    /// <summary>Verifies explicit binding disposal removes both synchronization directions.</summary>
    [Fact]
    public void Dispose_WhenPagerBindingIsDisposed_StopsBothDirections()
    {
        var model = new BindingModel { Number = 1 };
        var pager = new Pager { PageCount = 5 };
        var binding = pager.Bind(model, source => source.Number);

        binding.Dispose();
        model.Number = 2;
        pager.PageIndex = 3;

        binding.IsDisposed.ShouldBeTrue();
        pager.PageIndex.ShouldBe(3);
        model.Number.ShouldBe(2);
    }

    /// <summary>Verifies target disposal owns the Pager binding and detaches its source callback.</summary>
    [Fact]
    public void Dispose_WhenPagerIsDisposed_DisposesBindingAndStopsSourceUpdates()
    {
        var model = new BindingModel { Number = 1 };
        var pager = new Pager { PageCount = 5 };
        var binding = pager.Bind(model, source => source.Number);

        pager.Dispose();
        model.Number = 2;

        binding.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies replacing a disposed binding with a different source leaves later stale
    /// notifications from the first source inert.</summary>
    [Fact]
    public void Bind_WhenSourceIsReplaced_IgnoresOldSourceNotifications()
    {
        var previous = new BindingModel { Number = 1 };
        var current = new BindingModel { Number = 2 };
        var pager = new Pager { PageCount = 5 };
        var previousBinding = pager.Bind(previous, source => source.Number);
        previousBinding.Dispose();
        using var currentBinding = pager.Bind(current, source => source.Number);

        previous.Number = 4;

        pager.PageIndex.ShouldBe(2);
    }

    /// <summary>Verifies a worker burst coalesces to the latest valid page on the owning dispatcher.</summary>
    [Fact]
    public async Task Bind_WhenWorkerSourceChangesCoalesce_UsesNewestValidPageIndexAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var model = new BindingModel { Number = 0 };
        var pager = new Pager();
        Binding? binding = null;
        await dispatcher.InvokeAsync(() =>
        {
            pager.PageCount = 5;
            pager.Attach(dispatcher);
            binding = pager.Bind(model, source => source.Number);
        }, TestContext.Current.CancellationToken);
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
        });
        entered.Wait(TestContext.Current.CancellationToken);

        try
        {
            await Task.Run(() =>
            {
                for (var index = 1; index <= 10_004; index++)
                {
                    model.Number = index % 5;
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            release.Set();
        }

        await dispatcher.InvokeAsync(() =>
        {
            pager.PageIndex.ShouldBe(4);
            binding!.Dispose();
            pager.Dispose();
        }, TestContext.Current.CancellationToken);
    }
}
