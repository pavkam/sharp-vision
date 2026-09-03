// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies AccessKeyManager construction guards.</summary>
public sealed class AccessKeyManagerConditionTests
{
    /// <summary>Verifies the manager refuses focus or modality services that own a different root.</summary>
    [Fact]
    public async Task Constructor_WhenServicesOwnAnotherRoot_ThrowsArgumentExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            using var root = new Stack();
            using var other = new Stack();
            root.Attach(dispatcher);
            other.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var otherFocus = new FocusManager(other);
            using var otherPointer = new PointerManager(other);
            using var otherModality = new ModalityManager(other, otherFocus, otherPointer);

            // Act and assert
            var focusFailure = Should.Throw<ArgumentException>(() => new AccessKeyManager(root, otherFocus, modality));
            focusFailure.ParamName.ShouldBe("root");
            var modalityFailure = Should.Throw<ArgumentException>(() => new AccessKeyManager(root, focus, otherModality));
            modalityFailure.ParamName.ShouldBe("root");
            _ = Should.NotThrow(() => new AccessKeyManager(root, focus, modality));
        }, TestContext.Current.CancellationToken);
    }
}
