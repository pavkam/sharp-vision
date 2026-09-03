// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

using System.Collections.Immutable;

using SharpVision.Terminal.Input;

/// <summary>Provides the state, style, and <see cref="IChartControl"/> plumbing shared by every
/// concrete chart control.</summary>
[PublicAPI]
public abstract class ChartControlBase: ControlBase, IStyled<ChartStyle>, IChartControl
{
    private readonly ChartDataObserver _observer;
    private readonly CallbackTransitionStream _selectionTransitions = new();
    private readonly StyleSlot<ChartStyle> _style;
    private ChartLegendPlacement _legendPlacement;
    private ChartScale _scale;
    private bool _showCategoryLabels;
    private bool _showValueLabels;
    private ChartSelection? _selection;
    private ChartSeries? _selectedSeries;
    private ChartDataPoint? _selectedPoint;

    // Instance-level rather than the usual static readonly field: unlike every other registered
    // Theme value dependency in this codebase, the resolved value here depends on this control's
    // own live series and point data, not on the Theme alone, so it cannot be shared across
    // instances of the type. Registered through SetThemeValueDependency (not ResolveThemeValue) so
    // registration itself never re-walks every series and point - only an actual Theme swap does,
    // by calling the resolver through GetImpact.
    private readonly ThemeValueDependency<ImmutableArray<Color>> _seriesColorsThemeDependency;

    /// <summary>Initializes common chart state with family-specific presentation defaults.</summary>
    /// <param name="scale">The authored scale bounds and zero-inclusion policy.</param>
    /// <param name="legendPlacement">The initial legend policy reported to <see cref="IChartControl"/>
    /// consumers via <see cref="ResolveLegendPlacement"/>.</param>
    /// <param name="showCategoryLabels">The initial category-label policy reported via
    /// <see cref="ResolveShowCategoryLabels"/>.</param>
    /// <param name="showValueLabels">The initial value-label policy reported via
    /// <see cref="ResolveShowValueLabels"/>.</param>
    protected ChartControlBase(
        ChartScale scale,
        ChartLegendPlacement legendPlacement,
        bool showCategoryLabels,
        bool showValueLabels)
    {
        _style = InitializeStyle(ChartStyle.Definition);
        _scale = scale;
        _legendPlacement = legendPlacement;
        _showCategoryLabels = showCategoryLabels;
        _showValueLabels = showValueLabels;
        _observer = new ChartDataObserver(this);
        _seriesColorsThemeDependency = new ThemeValueDependency<ImmutableArray<Color>>(
            ResolveAssignedSeriesColors,
            InvalidationImpact.Render,
            SeriesColorSnapshotComparer.Instance);
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    public ChartStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public ChartStyle ActualStyle => _style.Actual;

    /// <summary>Raised after the selected data point changes or is cleared.</summary>
    public event EventHandler<ChartSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Gets or sets the borrowed observable series source.</summary>
    /// <exception cref="ArgumentNullException">The assigned source is null.</exception>
    /// <exception cref="ArgumentException">The assigned membership violates the concrete chart's series contract.</exception>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    public IReadOnlyList<ChartSeries> Series
    {
        get => _observer.Current;
        set
        {
            ValidateSeriesCore(value);
            VerifyMutable();
            _observer.Replace(value);
        }
    }

    /// <summary>Gets or sets authored scale bounds and zero inclusion.</summary>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    public ChartScale Scale
    {
        get => _scale;
        set => SetChartProperty(ref _scale, value, InvalidationImpact.Render, nameof(Scale));
    }

    /// <summary>Gets or sets the selected data point, or null to clear selection.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The series or point index is outside the current data snapshot.
    /// </exception>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    public ChartSelection? Selection
    {
        get => _selection;
        set
        {
            ValidateSelection(value);
            _ = CommitSelection(value);
        }
    }

    /// <summary>Gets or sets the legend policy exposed by full chart families.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned placement is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    private protected ChartLegendPlacement LegendPlacementCore
    {
        get => _legendPlacement;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The chart legend placement is unknown.");

            SetChartProperty(ref _legendPlacement, value, InvalidationImpact.Measure, nameof(IChartControl.LegendPlacement));
        }
    }

