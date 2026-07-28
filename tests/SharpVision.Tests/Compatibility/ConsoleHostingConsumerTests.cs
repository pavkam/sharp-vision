// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

/// <summary>Compiles and executes console-hosting compatibility through public APIs only.</summary>
public sealed class ConsoleHostingConsumerTests
{
    /// <summary>Verifies the parameterless terminal-options mapping remains source-compatible and semantic.</summary>
    [Fact]
    public void ToTerminalOptions_WhenCalledWithoutProfile_ReturnsAnsiCompatibilityPolicy()
    {
        var terminal = new ConsoleRunOptions().ToTerminalOptions();

        terminal.Profile.Description.Name.ShouldBe("ansi");
        terminal.Profile.Description.Suitability.ShouldBe(Suitability.Usable);
        terminal.Profile.Capabilities.CellMouse.ShouldBe(
            new Feature(Terminal.Capabilities.Support.Supported, Origin.Override));
        _ = terminal.Negotiation.ShouldNotBeNull();
    }

    /// <summary>Verifies public description result and diagnostic types are consumable without friend access.</summary>
    [Fact]
    public void DescriptionTypes_WhenReferenced_ArePublicReadOnlyContracts()
    {
        DescriptionResult? result = null;
        var diagnostic = default(DescriptionDiagnostic);

        result.ShouldBeNull();
        diagnostic.Code.ShouldBe(DescriptionDiagnosticCode.WrongType);
        diagnostic.Capability.ShouldBeNull();
        DescriptionLoadStatus.ProviderFailed.ShouldNotBe(DescriptionLoadStatus.Loaded);
    }

    /// <summary>Verifies cursor-shape and terminal-service additions are public package contracts.</summary>
    [Fact]
    public void RenderingProfileTypes_WhenReferenced_ArePublicContracts()
    {
        var input = new TextInput { CursorShape = CursorShape.Bar };
        var cursor = new Cursor(new Point(1, 2), visible: true, CursorShape.Underline);
        ITerminalServices? services = null;

        input.CursorShape.ShouldBe(CursorShape.Bar);
        cursor.Shape.ShouldBe(CursorShape.Underline);
        _ = Should.Throw<NullReferenceException>(() =>
        {
            _ = services!.Description;
            _ = services.IsTitleSupported;
            _ = services.Bell.IsSupported;
        });
    }
}
