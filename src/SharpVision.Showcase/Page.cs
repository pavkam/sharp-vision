using System.Diagnostics;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Rendering;

using ControlRun = SharpVision.Controls.Run;
using ControlStack = SharpVision.Controls.Stack;
using Wrapping = SharpVision.Text.Wrapping;

namespace SharpVision.Showcase;

/// <summary>Defines one immutable catalog entry and builds its fresh documentation control tree.</summary>
internal sealed class Page
{
    private readonly Func<Control> _examples;
    private readonly PropertyDescription[] _properties;

    #region Construction and metadata

    /// <summary>Initializes one complete control page.</summary>
    /// <param name="name">The non-empty concrete control type name.</param>
    /// <param name="summary">The non-empty purpose and usage summary.</param>
    /// <param name="interaction">The non-empty keyboard, pointer, or display behavior guidance.</param>
    /// <param name="properties">The non-empty property documentation sequence.</param>
    /// <param name="examples">A non-null factory returning a fresh detached example tree.</param>
    /// <exception cref="ArgumentException">
    /// Text is empty, no properties are supplied, or a property is the invalid default value.
    /// </exception>
    /// <exception cref="ArgumentNullException">Properties or the example factory is null.</exception>
    internal Page(
        string name,
        string summary,
        string interaction,
        IEnumerable<PropertyDescription> properties,
        Func<Control> examples)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(interaction);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(examples);
        var copied = properties.ToArray();

        if (copied.Length == 0)
        {
            throw new ArgumentException("A showcase page requires property documentation.", nameof(properties));
        }

        if (copied.Any(static property =>
            string.IsNullOrWhiteSpace(property.Name) ||
            string.IsNullOrWhiteSpace(property.Type) ||
            string.IsNullOrWhiteSpace(property.Default) ||
            string.IsNullOrWhiteSpace(property.Description)))
        {
            throw new ArgumentException("Every property description must be initialized.", nameof(properties));
        }

        Name = name;
        Summary = summary;
        Interaction = interaction;
        _properties = copied;
        _examples = examples;
    }

    /// <summary>Gets the exact concrete control type name used by navigation.</summary>
    internal string Name { get; }

    /// <summary>Gets the control's concise purpose and intended use.</summary>
    internal string Summary { get; }

    /// <summary>Gets keyboard, pointer, focus, or display-only behavior guidance.</summary>
    internal string Interaction { get; }

    /// <summary>Gets immutable documentation for the control's meaningful properties.</summary>
    internal IReadOnlyList<PropertyDescription> Properties => _properties;

    #endregion

    #region Control tree creation

    /// <summary>Creates one fresh detached live example tree.</summary>
    /// <returns>The detached example root owned by the caller.</returns>
    /// <exception cref="InvalidOperationException">
    /// The factory returns null or a control that already has a parent.
    /// </exception>
    internal Control CreateExamples()
    {
        var control = _examples() ??
            throw new InvalidOperationException($"The {Name} example factory returned null.");
        return control.Parent is null
            ? control
            : throw new InvalidOperationException($"The {Name} example factory returned an owned control.");
    }

    /// <summary>Creates a fresh documentation page with live examples and typed RichText sections.</summary>
    /// <returns>A detached page root owned by the caller.</returns>
    internal ControlStack CreateContent()
    {
        Debug.Assert(_properties.Length > 0, "Constructor validation guarantees page documentation.");
        var content = new ControlStack
        {
            Padding = new Thickness(1),
            Spacing = 1,
        };
        content.Children.Add(Heading(Name, Summary));
        content.Children.Add(Section("Examples"));
        content.Children.Add(CreateExamples());
        content.Children.Add(Section("Properties"));

        foreach (var property in _properties)
        {
            content.Children.Add(Property(property));
        }

        content.Children.Add(Section("Interaction"));
        content.Children.Add(Paragraph(Interaction));
        return content;
    }

    #endregion

    #region RichText composition

    private static RichText Heading(string title, string summary)
    {
        var text = new RichText { Wrapping = Wrapping.Word };
        text.Inlines.Add(new ControlRun(title) { Attributes = Attributes.Bold });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new ControlRun("Overview") { Attributes = Attributes.Bold });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new ControlRun(summary));
        return text;
    }

    private static RichText Section(string title)
    {
        var text = new RichText { Wrapping = Wrapping.Word };
        text.Inlines.Add(new ControlRun(title) { Attributes = Attributes.Bold | Attributes.Underline });
        return text;
    }

    private static RichText Paragraph(string content)
    {
        var text = new RichText { Wrapping = Wrapping.Word };
        text.Inlines.Add(new ControlRun(content));
        return text;
    }

    private static Border Property(PropertyDescription property)
    {
        var text = new RichText
        {
            Padding = new Thickness(1, 0),
            Wrapping = Wrapping.Word,
        };
        text.Inlines.Add(new ControlRun(property.Name) { Attributes = Attributes.Bold });
        text.Inlines.Add(new ControlRun($"  {property.Type}  ·  default: {property.Default}"));
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new ControlRun(property.Description));
        return new Border
        {
            BorderThickness = new Thickness(1),
            Child = text,
            Glyphs = Glyphs.Rounded,
        };
    }

    #endregion
}
