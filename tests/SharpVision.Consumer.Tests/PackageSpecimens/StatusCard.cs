// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests.PackageSpecimens;

using ControlText = Controls.Text;

/// <summary>Provides an externally authored retained component with a private two-row implementation tree.</summary>
public sealed class StatusCard: CompositeControl
{
    private readonly ControlText _status;

    /// <summary>Initializes a status card with validated label and status text.</summary>
    /// <param name="label">The non-null label shown above the current status.</param>
    /// <param name="status">The non-null initial status.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public StatusCard(string label, string status)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(status);

        _status = new ControlText(status);
        var root = new Stack { Spacing = 1 };
        root.Children.Add(new ControlText(label));
        root.Children.Add(_status);
        InitializeContent(root);
    }

    /// <summary>Gets or sets the non-null status displayed by the private implementation tree.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached component is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The component is disposed.</exception>
    public string Status
    {
        get => _status.Content;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _status.Content = value;
        }
    }
}
