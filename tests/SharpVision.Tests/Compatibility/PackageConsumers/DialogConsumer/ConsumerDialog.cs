// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DialogConsumer;

/// <summary>Exercises the protected PresentAsync overload exposed by the packed SharpVision package.</summary>
public sealed class ConsumerDialog: Dialog<bool>
{
    /// <summary>Initializes a dialog whose cancelled result is false.</summary>
    public ConsumerDialog(): base(cancelledResult: false) =>
        Content = new DisplayText("Consumer dialog");

    /// <summary>Presents this dialog over the given owner's presentation host.</summary>
    public Task<bool> ShowAsync(Control owner, CancellationToken cancellationToken) =>
        PresentAsync(owner, initialFocus: null, cancellationToken);

    /// <summary>Completes this dialog with a true result through the protected Complete method.</summary>
    public void Accept() => _ = Complete(true);
}
