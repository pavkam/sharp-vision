// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Backends;

using SharpVision.Terminal.Backends;

/// <summary>Verifies terminal backend identity and protocol-family composition.</summary>
public sealed class TerminalBackendTests
{
    /// <summary>Verifies the terminal assembly exposes the internal backend hierarchy with its intended shapes.</summary>
    [Fact]
    public void BackendHierarchyTypes_WhenLoadedFromTerminalAssembly_AreInternalAndHaveExpectedShapes()
    {
        var assembly = typeof(Renderer).Assembly;
        var names = new[]
        {
            "SharpVision.Terminal.Backends.TerminalBackendKind",
            "SharpVision.Terminal.Backends.ProtocolExtensionKind",
            "SharpVision.Terminal.Backends.ProtocolExtension",
            "SharpVision.Terminal.Backends.TerminalBackend",
            "SharpVision.Terminal.Backends.VtBackend",
            "SharpVision.Terminal.Backends.XtermBackend",
            "SharpVision.Terminal.Backends.KittyBackend",
            "SharpVision.Terminal.Backends.ItermBackend",
        };

        var types = names
            .Select(assembly.GetType)
            .Select(type => type.ShouldNotBeNull())
            .ToArray();

        foreach (var type in types)
        {
            type.IsNotPublic.ShouldBeTrue();
        }

        types[0].IsEnum.ShouldBeTrue();
        types[1].IsEnum.ShouldBeTrue();
        types[2].IsValueType.ShouldBeTrue();
        types[2].IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), inherit: false)
            .ShouldBeTrue();
        types[3].IsAbstract.ShouldBeTrue();
        types[4].BaseType.ShouldBe(types[3]);
        types[5].BaseType.ShouldBe(types[4]);
        types[6].BaseType.ShouldBe(types[5]);
        types[7].BaseType.ShouldBe(types[5]);
        types[6].IsSealed.ShouldBeTrue();
        types[7].IsSealed.ShouldBeTrue();
    }

    /// <summary>Verifies derived backends expose inherited protocol families before local families.</summary>
    [Fact]
    public void Extensions_WhenBackendsAreDerived_ContainInheritedFamilies()
    {
        var backends = new TerminalBackend[]
        {
            VtBackend.Instance,
            XtermBackend.Instance,
            KittyBackend.Instance,
            ItermBackend.Instance,
        };
        var expectedKinds = new[]
        {
            TerminalBackendKind.Vt,
            TerminalBackendKind.Xterm,
            TerminalBackendKind.Kitty,
            TerminalBackendKind.Iterm2,
        };
        var expectedNames = new[] { "VT", "xterm", "Kitty", "iTerm2" };
        ProtocolExtensionKind[][] expectedExtensions =
        [
            [ProtocolExtensionKind.Vt],
            [ProtocolExtensionKind.Vt, ProtocolExtensionKind.Xterm],
            [ProtocolExtensionKind.Vt, ProtocolExtensionKind.Xterm, ProtocolExtensionKind.Kitty],
            [ProtocolExtensionKind.Vt, ProtocolExtensionKind.Xterm, ProtocolExtensionKind.Iterm2],
        ];

        for (var index = 0; index < backends.Length; index++)
        {
            backends[index].Kind.ShouldBe(expectedKinds[index]);
            backends[index].Name.ShouldBe(expectedNames[index]);
            backends[index].Extensions.Select(extension => extension.Kind).ShouldBe(expectedExtensions[index]);
        }
    }

    /// <summary>Verifies backend instances and their published extension collections are stable and immutable.</summary>
    [Fact]
    public void Extensions_WhenReadFromSingleton_AreStableAndImmutable()
    {
        var backend = KittyBackend.Instance;
        var extensions = backend.Extensions;

        KittyBackend.Instance.ShouldBeSameAs(backend);
        backend.Extensions.ShouldBeSameAs(extensions);
        var mutableView = extensions.ShouldBeAssignableTo<IList<ProtocolExtension>>();
        var exception = Should.Throw<NotSupportedException>(() => mutableView.Add(new ProtocolExtension(ProtocolExtensionKind.Iterm2)));
        exception.Message.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>Verifies protocol extension construction rejects unknown families.</summary>
    [Fact]
    public void Constructor_WhenProtocolExtensionKindIsUnknown_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ProtocolExtension((ProtocolExtensionKind) int.MaxValue));
    }

    /// <summary>Verifies terminal backend construction rejects an unknown family.</summary>
    [Fact]
    public void Constructor_WhenTerminalBackendKindIsUnknown_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new DuplicateTerminalBackend((TerminalBackendKind) int.MaxValue, "Unknown"));
    }

    /// <summary>Verifies terminal backend construction rejects a blank display name.</summary>
    [Fact]
    public void Constructor_WhenTerminalBackendNameIsBlank_ThrowsArgumentException()
    {
        _ = Should.Throw<ArgumentException>(() =>
            new DuplicateTerminalBackend(TerminalBackendKind.Vt, " \t"));
    }

    /// <summary>Verifies terminal backend type inheritance remains shallow and explicit.</summary>
    [Fact]
    public void BackendHierarchyTypes_WhenLoadedFromTerminalAssembly_HaveExpectedInheritance()
    {
        typeof(VtBackend).BaseType.ShouldBe(typeof(TerminalBackend));
        typeof(XtermBackend).BaseType.ShouldBe(typeof(VtBackend));
        typeof(KittyBackend).BaseType.ShouldBe(typeof(XtermBackend));
        typeof(ItermBackend).BaseType.ShouldBe(typeof(XtermBackend));
    }

    /// <summary>Verifies duplicate protocol-family contributions fail with the backend name.</summary>
    [Fact]
    public void Extensions_WhenFamilyIsDuplicated_Throws()
    {
        var backend = new DuplicateTerminalBackend();

        var exception = Should.Throw<InvalidOperationException>(() => _ = backend.Extensions);

        exception.Message.ShouldContain(backend.Name);
    }
}
