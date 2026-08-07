// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Records screen lifecycle hook order for Application startup tests.</summary>
internal sealed class ProbeScreen: Screen
{
    private readonly bool _throwOnAttach;
    private readonly bool _throwOnDispose;

    /// <summary>Initializes a probe screen with configurable lifecycle failures.</summary>
    /// <param name="initializeContent">Whether to install the required composition root.</param>
    /// <param name="throwOnAttach">Whether the application-attach hook throws.</param>
    /// <param name="throwOnDispose">Whether the screen-disposal hook throws.</param>
    /// <param name="throwOnContentDisposing">Whether the owned composition root throws while disposing.</param>
    internal ProbeScreen(
        bool initializeContent = true,
        bool throwOnAttach = false,
        bool throwOnDispose = false,
        bool throwOnContentDisposing = false)
    {
        Order = [];
        _throwOnAttach = throwOnAttach;
        _throwOnDispose = throwOnDispose;
        ContentRoot = new ProbeControl
        {
            ThrowOnDisposing = throwOnContentDisposing,
            Measuring = _ => Order.Add("measure")
        };

        if (initializeContent)
        {
            InitializeContent(ContentRoot);
            Order.Add("compose");
        }
    }

    /// <summary>Gets the recorded hook invocation order.</summary>
    internal List<string> Order { get; }

    /// <summary>Gets the application currently bound to the screen.</summary>
    internal Application? BoundApplication => Application;

    /// <summary>Gets the composition root created during construction.</summary>
    internal ProbeControl ContentRoot { get; }

    /// <inheritdoc/>
    protected override void OnAttach(Application application)
    {
        Order.Add("attach");

        if (_throwOnAttach)
        {
            throw new InvalidOperationException("The screen attach hook failed.");
        }
    }

    /// <inheritdoc/>
    protected override void OnStarted(Application application) => Order.Add("started");

    /// <inheritdoc/>
    protected override void OnDispose()
    {
        Order.Add("dispose");

        if (_throwOnDispose)
        {
            throw new InvalidOperationException("The screen dispose hook failed.");
        }
    }
}