    /// <summary>Gets or sets whether category labels may consume cells in full chart families.</summary>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    private protected bool ShowCategoryLabelsCore
    {
        get => _showCategoryLabels;
        set => SetChartProperty(
            ref _showCategoryLabels, value, InvalidationImpact.Measure, nameof(IChartControl.ShowCategoryLabels));
    }

    /// <summary>Gets or sets whether value labels may consume cells in full chart families.</summary>
    /// <exception cref="InvalidOperationException">The attached chart is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The chart is disposed.</exception>
    private protected bool ShowValueLabelsCore
    {
        get => _showValueLabels;
        set => SetChartProperty(ref _showValueLabels, value, InvalidationImpact.Measure, nameof(IChartControl.ShowValueLabels));
    }

    ControlBase IChartControl.Control => this;

    ChartLegendPlacement IChartControl.LegendPlacement => ResolveLegendPlacement();

    bool IChartControl.ShowCategoryLabels => ResolveShowCategoryLabels();

    bool IChartControl.ShowValueLabels => ResolveShowValueLabels();

    bool IChartControl.ShowZeroAxis => ResolveShowZeroAxis();

    string IChartControl.ValueLabelFormat => ResolveValueLabelFormat();

    ChartSelection? IChartControl.Selection => Selection;

    /// <summary>Gets the legend policy reported to <see cref="IChartControl"/> consumers. The
    /// default forwards to the base-owned authored value; a chart family with a fixed policy (no public
    /// legend surface) overrides this instead of exposing a settable property.</summary>
    protected virtual ChartLegendPlacement ResolveLegendPlacement() => _legendPlacement;

    /// <summary>Gets whether category labels are reported to <see cref="IChartControl"/>
    /// consumers. See <see cref="ResolveLegendPlacement"/> for the override convention.</summary>
    protected virtual bool ResolveShowCategoryLabels() => _showCategoryLabels;

    /// <summary>Gets whether value labels are reported to <see cref="IChartControl"/> consumers.
    /// See <see cref="ResolveLegendPlacement"/> for the override convention.</summary>
    protected virtual bool ResolveShowValueLabels() => _showValueLabels;

    /// <summary>Gets whether a visible zero axis is reported to <see cref="IChartControl"/>
    /// consumers. Compact chart families leave the default disabled.</summary>
    protected virtual bool ResolveShowZeroAxis() => false;

    /// <summary>Gets the invariant numeric format reported to <see cref="IChartControl"/>
    /// consumers.</summary>
    protected virtual string ResolveValueLabelFormat() => "G";

    void IChartControl.ValidateSeries(IReadOnlyList<ChartSeries> series) => ValidateSeriesCore(series);

    /// <summary>Validates a complete prospective series membership snapshot. The default rejects
    /// only a null snapshot; a chart family with a narrower series-count contract overrides this.</summary>
    /// <param name="series">The prospective series membership snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="series"/> is null.</exception>
    protected virtual void ValidateSeriesCore(IReadOnlyList<ChartSeries> series) =>
        ArgumentNullException.ThrowIfNull(series);

    void IChartControl.OnChartDataChanged(InvalidationImpact impact, bool seriesMembershipChanged)
    {
        if (seriesMembershipChanged)
        {
            NotifyPropertyChanged(nameof(Series), impact);
        }
        else
        {
            Invalidate(impact);
        }

        if (seriesMembershipChanged)
        {
            RepairSelection();
        }
    }

    void IChartControl.OnChartPropertyChanged(string propertyName, InvalidationImpact impact) =>
        NotifyPropertyChanged(propertyName, impact);

    Color IChartControl.ResolveSeriesColor(ControlColor color)
    {
        // Registers, but deliberately does not resolve: resolving would re-walk every series and
        // point on every point ChartRenderer draws, turning an O(points) render into O(points^2).
        // The framework only ever calls the dependency's own resolver from GetImpact, which runs
        // once per actual Theme swap - exactly the frequency this snapshot needs.
        SetThemeValueDependency(_seriesColorsThemeDependency, active: true);
        return ResolveColor(color, Theme);
    }

