// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

using SharpVision.Controls.Input;
using SharpVision.Documents.Markdown;

/// <summary>Owns the document's painted surface and every projected retained control.</summary>
internal sealed class DocumentPresenter: Container
{
    private static int _nextRadioScope;

    private readonly Document _owner;
    private readonly string _radioScope;
    private readonly DocumentSurface _surface;

    /// <summary>Initializes a presenter with its painted surface as the backmost child.</summary>
    /// <param name="owner">The owning document.</param>
    /// <param name="surface">The painted surface.</param>
    internal DocumentPresenter(Document owner, DocumentSurface surface)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(surface);
        _owner = owner;
        _radioScope = FormattableString.Invariant($"document-{Interlocked.Increment(ref _nextRadioScope)}");
        _surface = surface;
        Children.Add(surface);
    }

    /// <summary>Synchronizes retained children with the current document tree.</summary>
    internal void ReconcileControls()
    {
        var desired = new List<ControlBase>();
        DocumentEmbeddedControlCollector.Collect(_owner.Blocks, desired);
        _ = desired.RemoveAll(static control => control.IsDisposed);

        foreach (var radio in desired.OfType<RadioButton>())
        {
            ScopeMarkdownRadio(radio);
        }

        for (var index = Children.Count - 1; index >= 1; index--)
        {
            if (!desired.Contains(Children[index], ReferenceEqualityComparer.Instance))
            {
                Children.RemoveAt(index);
            }
        }

        for (var index = 0; index < desired.Count; index++)
        {
            var childIndex = index + 1;

            if (childIndex < Children.Count && ReferenceEquals(Children[childIndex], desired[index]))
            {
                continue;
            }

            var existing = Children.IndexOf(desired[index]);

            if (existing >= 0)
            {
                Children.RemoveAt(existing);
            }

            Children.Insert(childIndex, desired[index]);
        }
    }

    private void ScopeMarkdownRadio(RadioButton radio)
    {
        const string prefix = "markdown-radio-";
        const char separator = '@';

        if (!MarkdownRadioRegistry.IsGenerated(radio) ||
            radio.GroupName is not { } group ||
            !group.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        var separatorIndex = group.IndexOf(separator);
        var sourceGroup = separatorIndex < 0 ? group : group[..separatorIndex];
        radio.GroupName = FormattableString.Invariant($"{sourceGroup}{separator}{_radioScope}");
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        for (var index = 1; index < Children.Count; index++)
        {
            _ = MeasureChild(Children[index], new Constraint(null, null));
        }

        var content = _owner.MeasureContent(constraint.Width, force: true);
        _ = MeasureChild(_surface, constraint);
        return content;
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var projectedBounds = _owner.ProjectContentBounds(bounds);
        ArrangeChild(_surface, projectedBounds, ResolvedAxes.Both);

        foreach (var placement in _owner.ControlPlacements)
        {
            var projected = placement.Bounds;
            ArrangeChild(
                placement.Control,
                new Rect(
                    projectedBounds.X + projected.X,
                    projectedBounds.Y + projected.Y,
                    projected.Width,
                    projected.Height),
                ResolvedAxes.Both);
        }

        _owner.RefreshSelectionGeometry(new Point(_surface.ContentBounds.X, _surface.ContentBounds.Y));
    }
}
