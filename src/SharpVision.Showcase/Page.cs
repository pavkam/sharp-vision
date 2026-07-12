using System.Diagnostics;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Rendering;

using ControlRun = SharpVision.Controls.Run;
using ControlStack = SharpVision.Controls.Stack;
using ControlTable = SharpVision.Controls.Table;
using ControlText = SharpVision.Controls.Text;
using Wrapping = SharpVision.Text.Wrapping;

namespace SharpVision.Showcase;

/// <summary>Defines one immutable catalog entry and builds its fresh documentation control tree.</summary>
internal sealed class Page
{
    private readonly Func<Control> _examples;
    private readonly PropertyDescription[] _properties;
    private readonly InteractionDescription[] _interactions;

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
        : this(
            name,
            summary,
            [new InteractionDescription("General", "Use the documented control interaction.", interaction)],
            properties,
            examples)
    {
    }

    /// <summary>Initializes one complete catalog entry with structured interaction metadata.</summary>
    /// <param name="name">The non-empty concrete control type name.</param>
    /// <param name="summary">The non-empty purpose and usage summary.</param>
    /// <param name="interactions">The non-empty supported interaction descriptions.</param>
    /// <param name="properties">The non-empty property documentation sequence.</param>
    /// <param name="examples">A non-null factory returning a fresh detached example tree.</param>
    /// <exception cref="ArgumentException">Text is blank or required metadata is empty.</exception>
    /// <exception cref="ArgumentNullException">A sequence or factory is null.</exception>
    internal Page(
        string name,
        string summary,
        IEnumerable<InteractionDescription> interactions,
        IEnumerable<PropertyDescription> properties,
        Func<Control> examples)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(interactions);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(examples);
        var copiedInteractions = interactions.ToArray();
        var copied = properties.ToArray();

        if (copiedInteractions.Length == 0 || copiedInteractions.Any(static interaction =>
                string.IsNullOrWhiteSpace(interaction.Input) ||
                string.IsNullOrWhiteSpace(interaction.Behavior) ||
                string.IsNullOrWhiteSpace(interaction.Result)))
        {
            throw new ArgumentException(
                "Every showcase page requires at least one complete interaction description.",
                nameof(interactions));
        }

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
        _interactions = copiedInteractions;
        Interaction = string.Join(' ', copiedInteractions.Select(static item => item.Result));
        _properties = copied;
        _examples = examples;
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
        content.Children.Add(Section("Practical recipe"));
        content.Children.Add(Narrative(Summary, _interactions));
        content.Children.Add(Section("Examples"));
        content.Children.Add(Card(CreateExamples()));
        content.Children.Add(Section("Technical details"));
        content.Children.Add(TechnicalDetails());

        content.Children.Add(Section("Interaction"));
        content.Children.Add(InteractionTable());
        return content;
    }

    #endregion

    #region RichText composition

    private static Border Heading(string title, string summary)
    {
        var text = new RichText
        {
            Padding = new Thickness(1, 0),
            Style = Palette.HeaderText(),
            Wrapping = Wrapping.Word,
        };
        text.Inlines.Add(new ControlRun(title)
        {
            Foreground = Palette.Accent,
            Attributes = Attributes.Bold,
        });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new ControlRun("Overview")
        {
            Foreground = Palette.Success,
            Attributes = Attributes.Bold,
        });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new ControlRun(summary));
        return new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderColor = Palette.Accent,
            Background = Palette.Canvas,
            Child = text,
        };
    }

    private static RichText Section(string title)
    {
        var text = new RichText { Wrapping = Wrapping.Word };
        text.Inlines.Add(new ControlRun(title)
        {
            Foreground = Palette.Warning,
            Attributes = Attributes.Bold,
        });
        return text;
    }

    private static Border Card(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new Border
        {
            Padding = new Thickness(1),
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Rounded,
            BorderColor = Palette.Border,
            Background = Palette.Surface,
            Child = content,
        };
    }

    private static RichText Narrative(string summary, InteractionDescription[] interactions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(interactions);

        var text = new RichText
        {
            Padding = new Thickness(1, 0),
            Style = Palette.HeaderText(),
            Wrapping = Wrapping.Word,
        };

        text.Inlines.Add(new ControlRun("Use this control when ")
        {
            Foreground = Palette.Success,
            Attributes = Attributes.Bold,
        });
        text.Inlines.Add(new ControlRun(summary));
        text.Inlines.Add(new ControlRun(" Explore it in the live example below: ")
        {
            Foreground = Palette.Accent,
            Attributes = Attributes.Bold,
        });

        for (var index = 0; index < interactions.Length; index++)
        {
            var interaction = interactions[index];
            if (index > 0)
            {
                text.Inlines.Add(new ControlRun(" "));
            }

            text.Inlines.Add(new ControlRun($"{interaction.Input} — ")
            {
                Foreground = Palette.Warning,
                Attributes = Attributes.Bold,
            });
            text.Inlines.Add(new ControlRun($"{interaction.Behavior}; {interaction.Result}"));
        }

        text.Inlines.Add(new ControlRun(
            " Resize the terminal as you explore: this narrative, the live specimens, and the tables below reflow together so the control's behavior stays readable at every width.")
        {
            Foreground = Palette.Muted,
        });
        return text;
    }

    private ControlTable TechnicalDetails()
    {
        var table = new ControlTable
        {
            ShowGridLines = true,
            GridLineColor = Palette.Border,
            HeaderForeground = Palette.Text,
            HeaderBackground = Palette.Highlight,
            CellPadding = new Thickness(1, 0),
        };
        table.Columns.Add(TableColumn.Fixed("Property", 18));
        table.Columns.Add(TableColumn.Fixed("Type", 16));
        table.Columns.Add(TableColumn.Fixed("Default", 18));
        table.Columns.Add(TableColumn.Fill("Meaning"));

        foreach (var property in _properties)
        {
            table.Rows.Add(new TableRow([
                new ControlText(property.Name) { Foreground = Palette.Accent, Attributes = Attributes.Bold },
                new ControlText(property.Type) { Foreground = Palette.Muted },
                new ControlText(property.Default) { Foreground = Palette.Muted },
                new ControlText(property.Description) { Wrapping = Wrapping.Word },
            ]));
        }

        return table;
    }

    private ControlTable InteractionTable()
    {
        var table = new ControlTable
        {
            ShowGridLines = true,
            GridLineColor = Palette.Border,
            HeaderForeground = Palette.Text,
            HeaderBackground = Palette.Highlight,
            CellPadding = new Thickness(1, 0),
        };
        table.Columns.Add(TableColumn.Fixed("Input", 18));
        table.Columns.Add(TableColumn.Fixed("Behavior", 28));
        table.Columns.Add(TableColumn.Fill("Result"));

        foreach (var interaction in _interactions)
        {
            table.Rows.Add(new TableRow([
                new ControlText(interaction.Input) { Foreground = Palette.Accent, Attributes = Attributes.Bold },
                new ControlText(interaction.Behavior) { Foreground = Palette.Warning, Wrapping = Wrapping.Word },
                new ControlText(interaction.Result) { Wrapping = Wrapping.Word },
            ]));
        }

        return table;
    }

    #endregion
}
