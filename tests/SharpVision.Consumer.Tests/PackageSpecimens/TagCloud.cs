// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests.PackageSpecimens;

using System.Collections.ObjectModel;

using ControlText = Controls.Text;

/// <summary>Proves a third party can expose typed semantic items over a private presentation host.</summary>
public sealed class TagCloud: ItemsControl
{
    private readonly List<string> _tags = [];
    private readonly ReadOnlyCollection<string> _tagsView;

    /// <summary>Initializes an empty horizontally arranged tag collection.</summary>
    public TagCloud()
    {
        _tagsView = _tags.AsReadOnly();
        InitializeItemsHost(new Stack { Orientation = Orientation.Horizontal });
    }

    /// <summary>Gets the typed semantic tags without exposing realized controls.</summary>
    public IReadOnlyList<string> Tags => _tagsView;

    /// <summary>Adds one non-null tag and realizes its text control.</summary>
    /// <param name="tag">The tag text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tag"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached cloud is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The cloud is disposed.</exception>
    public void Add(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        InsertItemControl(_tags.Count, new ControlText { Content = tag });
        _tags.Add(tag);
    }

    /// <summary>Gets the number of typed semantic tags.</summary>
    public int Count => _tags.Count;
}
