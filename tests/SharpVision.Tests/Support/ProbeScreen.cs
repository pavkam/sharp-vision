// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using SharpVision.Runtime;


/// <summary>Records screen lifecycle hook order for Application startup tests.</summary>
internal sealed class ProbeScreen: SharpVision.Controls.Screen
{
    /// <summary>Initializes a probe screen with an empty hook-order log.</summary>
    internal ProbeScreen() => Order = [];

    /// <summary>Gets the recorded hook invocation order.</summary>
    internal List<string> Order { get; }

    /// <inheritdoc/>
    protected override void OnAttach(Application application) => Order.Add("attach");

    /// <inheritdoc/>
    protected override void OnStarted(Application application) => Order.Add("started");

    /// <inheritdoc/>
    protected override Control Build()
    {
        Order.Add("build");
        return new ProbeControl();
    }
}
