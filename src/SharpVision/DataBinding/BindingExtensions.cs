// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DataBinding;

using System.Collections.Specialized;
using System.Linq.Expressions;
using System.Windows.Input;

using Controls.Charts;
using Controls.Collections;
using Controls.Display;
using Controls.Scrolling;

using Menus;

using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;

using ControlCalendar = Controls.Input.Calendar;
using DisplayText = Controls.Display.Text;
using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>Creates strongly typed model relationships for retained SharpVision controls.</summary>
[PublicAPI]
public static class BindingExtensions
{
    /// <summary>Binds nullable model state two-way to a check box.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this CheckBox target,
        TModel source,
        Expression<Func<TModel, bool?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.IsChecked,
            source,
            sourceProperty,
            BindingMode.TwoWay);

    /// <summary>Binds model state two-way to a radio button.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this RadioButton target,
        TModel source,
        Expression<Func<TModel, bool>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.IsChecked,
            source,
            sourceProperty,
            BindingMode.TwoWay);

    /// <summary>Binds an integer model value two-way to a slider.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this Slider target,
        TModel source,
        Expression<Func<TModel, int>> sourceProperty)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(target);
        return BindProperty(
            target,
            static control => control.Value,
            source,
            sourceProperty,
            static value => value,
            static value => value,
            BindingMode.TwoWay,
            target.Minimum);
    }

    /// <summary>Binds an integer model value two-way to a scroll bar.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this ScrollBar target,
        TModel source,
        Expression<Func<TModel, int>> sourceProperty)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(target);
        return BindProperty(
            target,
            static control => control.Value,
            source,
            sourceProperty,
            static value => value,
            static value => value,
            BindingMode.TwoWay,
            target.Minimum);
    }

    /// <summary>Binds a floating-point model value one-way to a progress bar.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this ProgressBar target,
        TModel source,
        Expression<Func<TModel, double>> sourceProperty)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(target);
        return BindProperty(
            target,
            static control => control.Value,
            source,
            sourceProperty,
            static value => value,
            convertBack: null,
            BindingMode.OneWay,
            target.Minimum);
    }

    /// <summary>Binds observable chart series one-way to a horizontal bar chart.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this HorizontalBarChart target,
        TModel source,
        Expression<Func<TModel, IReadOnlyList<ChartSeries>?>> sourceProperty)
        where TModel : class => BindChartSeries(target, static chart => chart.Series, source, sourceProperty);

    /// <summary>Binds observable chart series one-way to a vertical bar chart.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this VerticalBarChart target,
        TModel source,
        Expression<Func<TModel, IReadOnlyList<ChartSeries>?>> sourceProperty)
        where TModel : class => BindChartSeries(target, static chart => chart.Series, source, sourceProperty);

    /// <summary>Binds observable chart series one-way to a line chart.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this LineChart target,
        TModel source,
        Expression<Func<TModel, IReadOnlyList<ChartSeries>?>> sourceProperty)
        where TModel : class => BindChartSeries(target, static chart => chart.Series, source, sourceProperty);

    /// <summary>Binds observable chart series one-way to an area chart.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this AreaChart target,
        TModel source,
        Expression<Func<TModel, IReadOnlyList<ChartSeries>?>> sourceProperty)
        where TModel : class => BindChartSeries(target, static chart => chart.Series, source, sourceProperty);

    /// <summary>Binds zero or one observable chart series one-way to a sparkline.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this Sparkline target,
        TModel source,
        Expression<Func<TModel, IReadOnlyList<ChartSeries>?>> sourceProperty)
        where TModel : class => BindChartSeries(target, static chart => chart.Series, source, sourceProperty);

    /// <summary>Binds a color model value two-way to a color picker.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this ColorPicker target,
        TModel source,
        Expression<Func<TModel, Color>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Value,
            source,
            sourceProperty,
            BindingMode.TwoWay);

    /// <summary>Binds a selected index two-way to a list.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this ListView target,
        TModel source,
        Expression<Func<TModel, int>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.SelectedIndex,
            source,
            sourceProperty,
            static value => value,
            static value => value,
            BindingMode.TwoWay,
            -1);

    /// <summary>Binds a selected index two-way to a combo box.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this ComboBox target,
        TModel source,
        Expression<Func<TModel, int>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.SelectedIndex,
            source,
            sourceProperty,
            static value => value,
            static value => value,
            BindingMode.TwoWay,
            -1);

    /// <summary>Binds a selected index two-way to a tab control.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this TabControl target,
        TModel source,
        Expression<Func<TModel, int>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.SelectedIndex,
            source,
            sourceProperty,
            static value => value,
            static value => value,
            BindingMode.TwoWay,
            -1);

    /// <summary>Binds a selected index two-way to a menu.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this Menu target,
        TModel source,
        Expression<Func<TModel, int>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.SelectedIndex,
            source,
            sourceProperty,
            static value => value,
            static value => value,
            BindingMode.TwoWay,
            -1);

    /// <summary>Binds a nullable date interval two-way to a calendar.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this ControlCalendar target,
        TModel source,
        Expression<Func<TModel, DateInterval?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Selection,
            source,
            sourceProperty,
            BindingMode.TwoWay);

    /// <summary>Binds a nullable date model value two-way to a date input.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this DateInput target,
        TModel source,
        Expression<Func<TModel, DateOnly?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Value,
            source,
            sourceProperty,
            BindingMode.TwoWay);

    /// <summary>Binds a nullable time model value two-way to a time input.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this TimeInput target,
        TModel source,
        Expression<Func<TModel, TimeOnly?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Value,
            source,
            sourceProperty,
            BindingMode.TwoWay);

    /// <summary>Binds a nullable date-time model value two-way to a date-time input.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this DateTimeInput target,
        TModel source,
        Expression<Func<TModel, DateTime?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Value,
            source,
            sourceProperty,
            BindingMode.TwoWay);

    /// <summary>Binds an expansion state two-way to an expander.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this Expander target,
        TModel source,
        Expression<Func<TModel, bool>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.IsExpanded,
            source,
            sourceProperty,
            BindingMode.TwoWay);

    /// <summary>Binds optional model text one-way to FIGlet text.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this FigletText target,
        TModel source,
        Expression<Func<TModel, string?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Content,
            source,
            sourceProperty,
            static value => value ?? string.Empty,
            convertBack: null,
            BindingMode.OneWay,
            string.Empty);

    /// <summary>Binds a command property one-way to a control that opted into the command capability.</summary>
    [MustDisposeResource]
    public static Binding BindCommand<TModel>(
        this InputBase target,
        TModel source,
        Expression<Func<TModel, ICommand?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Command,
            source,
            sourceProperty,
            BindingMode.OneWay);

    /// <summary>Binds a borrowed parameter property one-way to a control that opted into the command
    /// capability.</summary>
    [MustDisposeResource]
    public static Binding BindCommandParameter<TModel, TValue>(
        this InputBase target,
        TModel source,
        Expression<Func<TModel, TValue>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.CommandParameter,
            source,
            sourceProperty,
            static value => value,
            convertBack: null,
            BindingMode.OneWay,
            fallbackValue: null);

    /// <summary>Binds optional model text one-way to displayed text.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this DisplayText target,
        TModel source,
        Expression<Func<TModel, string?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Content,
            source,
            sourceProperty,
            static value => value ?? string.Empty,
            convertBack: null,
            BindingMode.OneWay,
            string.Empty);

    /// <summary>Binds optional model text one-way to a control's caption.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this InputBase target,
        TModel source,
        Expression<Func<TModel, string?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Text,
            source,
            sourceProperty,
            static value => value ?? string.Empty,
            convertBack: null,
            BindingMode.OneWay,
            string.Empty);

    /// <summary>Binds optional model text two-way to editable text.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this TextInput target,
        TModel source,
        Expression<Func<TModel, string?>> sourceProperty)
        where TModel : class =>
        Bind(target, source, sourceProperty, BindingMode.TwoWay);

    /// <summary>Binds optional model text to editable text with an explicit direction.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this TextInput target,
        TModel source,
        Expression<Func<TModel, string?>> sourceProperty,
        BindingMode mode)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Text,
            source,
            sourceProperty,
            static value => value ?? string.Empty,
            static value => value,
            mode,
            string.Empty);

    /// <summary>Binds optional model text two-way to an editable suggestion input.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this SuggestionInput target,
        TModel source,
        Expression<Func<TModel, string?>> sourceProperty)
        where TModel : class =>
        Bind(target, source, sourceProperty, BindingMode.TwoWay);

    /// <summary>Binds optional model text to an editable suggestion input with an explicit direction.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this SuggestionInput target,
        TModel source,
        Expression<Func<TModel, string?>> sourceProperty,
        BindingMode mode)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Text,
            source,
            sourceProperty,
            static value => value ?? string.Empty,
            static value => value,
            mode,
            string.Empty);

    /// <summary>Binds one finite observable item snapshot to a list.</summary>
    [MustDisposeResource]
    public static Binding BindItems<TModel, TItem>(
        this ListView target,
        TModel source,
        Expression<Func<TModel, IReadOnlyList<TItem>?>> sourceProperty)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(target);

        return CreateBinding(
            target,
            static control => control.Items,
            source,
            sourceProperty,
            Snapshot,
            convertBack: null,
            BindingMode.OneWay,
            Array.Empty<object?>(),
            tracksCollection: true,
            coordinatesItems: true,
            refreshAfterItems: false,
            applyIncrementalChange: CreateListIncrementalCallback(target));
    }

    /// <summary>Binds one finite observable item snapshot to a combo box.</summary>
    [MustDisposeResource]
    public static Binding BindItems<TModel, TItem>(
        this ComboBox target,
        TModel source,
        Expression<Func<TModel, IReadOnlyList<TItem>?>> sourceProperty)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(target);

        return CreateBinding(
            target,
            static control => control.Items,
            source,
            sourceProperty,
            Snapshot,
            convertBack: null,
            BindingMode.OneWay,
            Array.Empty<object?>(),
            tracksCollection: true,
            coordinatesItems: true,
            refreshAfterItems: false,
            applyIncrementalChange: CreateListIncrementalCallback(target.GetDropDownList()));
    }

    /// <summary>Binds one nullable selected item two-way to a single-selection list.</summary>
    [MustDisposeResource]
    public static Binding BindSelection<TModel, TItem>(
        this ListView target,
        TModel source,
        Expression<Func<TModel, TItem?>> sourceProperty)
        where TModel : class
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(target);

        _ = target.SelectionMode == ListSelectionMode.Single
            ? true
            : throw new ArgumentException(
                "Selected-item binding requires ListSelectionMode.Single.",
                nameof(target));

        return CreateBinding(
            target,
            static control => control.SelectedIndex,
            source,
            sourceProperty,
            value => IndexOf(target.Items, value),
            index => ItemAt<TItem>(target.Items, index),
            BindingMode.TwoWay,
            -1,
            tracksCollection: false,
            coordinatesItems: false,
            refreshAfterItems: true);
    }

    /// <summary>Binds one nullable selected item two-way to a combo box.</summary>
    [MustDisposeResource]
    public static Binding BindSelection<TModel, TItem>(
        this ComboBox target,
        TModel source,
        Expression<Func<TModel, TItem?>> sourceProperty)
        where TModel : class
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(target);
        return CreateBinding(
            target,
            static control => control.SelectedIndex,
            source,
            sourceProperty,
            value => IndexOf(target.Items, value),
            index => ItemAt<TItem>(target.Items, index),
            BindingMode.TwoWay,
            -1,
            tracksCollection: false,
            coordinatesItems: false,
            refreshAfterItems: true);
    }

    /// <summary>Binds equal source and target property types with an explicit direction.</summary>
    [MustDisposeResource]
    public static Binding BindProperty<TControl, TModel, TValue>(
        this TControl target,
        Expression<Func<TControl, TValue>> targetProperty,
        TModel source,
        Expression<Func<TModel, TValue>> sourceProperty,
        BindingMode mode)
        where TControl : ControlBase
        where TModel : class =>
        BindProperty(
            target,
            targetProperty,
            source,
            sourceProperty,
            static value => value,
            static value => value,
            mode,
            default!);

    /// <summary>Binds converted source and target properties with an explicit direction and fallback.</summary>
    [MustDisposeResource]
    public static Binding BindProperty<TControl, TModel, TSource, TTarget>(
        this TControl target,
        Expression<Func<TControl, TTarget>> targetProperty,
        TModel source,
        Expression<Func<TModel, TSource>> sourceProperty,
        Func<TSource, TTarget> convert,
        Func<TTarget, TSource>? convertBack,
        BindingMode mode,
        TTarget fallbackValue)
        where TControl : ControlBase
        where TModel : class =>
        CreateBinding(
            target,
            targetProperty,
            source,
            sourceProperty,
            convert,
            convertBack,
            mode,
            fallbackValue,
            tracksCollection: false,
            coordinatesItems: false,
            refreshAfterItems: false);

    [MustDisposeResource]
    private static Binding CreateBinding<TControl, TModel, TSource, TTarget>(
        TControl target,
        Expression<Func<TControl, TTarget>> targetProperty,
        TModel source,
        Expression<Func<TModel, TSource>> sourceProperty,
        Func<TSource, TTarget> convert,
        Func<TTarget, TSource>? convertBack,
        BindingMode mode,
        TTarget fallbackValue,
        bool tracksCollection,
        bool coordinatesItems,
        bool refreshAfterItems,
        Func<NotifyCollectionChangedEventArgs, bool>? applyIncrementalChange = null)
        where TControl : ControlBase
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetProperty);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        ArgumentNullException.ThrowIfNull(convert);

        ArgumentOutOfRangeException.ThrowIfNotDefined(mode, nameof(mode), "The binding mode is unknown.");

        if (mode is BindingMode.TwoWay or BindingMode.OneWayToSource)
        {
            ArgumentNullException.ThrowIfNull(convertBack);
        }

        target.VerifyMutable();
        var targetPath = PropertyPath.Create(
            targetProperty,
            requireWritable: mode is BindingMode.OneWay or BindingMode.TwoWay);
        var sourcePath = PropertyPath.Create(
            sourceProperty,
            requireWritable: mode is BindingMode.TwoWay or BindingMode.OneWayToSource);
        var registry = target.GetBindingRegistry();
        var binding = new Binding(
            target,
            targetPath,
            source,
            sourcePath,
            mode,
            value => convert((TSource) value!),
            value => convertBack is null ? default : convertBack((TTarget) value!),
            fallbackValue,
            registry,
            tracksCollection,
            coordinatesItems,
            refreshAfterItems,
            applyIncrementalChange);
        binding.Start();
        return binding;
    }

    [MustDisposeResource]
    private static Binding BindChartSeries<TControl, TModel>(
        TControl target,
        Expression<Func<TControl, IReadOnlyList<ChartSeries>>> targetProperty,
        TModel source,
        Expression<Func<TModel, IReadOnlyList<ChartSeries>?>> sourceProperty)
        where TControl : ControlBase
        where TModel : class =>
        CreateBinding(
            target,
            targetProperty,
            source,
            sourceProperty,
            SnapshotChartSeries,
            convertBack: null,
            BindingMode.OneWay,
            Array.Empty<ChartSeries>(),
            tracksCollection: true,
            coordinatesItems: false,
            refreshAfterItems: false);

    private static ChartSeries[] SnapshotChartSeries(IReadOnlyList<ChartSeries>? series)
    {
        if (series is null)
        {
            return [];
        }

        var snapshot = new ChartSeries[series.Count];

        for (var index = 0; index < snapshot.Length; index++)
        {
            snapshot[index] = series[index];
        }

        return snapshot;
    }

    private static TItem? ItemAt<TItem>(IReadOnlyList<object?> items, int index)
        where TItem : class =>
        (uint) index < (uint) items.Count ? items[index] as TItem : null;

    private static int IndexOf<TItem>(IReadOnlyList<object?> items, TItem? value)
        where TItem : class
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is TItem item && EqualityComparer<TItem>.Default.Equals(item, value))
            {
                return index;
            }
        }

        return -1;
    }

    private static Func<NotifyCollectionChangedEventArgs, bool> CreateListIncrementalCallback(ListView target) =>
        change => change.Action switch
        {
            NotifyCollectionChangedAction.Add
                when change.NewItems is { Count: 1 } && change.NewStartingIndex >= 0 &&
                     change.NewStartingIndex <= target.Items.Count =>
                ApplyListInsert(target, change.NewStartingIndex, change.NewItems[0]),

            NotifyCollectionChangedAction.Remove
                when change.OldItems is { Count: 1 } && change.OldStartingIndex >= 0 &&
                     change.OldStartingIndex < target.Items.Count =>
                ApplyListRemove(target, change.OldStartingIndex),

            NotifyCollectionChangedAction.Replace
                when change.NewItems is { Count: 1 } && change.OldItems is { Count: 1 } &&
                     change.NewStartingIndex >= 0 && change.NewStartingIndex < target.Items.Count =>
                ApplyListReplace(target, change.NewStartingIndex, change.NewItems[0]),

            NotifyCollectionChangedAction.Add => false,
            NotifyCollectionChangedAction.Remove => false,
            NotifyCollectionChangedAction.Replace => false,
            NotifyCollectionChangedAction.Move => false,
            NotifyCollectionChangedAction.Reset => false,
            _ => false
        };

    private static bool ApplyListInsert(ListView target, int index, object? item)
    {
        target.InsertItem(index, item);
        return true;
    }

    private static bool ApplyListRemove(ListView target, int index)
    {
        target.RemoveItem(index);
        return true;
    }

    private static bool ApplyListReplace(ListView target, int index, object? item)
    {
        target.ReplaceItem(index, item);
        return true;
    }

    /// <summary>Binds a selected item two-way to a tree view.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this TreeView target,
        TModel source,
        Expression<Func<TModel, TreeViewItem?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.SelectedItem,
            source,
            sourceProperty,
            BindingMode.TwoWay);

    /// <summary>Binds model JSON text one-way to a JSON view.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this JsonView target,
        TModel source,
        Expression<Func<TModel, string?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Json,
            source,
            sourceProperty,
            static value => value ?? "null",
            convertBack: null,
            BindingMode.OneWay,
            "null");

    /// <summary>Binds a model header text one-way to a tree view item.</summary>
    [MustDisposeResource]
    public static Binding Bind<TModel>(
        this TreeViewItem target,
        TModel source,
        Expression<Func<TModel, string?>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.Header,
            source,
            sourceProperty,
            static value => value ?? string.Empty,
            convertBack: null,
            BindingMode.OneWay,
            string.Empty);

    /// <summary>Binds a model expansion state two-way to a tree view item.</summary>
    [MustDisposeResource]
    public static Binding BindExpanded<TModel>(
        this TreeViewItem target,
        TModel source,
        Expression<Func<TModel, bool>> sourceProperty)
        where TModel : class =>
        BindProperty(
            target,
            static control => control.IsExpanded,
            source,
            sourceProperty,
            BindingMode.TwoWay);

    private static object?[] Snapshot<TItem>(IReadOnlyList<TItem>? items)
    {
        if (items is null)
        {
            return [];
        }

        var snapshot = new object?[items.Count];

        for (var index = 0; index < snapshot.Length; index++)
        {
            snapshot[index] = items[index];
        }

        return snapshot;
    }

}