    /// <summary>Resolves every currently assigned series or point color override against one
    /// Theme, skipping series and points that fall back to the style-owned palette - that path is
    /// already covered by <see cref="ChartStyle"/>'s own slot comparison.</summary>
    private ImmutableArray<Color> ResolveAssignedSeriesColors(Theme theme)
    {
        var builder = ImmutableArray.CreateBuilder<Color>();

        foreach (var series in Series)
        {
            if (series.Color is { } seriesColor)
            {
                builder.Add(ResolveColor(seriesColor, theme));
            }

            foreach (var point in series.Points)
            {
                if (point.Color is { } pointColor)
                {
                    builder.Add(ResolveColor(pointColor, theme));
                }
            }
        }

        return builder.ToImmutable();
    }

    // ImmutableArray<T>'s own Equals compares the underlying array reference, not its elements -
    // wrong here, since ResolveAssignedSeriesColors builds a fresh array on every call. This
    // compares the resolved colors themselves, in assignment order.
    private sealed class SeriesColorSnapshotComparer: IEqualityComparer<ImmutableArray<Color>>
    {
        internal static readonly SeriesColorSnapshotComparer Instance = new();

        public bool Equals(ImmutableArray<Color> x, ImmutableArray<Color> y) => x.AsSpan().SequenceEqual(y.AsSpan());

        public int GetHashCode(ImmutableArray<Color> obj)
        {
            var hash = new HashCode();

            foreach (var color in obj.AsSpan())
            {
                hash.Add(color);
            }

            return hash.ToHashCode();
        }
    }

