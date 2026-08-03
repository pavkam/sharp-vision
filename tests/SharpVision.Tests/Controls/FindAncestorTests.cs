// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the protected <see cref="ControlBase.FindAncestor{T}"/> helper.</summary>
public sealed class FindAncestorTests
{
    /// <summary>Verifies a direct parent is returned when it matches the requested type.</summary>
    [Fact]
    public void FindAncestor_WhenParentMatchesType_ReturnsParent()
    {
        var probe = new AncestorProbe();
        using var container = new ProbeContainer();
        container.Children.Add(probe);

        probe.ExposedFindAncestor<ProbeContainer>().ShouldBeSameAs(container);
    }

    /// <summary>Verifies a grandparent is returned when the direct parent does not match.</summary>
    [Fact]
    public void FindAncestor_WhenGrandparentMatchesType_ReturnsGrandparent()
    {
        var probe = new AncestorProbe();
        using var inner = new ProbeContainer();
        using var outer = new Stack();
        inner.Children.Add(probe);
        outer.Children.Add(inner);

        probe.ExposedFindAncestor<Stack>().ShouldBeSameAs(outer);
    }

    /// <summary>Verifies null is returned when no ancestor matches the requested type.</summary>
    [Fact]
    public void FindAncestor_WhenNoAncestorMatchesType_ReturnsNull()
    {
        var probe = new AncestorProbe();
        using var container = new ProbeContainer();
        container.Children.Add(probe);

        probe.ExposedFindAncestor<Stack>().ShouldBeNull();
    }

    /// <summary>Verifies null is returned when the control has no parent.</summary>
    [Fact]
    public void FindAncestor_WhenControlHasNoParent_ReturnsNull()
    {
        var probe = new AncestorProbe();

        probe.ExposedFindAncestor<ProbeContainer>().ShouldBeNull();
    }

    /// <summary>Verifies the first matching ancestor is returned when multiple match.</summary>
    [Fact]
    public void FindAncestor_WhenMultipleAncestorsMatch_ReturnsNearest()
    {
        var probe = new AncestorProbe();
        using var inner = new ProbeContainer();
        using var outer = new ProbeContainer();
        inner.Children.Add(probe);
        outer.Children.Add(inner);

        probe.ExposedFindAncestor<ProbeContainer>().ShouldBeSameAs(inner);
    }
}
