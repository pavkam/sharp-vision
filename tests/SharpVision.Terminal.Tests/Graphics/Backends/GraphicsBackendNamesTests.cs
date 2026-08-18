// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Backends;

/// <summary>Proves renderer-owned graphics backends have unambiguous terminal-layer names.</summary>
public sealed class GraphicsBackendNamesTests
{
    /// <summary>Verifies every renderer graphics backend type is internal, top-level, and correctly shaped.</summary>
    [Fact]
    public void GraphicsBackendTypes_WhenLoadedFromTerminalAssembly_AreInternalAndHaveExpectedShapes()
    {
        var assembly = typeof(Renderer).Assembly;
        var names = new[]
        {
            "SharpVision.Terminal.Graphics.Backends.IGraphicsBackend",
            "SharpVision.Terminal.Graphics.Backends.GraphicsBackendResult",
            "SharpVision.Terminal.Graphics.Backends.GraphicsBackendSelector",
            "SharpVision.Terminal.Graphics.Backends.NonRetainedGraphicsBackend",
            "SharpVision.Terminal.Graphics.Backends.KittyGraphicsBackend",
        };
        var types = names
            .Select(assembly.GetType)
            .Select(type => type.ShouldNotBeNull())
            .ToArray();

        foreach (var type in types)
        {
            type.DeclaringType.ShouldBeNull();
            type.IsNotPublic.ShouldBeTrue();
        }

        types[0].IsInterface.ShouldBeTrue();
        types[1].IsValueType.ShouldBeTrue();
        types[1].IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), inherit: false)
            .ShouldBeTrue();
        types[2].IsAbstract.ShouldBeTrue();
        types[2].IsSealed.ShouldBeTrue();

        for (var index = 3; index < types.Length; index++)
        {
            types[index].IsClass.ShouldBeTrue();
            types[index].IsSealed.ShouldBeTrue();
            types[0].IsAssignableFrom(types[index]).ShouldBeTrue();
        }
    }

    /// <summary>Verifies previous ambiguous backend names are absent from terminal assembly metadata.</summary>
    [Fact]
    public void LegacyGraphicsBackendTypes_WhenLoadedFromTerminalAssembly_AreAbsent()
    {
        var assembly = typeof(Renderer).Assembly;
        var names = new[]
        {
            "SharpVision.Terminal.Abstractions.I" + "Backend",
            "SharpVision.Terminal.Graphics.Backend" + "Result",
            "SharpVision.Terminal.Graphics.Backend" + "Selector",
            "SharpVision.Terminal.Graphics.NonRetained" + "Backend",
            "SharpVision.Terminal.Kitty.Backend",
            "SharpVision.Terminal.Iterm.Backend",
            "SharpVision.Terminal.Sixel.Backend",
            "SharpVision.Terminal.Graphics.Backends.ItermGraphicsBackend",
            "SharpVision.Terminal.Graphics.Backends.SixelGraphicsBackend",
        };

        foreach (var name in names)
        {
            assembly.GetType(name).ShouldBeNull();
        }
    }
}