    /// <inheritdoc/>
    protected sealed override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return DefaultSize;
    }

    /// <summary>Gets the fixed size this chart reports regardless of its measure constraint.</summary>
    protected abstract Size DefaultSize { get; }

    /// <summary>Gets whether point categories advance vertically for keyboard input.</summary>
    private protected virtual bool CategoriesAreVertical => false;

    /// <summary>Maps a chart-space pointer cell to the nearest selectable visible data point.</summary>
    /// <param name="position">The absolute terminal cell position.</param>
    /// <param name="selection">Receives the visible data-point selection.</param>
    /// <returns>True when a selectable point owns or is nearest to the supplied plot cell.</returns>
    private protected virtual bool TryHitTestSelection(Point position, out ChartSelection selection) =>
        ChartRenderer.TryHitTestSelection(this, position, out selection);

    private protected void SetChartProperty<T>(ref T field, T value, InvalidationImpact impact, string propertyName)
    {
        VerifyMutable();

        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        NotifyPropertyChanged(propertyName, impact);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            base.OnEvent(eventArgs);
            return;
        }

        if (eventArgs is KeyEventArgs key)
        {
            Handle(key);
        }
        else if (eventArgs is PointerEventArgs pointer)
        {
            Handle(pointer);
        }

        if (!eventArgs.IsHandled)
        {
            base.OnEvent(eventArgs);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            _observer.Dispose();
            SelectionChanged = null;
        }
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        if (!eventArgs.IsKeyDown ||
            !KeyboardModifierPolicy.MatchesCommand(eventArgs.Stroke.Modifiers, Modifiers.None))
        {
            return;
        }

        var code = eventArgs.Stroke.Code;

        if (code == Code.Escape)
        {
            eventArgs.IsHandled = CommitSelection(null);
            return;
        }

        var categoryDelta = CategoriesAreVertical
            ? code == Code.Up ? -1 : code == Code.Down ? 1 : 0
            : code == Code.Left ? -1 : code == Code.Right ? 1 : 0;
        var seriesDelta = CategoriesAreVertical
            ? code == Code.Left ? -1 : code == Code.Right ? 1 : 0
            : code == Code.Up ? -1 : code == Code.Down ? 1 : 0;
        var isEndpoint = code is Code.Home or Code.End;

        if ((categoryDelta == 0 && seriesDelta == 0 && !isEndpoint) ||
            (seriesDelta != 0 && !HasMultiplePopulatedSeries()))
        {
            return;
        }

        if (!TryResolveKeyboardSelection(categoryDelta, seriesDelta, code, out var selection))
        {
            return;
        }

        _ = CommitSelection(selection);
        eventArgs.IsHandled = true;
    }

    [Pure]
    private bool HasMultiplePopulatedSeries()
    {
        var populated = 0;

        foreach (var series in Series)
        {
            if (series.Points.Count == 0)
            {
                continue;
            }

            populated++;

            if (populated == 2)
            {
                return true;
            }
        }

        return false;
    }

    private void Handle(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action != PointerAction.Press ||
            (pointer.Buttons & Buttons.Primary) == 0 ||
            pointer.Cells is not { } cells ||
            !ContentBounds.Contains(cells))
        {
            return;
        }

        var dispatcher = Dispatcher;
        _ = RequestFocus();

        if (!CanContinueAfterFocus(dispatcher) || !TryHitTestSelection(cells, out var selection))
        {
            return;
        }

        _ = CommitSelection(selection);
        eventArgs.IsHandled = true;
    }

    private bool TryResolveKeyboardSelection(
        int categoryDelta,
        int seriesDelta,
        Code code,
        out ChartSelection selection)
    {
        if (_selection is null)
        {
            return TryFindFirstPoint(out selection);
        }

        var current = _selection.Value;
        var seriesIndex = Math.Clamp(current.SeriesIndex + seriesDelta, 0, Series.Count - 1);
        var points = Series[seriesIndex].Points;

        if (points.Count == 0)
        {
            return TryFindNearestPopulatedSeries(seriesIndex, seriesDelta, current.PointIndex, out selection);
        }

        var pointIndex = code == Code.Home
            ? 0
            : code == Code.End
                ? points.Count - 1
                : Math.Clamp(current.PointIndex + categoryDelta, 0, points.Count - 1);
        selection = new ChartSelection(seriesIndex, pointIndex);
        return true;
    }

    private bool TryFindFirstPoint(out ChartSelection selection)
    {
        for (var seriesIndex = 0; seriesIndex < Series.Count; seriesIndex++)
        {
            if (Series[seriesIndex].Points.Count == 0)
            {
                continue;
            }

            selection = new ChartSelection(seriesIndex, 0);
            return true;
        }

        selection = default;
        return false;
    }

    private bool TryFindNearestPopulatedSeries(
        int start,
        int direction,
        int pointIndex,
        out ChartSelection selection)
    {
        if (direction == 0)
        {
            selection = default;
            return false;
        }

        for (var index = start; index >= 0 && index < Series.Count; index += direction)
        {
            var count = Series[index].Points.Count;

            if (count == 0)
            {
                continue;
            }

            selection = new ChartSelection(index, Math.Min(pointIndex, count - 1));
            return true;
        }

        selection = default;
        return false;
    }

    private void ValidateSelection(ChartSelection? value)
    {
        if (value is not { } selection)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            selection.SeriesIndex,
            Series.Count,
            nameof(value));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            selection.PointIndex,
            Series[selection.SeriesIndex].Points.Count,
            nameof(value));
    }

    private bool CommitSelection(ChartSelection? value)
    {
        VerifyMutable();

        if (_selection == value)
        {
            return false;
        }

        var previous = _selection;
        _selection = value;
        _selectedSeries = value is { } selection ? Series[selection.SeriesIndex] : null;
        _selectedPoint = value is { } selected ? Series[selected.SeriesIndex].Points[selected.PointIndex] : null;
        var transition = BeginPropertyTransition(
            _selectionTransitions,
            InvalidationImpact.Render,
            nameof(Selection));
        transition.PublishCurrent(
            SelectionChanged,
            this,
            new ChartSelectionChangedEventArgs(previous, value));
        transition.ThrowIfFailed();
        return true;
    }

    private void RepairSelection()
    {
        if (_selection is null || _selectedSeries is null || _selectedPoint is null)
        {
            return;
        }

        for (var seriesIndex = 0; seriesIndex < Series.Count; seriesIndex++)
        {
            var series = Series[seriesIndex];

            if (!ReferenceEquals(series, _selectedSeries))
            {
                continue;
            }

            for (var pointIndex = 0; pointIndex < series.Points.Count; pointIndex++)
            {
                if (ReferenceEquals(series.Points[pointIndex], _selectedPoint))
                {
                    _ = CommitSelection(new ChartSelection(seriesIndex, pointIndex));
                    return;
                }
            }
        }

        _ = CommitSelection(null);
    }
}
