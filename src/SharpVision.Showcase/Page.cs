using SharpVision.Controls;
using SharpVision.Showcase.Panes;

namespace SharpVision.Showcase;

/// <summary>Defines one immutable catalog entry and builds its fresh documentation control tree.</summary>
internal sealed class Page
{
    private readonly Func<ShowcasePane> _createPane;
    private readonly PropertyDescription[] _properties;
    private readonly InteractionDescription[] _interactions;

    #region Construction and metadata

    /// <summary>Initializes one catalog entry backed by a showcase pane control.</summary>
    /// <param name="name">The non-empty concrete control type name.</param>
    /// <param name="summary">The non-empty purpose and usage summary.</param>
    /// <param name="interactions">The non-empty supported interaction descriptions.</param>
    /// <param name="properties">The non-empty property documentation sequence.</param>
    /// <param name="createPane">A non-null factory returning a fresh detached showcase pane.</param>
    /// <exception cref="ArgumentException">Text is blank or required metadata is empty.</exception>
    /// <exception cref="ArgumentNullException">A sequence or factory is null.</exception>
    internal Page(
        string name,
        string summary,
        IEnumerable<InteractionDescription> interactions,
        IEnumerable<PropertyDescription> properties,
        Func<ShowcasePane> createPane)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(interactions);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(createPane);
        var copiedInteractions = interactions.ToArray();
        var copied = properties.ToArray();
        ValidateMetadata(copiedInteractions, copied);

        Name = name;
        Summary = summary;
        _interactions = copiedInteractions;
        Interaction = string.Join(' ', copiedInteractions.Select(static item => item.Result));
        _properties = copied;
        _createPane = createPane;
    }

    private static void ValidateMetadata(
        InteractionDescription[] interactions,
        PropertyDescription[] properties)
    {
        if (interactions.Length == 0 || interactions.Any(static interaction =>
                string.IsNullOrWhiteSpace(interaction.Input) ||
                string.IsNullOrWhiteSpace(interaction.Behavior) ||
                string.IsNullOrWhiteSpace(interaction.Result)))
        {
            throw new ArgumentException(
                "Every showcase page requires at least one complete interaction description.",
                nameof(interactions));
        }

        if (properties.Length == 0)
        {
            throw new ArgumentException("A showcase page requires property documentation.", nameof(properties));
        }

        if (properties.Any(static property =>
            string.IsNullOrWhiteSpace(property.Name) ||
            string.IsNullOrWhiteSpace(property.Type) ||
            string.IsNullOrWhiteSpace(property.Default) ||
            string.IsNullOrWhiteSpace(property.Description)))
        {
            throw new ArgumentException("Every property description must be initialized.", nameof(properties));
        }
    }

    /// <summary>Gets the exact concrete control type name used by navigation.</summary>
    internal string Name { get; }

    /// <summary>Gets the control's concise purpose and intended use.</summary>
    internal string Summary { get; }

    /// <summary>Gets keyboard, pointer, focus, or display-only behavior guidance.</summary>
    internal string Interaction { get; }

    /// <summary>Gets immutable structured interaction descriptions.</summary>
    internal IReadOnlyList<InteractionDescription> Interactions => _interactions;

    /// <summary>Gets immutable documentation for the control's meaningful properties.</summary>
    internal IReadOnlyList<PropertyDescription> Properties => _properties;

    #endregion

    #region Control tree creation

    /// <summary>Creates one fresh detached live example tree.</summary>
    /// <returns>The detached example root owned by the caller.</returns>
    internal Control CreateExamples()
    {
        using var pane = _createPane();
        var examples = new ControlStack { Spacing = 1 };
        pane.PopulateExamples(examples);
        return examples;
    }

    /// <summary>Creates a fresh documentation page with live examples and typed RichText sections.</summary>
    /// <returns>A detached page root owned by the caller.</returns>
    internal Control CreateContent() => _createPane();

    #endregion
}
