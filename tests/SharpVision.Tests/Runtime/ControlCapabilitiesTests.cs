// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies immutable terminal capabilities inherited by retained controls.</summary>
public sealed class ControlCapabilitiesTests
{
    /// <summary>Verifies attachment publishes the supplied profile before the attached callback.</summary>
    [Fact]
    public async Task Attach_WhenProfileIsSupplied_InheritsColorDepthAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var profile = Capabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 };
            var control = new CapabilityProbe();

            control.Attach(dispatcher, Policy.Default, profile);

            control.ColorDepth.ShouldBe(ColorDepth.Indexed256);
            control.Transitions.ShouldBe([ColorDepth.Indexed256]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies runtime publication reaches an attached subtree in ownership order.</summary>
    [Fact]
    public async Task SetCapabilities_WhenTreeIsAttached_PublishesToDescendantsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var child = new CapabilityProbe();
            var root = new Stack { Children = { child } };
            root.Attach(
                dispatcher,
                Policy.Default,
                Capabilities.Conservative with { ColorDepth = ColorDepth.Basic16 });
            var profile = Capabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };

            root.SetCapabilities(profile);

            child.ColorDepth.ShouldBe(ColorDepth.TrueColor);
            child.Transitions.ShouldBe([ColorDepth.TrueColor]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a child inserted after attachment inherits the owner's active profile.</summary>
    [Fact]
    public async Task ChildrenAdd_WhenOwnerIsAttached_InheritsCapabilitiesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new Stack();
            root.Attach(
                dispatcher,
                Policy.Default,
                Capabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });
            var child = new CapabilityProbe();

            root.Children.Add(child);

            child.ColorDepth.ShouldBe(ColorDepth.Indexed256);
            child.Transitions.ShouldBe([ColorDepth.Indexed256]);
        }, TestContext.Current.CancellationToken);
    }
}
