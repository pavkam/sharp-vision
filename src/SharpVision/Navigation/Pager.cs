// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using SharpVision.Terminal.Input;

/// <summary>Navigates one zero-based page index inside a finite page range.</summary>
[PublicAPI]
public sealed class Pager: ControlBase, IStyled<PagerStyle>
{
    private const int _maximumMaterializedWindow = 4096;
    private int _pageCount;
    private int _pageIndex = -1;
    private ulong _layoutGeneration;
    private readonly CallbackTransitionStream _pageTransitions = new();
    private readonly PressBehavior _press;
    private PagerLayoutTarget? _pressedTarget;
    private ulong _pressedLayoutGeneration;
    private readonly StyleSlot<PagerStyle> _style;

    /// <summary>Initializes an empty, focusable Pager outside the Tab sequence.</summary>
    public Pager()
    {
        _style = InitializeStyle(PagerStyle.Definition);
        _press = new PressBehavior(
            () => _pressedTarget?.Bounds ?? default,
            IsPressedTargetCurrent,
            IsPressedTargetCurrent,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            ActivatePressedTarget,
            () => Capabilities.KeyReleaseEvents.Authoritative);
        RegisterLifecycleParticipant(_press);
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Gets or sets the number of pages; zero establishes an empty PageIndex of -1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int PageCount
    {
        get => _pageCount;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            CommitPageCount(value);
        }
    }

    /// <summary>Gets or sets the zero-based current page, or -1 while PageCount is zero.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value does not satisfy the current page-range invariant.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int PageIndex
    {
        get => _pageIndex;
        set
        {
            ValidatePageIndex(value);
            _ = CommitPageIndex(value, ActivationCause.Programmatic);
        }
    }

    /// <summary>Gets or sets the positive centered page-window limit.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than one.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int MaximumVisiblePages
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);

            if (value != field)
            {
                CancelPointerInteraction();
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = 5;

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public PagerStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public PagerStyle ActualStyle => _style.Actual;

    /// <summary>Gets whether this Pager currently participates in Tab traversal.</summary>
    public override bool CanTabStop => base.CanTabStop && PageCount > 1;

    /// <summary>Raised after a changed page index commits.</summary>
    public event EventHandler<PageChangedEventArgs>? PageChanged;

    /// <summary>Gets the committed snapshot used to prove render and hit-test identity in tests.</summary>
    internal PagerLayout LayoutSnapshot { get; private set; } = PagerLayout.Empty;

    /// <summary>Changes to one validated zero-based page index.</summary>
    /// <param name="pageIndex">The requested zero-based page index.</param>
    /// <returns>True when a changed page committed; otherwise false.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> does not satisfy the current page-range invariant.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ChangePage(int pageIndex)
    {
        ValidatePageIndex(pageIndex);
        return CommitPageIndex(pageIndex, ActivationCause.Programmatic);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        if (PageCount == 0)
        {
            return default;
        }

        var width = constraint.Width;
        var materialized = CreateLayout(
            width,
            new Rect(0, 0, width ?? int.MaxValue, 1),
            generation: 0);
        return materialized.DesiredSize;
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        CancelPointerInteraction();
        _layoutGeneration++;
        LayoutSnapshot = CreateLayout(bounds.Width, bounds, _layoutGeneration);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        var inherited = ResolvedStyle;

        if (this.HasOpaqueFill(GetAppearanceState()))
        {
            canvas.Clear(bounds, inherited);
        }

        foreach (var target in LayoutSnapshot.Targets)
        {
            var foreground = target.IsCurrent
                ? ResolveColor(ActualStyle.CurrentPageColor)
                : target.IsEnabled
                    ? inherited.Foreground
                    : ResolveColor(SemanticColor.Muted);
            var style = inherited.WithForeground(foreground);
            _ = canvas.Draw(
                target.Text.AsSpan(),
                new Point(target.Bounds.X, target.Bounds.Y),
                style,
                background: BackgroundMode.Transparent);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (EffectiveIsEnabled && EffectiveIsVisible && PageCount > 1)
        {
            if (eventArgs is KeyEventArgs key)
            {
                HandleKey(key);
            }
            else if (eventArgs is PointerEventArgs pointer)
            {
                HandlePointer(pointer);
            }
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
        _pressedTarget = null;
        _pressedLayoutGeneration = 0;

        if (reason == ReleaseReason.Disposed)
        {
            PageChanged = null;
        }
    }

    private void CommitPageCount(int pageCount)
    {
        VerifyMutable();

        if (_pageCount == pageCount)
        {
            return;
        }

        CancelPointerInteraction();
        var previousPageIndex = _pageIndex;
        var previousCanTabStop = CanTabStop;
        var pageIndex = pageCount == 0
            ? -1
            : _pageCount == 0
                ? 0
                : Math.Min(_pageIndex, pageCount - 1);

        _pageCount = pageCount;
        _pageIndex = pageIndex;
        var currentCanTabStop = CanTabStop;
        var transition = BeginCallbackPropertyTransition(
            _pageTransitions,
            InvalidationImpact.Measure,
            nameof(PageCount));

        if (transition.IsCurrent && previousPageIndex != pageIndex)
        {
            PublishTransitionProperty(
                ref transition,
                nameof(PageIndex),
                InvalidationImpact.Measure);
        }

        if (transition.IsCurrent && previousCanTabStop != currentCanTabStop)
        {
            PublishTransitionProperty(
                ref transition,
                nameof(CanTabStop),
                InvalidationImpact.None);
        }

        if (transition.IsCurrent && previousPageIndex != pageIndex)
        {
            transition.PublishCurrent(
                PageChanged,
                this,
                new PageChangedEventArgs(
                    previousPageIndex,
                    pageIndex,
                    ActivationCause.Programmatic));
        }

        transition.ThrowIfFailed();
    }

    private bool CommitPageIndex(int pageIndex, ActivationCause cause)
    {
        CancelPointerInteraction();
        var previousPageIndex = _pageIndex;

        if (!SetTransitionProperty(
                ref _pageIndex,
                pageIndex,
                InvalidationImpact.Measure,
                _pageTransitions,
                out var transition,
                nameof(PageIndex)))
        {
            return false;
        }

        transition.PublishCurrent(
            PageChanged,
            this,
            new PageChangedEventArgs(previousPageIndex, pageIndex, cause));
        transition.ThrowIfFailed();
        return true;
    }

    private void ValidatePageIndex(int pageIndex)
    {
        if ((PageCount == 0 && pageIndex != -1) ||
            (PageCount > 0 && (pageIndex < 0 || pageIndex >= PageCount)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                pageIndex,
                "PageIndex must be -1 for an empty range or inside the current page range.");
        }
    }

    private void HandleKey(KeyEventArgs eventArgs)
    {
        if (!eventArgs.IsKeyDown ||
            !KeyboardModifierPolicy.IsScalarNavigationEligible(eventArgs.Stroke.Modifiers))
        {
            return;
        }

        var code = eventArgs.Stroke.Code;
        var target = PageIndex;

        if (code is Code.Left or Code.Up or Code.PageUp)
        {
            target = Math.Max(0, PageIndex - 1);
        }
        else if (code is Code.Right or Code.Down or Code.PageDown)
        {
            target = Math.Min(PageCount - 1, PageIndex + 1);
        }
        else if (code == Code.Home)
        {
            target = 0;
        }
        else if (code == Code.End)
        {
            target = PageCount - 1;
        }

        if (target != PageIndex)
        {
            eventArgs.IsHandled = CommitPageIndex(target, ActivationCause.Keyboard);
        }
    }

    private void HandlePointer(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action == PointerAction.Press)
        {
            if ((pointer.Buttons & Buttons.Primary) == 0 ||
                pointer.Cells is not { } cells ||
                !TryGetInteractiveTarget(cells, out var target))
            {
                return;
            }

            _pressedTarget = target;
            _pressedLayoutGeneration = LayoutSnapshot.Generation;
        }

        _press.Handle(eventArgs);

        if (pointer.Action is PointerAction.Release or PointerAction.Leave)
        {
            _pressedTarget = null;
            _pressedLayoutGeneration = 0;
        }
    }

    private bool TryGetInteractiveTarget(Point cells, out PagerLayoutTarget target)
    {
        foreach (var candidate in LayoutSnapshot.Targets)
        {
            if (candidate.IsEnabled && candidate.Bounds.Contains(cells))
            {
                target = candidate;
                return true;
            }
        }

        target = default;
        return false;
    }

    private bool IsPressedTargetCurrent() =>
        !IsDisposed &&
        EffectiveIsEnabled &&
        EffectiveIsVisible &&
        PageCount > 1 &&
        _pressedTarget is { } pressed &&
        _pressedLayoutGeneration == LayoutSnapshot.Generation &&
        LayoutSnapshot.Targets.Any(candidate =>
            candidate.IsEnabled &&
            candidate.Kind == pressed.Kind &&
            candidate.PageIndex == pressed.PageIndex &&
            candidate.Bounds == pressed.Bounds);

    private void ActivatePressedTarget(ActivationCause cause)
    {
        if (_pressedTarget is not { } target || !IsPressedTargetCurrent())
        {
            return;
        }

        _ = CommitPageIndex(target.PageIndex, cause);
    }

    private void CancelPointerInteraction()
    {
        if (_pressedTarget is null)
        {
            return;
        }

        _press.FocusChanged(focused: false);
        _pressedTarget = null;
        _pressedLayoutGeneration = 0;
    }

    private PagerLayout CreateLayout(int? availableWidth, Rect bounds, ulong generation)
    {
        if (PageCount == 0 || bounds.Height == 0 || availableWidth == 0)
        {
            return new PagerLayout([], new Size(0, PageCount == 0 ? 0 : 1), generation);
        }

        var currentText = FormatPage(PageIndex);

        if (availableWidth is { } finite && currentText.Length > finite)
        {
            return new PagerLayout([], new Size(0, 1), generation);
        }

        List<(long Order, PagerLayoutTarget Target)> selected = [];
        AddCandidate(
            selected,
            NumberTarget(PageIndex, currentText),
            NumberOrder(PageIndex),
            availableWidth);

        if (PageCount > 1)
        {
            AddNumberCandidate(selected, 0, availableWidth);
            AddNumberCandidate(selected, PageCount - 1, availableWidth);

            foreach (var pageIndex in WindowCandidates(availableWidth))
            {
                AddNumberCandidate(selected, pageIndex, availableWidth);
            }

            AddOmissionCandidates(selected, availableWidth);
            AddNavigationCandidates(selected, availableWidth);
        }

        selected.Sort(static (left, right) => left.Order.CompareTo(right.Order));
        var x = bounds.X;
        var targets = new PagerLayoutTarget[selected.Count];

        for (var index = 0; index < selected.Count; index++)
        {
            if (index > 0)
            {
                x = x.SaturatingAdd(1);
            }

            var target = selected[index].Target;
            var arranged = new Rect(x, bounds.Y, target.CellWidth, 1);
            targets[index] = target.At(arranged);
            x = x.SaturatingAdd(target.CellWidth);
        }

        var width = TotalWidth(selected);

        if (availableWidth is null && WindowCandidateCount() > _maximumMaterializedWindow)
        {
            width = int.MaxValue;
        }

        return new PagerLayout(targets, new Size(width, 1), generation);
    }

    private IEnumerable<int> WindowCandidates(int? availableWidth)
    {
        var remaining = WindowCandidateCount();
        var materializationLimit = availableWidth is { } finite
            ? Math.Min(remaining, finite)
            : Math.Min(remaining, _maximumMaterializedWindow);
        var produced = 0;

        for (var distance = 1; produced < materializationLimit; distance++)
        {
            var found = false;
            var left = PageIndex - distance;

            if (left > 0 && left < PageCount - 1)
            {
                yield return left;
                produced++;
                found = true;

                if (produced == materializationLimit)
                {
                    yield break;
                }
            }

            var right = (long) PageIndex + distance;

            if (right > 0 && right < PageCount - 1)
            {
                yield return (int) right;
                produced++;
                found = true;
            }

            if (!found && left <= 0 && right >= PageCount - 1L)
            {
                yield break;
            }
        }
    }

    private int WindowCandidateCount()
    {
        var interior = Math.Max(0, PageCount - 2);

        if (PageIndex > 0 && PageIndex < PageCount - 1)
        {
            interior--;
        }

        return Math.Min(MaximumVisiblePages, interior);
    }

    private void AddNumberCandidate(
        List<(long Order, PagerLayoutTarget Target)> selected,
        int pageIndex,
        int? availableWidth)
    {
        if (selected.Any(entry =>
                entry.Target.Kind == PagerTargetKind.Number &&
                entry.Target.PageIndex == pageIndex))
        {
            return;
        }

        var text = FormatPage(pageIndex);
        AddCandidate(selected, NumberTarget(pageIndex, text), NumberOrder(pageIndex), availableWidth);
    }

    private void AddOmissionCandidates(
        List<(long Order, PagerLayoutTarget Target)> selected,
        int? availableWidth)
    {
        if (!TryResolveGlyph(ActualStyle.OmittedPagesGlyph, out var glyph))
        {
            return;
        }

        var numericPages = selected
            .Where(static entry => entry.Target.Kind == PagerTargetKind.Number)
            .Select(static entry => entry.Target.PageIndex)
            .Order()
            .ToArray();

        for (var index = 1; index < numericPages.Length; index++)
        {
            var previous = numericPages[index - 1];
            var current = numericPages[index];

            if (current - previous <= 1)
            {
                continue;
            }

            var target = new PagerLayoutTarget(
                PagerTargetKind.Omitted,
                -1,
                glyph.ToString(),
                1,
                default,
                isEnabled: false,
                isCurrent: false);
            AddCandidate(
                selected,
                target,
                NumberOrder(previous) + 1,
                availableWidth);
        }
    }

    private void AddNavigationCandidates(
        List<(long Order, PagerLayoutTarget Target)> selected,
        int? availableWidth)
    {
        AddNavigationCandidate(
            selected,
            PagerTargetKind.Previous,
            Math.Max(0, PageIndex - 1),
            ActualStyle.PreviousPageGlyph,
            order: 1,
            isEnabled: PageIndex > 0,
            availableWidth);
        AddNavigationCandidate(
            selected,
            PagerTargetKind.Next,
            Math.Min(PageCount - 1, PageIndex + 1),
            ActualStyle.NextPageGlyph,
            order: long.MaxValue - 1,
            isEnabled: PageIndex < PageCount - 1,
            availableWidth);
        AddNavigationCandidate(
            selected,
            PagerTargetKind.First,
            0,
            ActualStyle.FirstPageGlyph,
            order: 0,
            isEnabled: PageIndex > 0,
            availableWidth);
        AddNavigationCandidate(
            selected,
            PagerTargetKind.Last,
            PageCount - 1,
            ActualStyle.LastPageGlyph,
            order: long.MaxValue,
            isEnabled: PageIndex < PageCount - 1,
            availableWidth);
    }

    private void AddNavigationCandidate(
        List<(long Order, PagerLayoutTarget Target)> selected,
        PagerTargetKind kind,
        int pageIndex,
        ControlGlyph controlGlyph,
        long order,
        bool isEnabled,
        int? availableWidth)
    {
        if (!TryResolveGlyph(controlGlyph, out var glyph))
        {
            return;
        }

        var target = new PagerLayoutTarget(
            kind,
            pageIndex,
            glyph.ToString(),
            1,
            default,
            isEnabled,
            isCurrent: false);
        AddCandidate(selected, target, order, availableWidth);
    }

    private static void AddCandidate(
        List<(long Order, PagerLayoutTarget Target)> selected,
        PagerLayoutTarget target,
        long order,
        int? availableWidth)
    {
        selected.Add((order, target));

        if (availableWidth is { } finite && TotalWidth(selected) > finite)
        {
            selected.RemoveAt(selected.Count - 1);
        }
    }

    private PagerLayoutTarget NumberTarget(int pageIndex, string text) => new(
        PagerTargetKind.Number,
        pageIndex,
        text,
        text.Length,
        default,
        isEnabled: PageCount > 1 && pageIndex != PageIndex,
        isCurrent: pageIndex == PageIndex);

    private bool TryResolveGlyph(ControlGlyph glyph, out Rune resolved)
    {
        if (IsOneCell(glyph.Value))
        {
            resolved = glyph.Value;
            return true;
        }

        if (IsOneCell(glyph.Fallback))
        {
            resolved = glyph.Fallback;
            return true;
        }

        resolved = default;
        return false;
    }

    private bool IsOneCell(Rune rune)
    {
        Span<char> buffer = stackalloc char[2];
        var length = rune.EncodeToUtf16(buffer);
        return Terminal.Unicode.Width.Measure(
            buffer[..length],
            CellPolicy.AmbiguousWidth) is { Cells: 1, Controls: 0 };
    }

    private static int TotalWidth(List<(long Order, PagerLayoutTarget Target)> entries)
    {
        if (entries.Count == 0)
        {
            return 0;
        }

        var width = entries.Count - 1L;

        foreach (var (_, target) in entries)
        {
            width += target.CellWidth;
        }

        return width >= int.MaxValue ? int.MaxValue : (int) width;
    }

    private static string FormatPage(int pageIndex) =>
        ((long) pageIndex + 1).ToString(CultureInfo.InvariantCulture);

    private static long NumberOrder(int pageIndex) => 2L + (pageIndex * 2L);
}
