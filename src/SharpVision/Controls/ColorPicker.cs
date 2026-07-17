// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Rendering;

/// <summary>Selects one terminal color through capability-adaptive retained controls.</summary>
public sealed class ColorPicker: CompositeControl
{
    private readonly Control _basicRoot;
    private readonly Slider _hue;
    private readonly Control _indexedRoot;
    private readonly Stack _monochromeRoot;
    private readonly Text _status;
    private readonly Control _trueColorRoot;
    private bool _synchronizing;
    private Color _value = Color.Rgb(255, 0, 0);

    #region Construction and public state

    /// <summary>Initializes a retained adaptive picker with an RGB-red detached value.</summary>
    public ColorPicker()
    {
        TabNavigation = TabNavigation.Continue;
        Plane = new ColorPlane();
        _hue = CreateSlider(359);
        RedSlider = CreateSlider(byte.MaxValue);
        GreenSlider = CreateSlider(byte.MaxValue);
        BlueSlider = CreateSlider(byte.MaxValue);
        Preview = new ColorSwatch();
        _status = new Text();
        IndexedGrid = new ColorGrid(256);
        BasicGrid = new ColorGrid(16);
        _trueColorRoot = CreateTrueColorRoot();
        _indexedRoot = IndexedGrid;
        _basicRoot = BasicGrid;
        _monochromeRoot = CreateMonochromeRoot();
        var root = new Overlay
        {
            Children =
            {
                _trueColorRoot,
                _indexedRoot,
                _basicRoot,
                _monochromeRoot,
            },
        };
        InitializeContent(root);
        Subscribe();
        SynchronizeParts();
        ApplyDepth(EffectiveColorDepth);
    }

    /// <summary>Raised after a changed color commits.</summary>
    public event EventHandler<ColorChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the selected terminal color.</summary>
    /// <remarks>
    /// A detached picker preserves the supplied representation. An attached picker immediately
    /// projects it to the active terminal color depth.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Color Value
    {
        get => _value;
        set => _ = Commit(value, normalize: Dispatcher is not null);
    }

    /// <summary>Gets the active terminal color depth inherited from the application.</summary>
    public ColorDepth EffectiveColorDepth => Capabilities.ColorDepth;

    /// <summary>Gets the uppercase RGB readout, or DEFAULT for the terminal default color.</summary>
    internal string HexText => _status.Content;

    /// <summary>Gets the retained true-color saturation/value surface for interaction tests.</summary>
    internal ColorPlane Plane { get; }

    /// <summary>Gets the retained preview surface.</summary>
    internal ColorSwatch Preview { get; }

    /// <summary>Gets the retained red-component slider.</summary>
    internal Slider RedSlider { get; }

    /// <summary>Gets the retained green-component slider.</summary>
    internal Slider GreenSlider { get; }

    /// <summary>Gets the retained blue-component slider.</summary>
    internal Slider BlueSlider { get; }

    /// <summary>Gets the retained indexed-256 palette surface.</summary>
    internal ColorGrid IndexedGrid { get; }

    /// <summary>Gets the retained basic-16 palette surface.</summary>
    internal ColorGrid BasicGrid { get; }

    /// <summary>Gets whether the true-color branch participates in layout.</summary>
    internal bool TrueColorVisible => _trueColorRoot.Visibility == Visibility.Visible;

    /// <summary>Gets whether the indexed-256 branch participates in layout.</summary>
    internal bool IndexedPaletteVisible => _indexedRoot.Visibility == Visibility.Visible;

    #endregion

    #region Lifecycle and capability adaptation

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        ApplyDepth(EffectiveColorDepth);
        _ = Commit(_value, normalize: true);
    }

    /// <inheritdoc/>
    protected override void OnCapabilitiesChanged(
        TerminalCapabilities previous,
        TerminalCapabilities current)
    {
        base.OnCapabilitiesChanged(previous, current);
        ApplyDepth(current.ColorDepth);
        _ = Commit(_value, normalize: true);
    }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        Plane.Changed -= OnPlaneChanged;
        _hue.ValueChanged -= OnHueChanged;
        RedSlider.ValueChanged -= OnRgbChanged;
        GreenSlider.ValueChanged -= OnRgbChanged;
        BlueSlider.ValueChanged -= OnRgbChanged;
        IndexedGrid.Changed -= OnIndexedChanged;
        BasicGrid.Changed -= OnBasicChanged;
        ValueChanged = null;
        base.OnDisposing();
    }

    private void ApplyDepth(ColorDepth depth)
    {
        Debug.Assert(Enum.IsDefined(depth), "Capability profiles validate color depth.");
        _trueColorRoot.Visibility = depth == ColorDepth.TrueColor
            ? Visibility.Visible
            : Visibility.Collapsed;
        _indexedRoot.Visibility = depth == ColorDepth.Indexed256
            ? Visibility.Visible
            : Visibility.Collapsed;
        _basicRoot.Visibility = depth == ColorDepth.Basic16
            ? Visibility.Visible
            : Visibility.Collapsed;
        _monochromeRoot.Visibility = depth == ColorDepth.Monochrome
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    #endregion

    #region Composition

    private Grid CreateTrueColorRoot()
    {
        var root = new Grid
        {
            RowSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        root.Rows.Add(Track.Star(1, minimum: 4));
        root.Rows.Add(Track.Cells(1));
        root.Rows.Add(Track.Auto());
        root.Rows.Add(Track.Cells(1));

        Grid.SetRow(Plane, 0);
        root.Children.Add(Plane);

        var hueOverlay = new Overlay
        {
            Height = Length.Cells(1),
            Children =
            {
                new ColorRamp(),
                _hue,
            },
        };
        Grid.SetRow(hueOverlay, 1);
        root.Children.Add(hueOverlay);

        var components = new Stack
        {
            Spacing = 0,
            Children =
            {
                CreateSliderRow("R", RedSlider),
                CreateSliderRow("G", GreenSlider),
                CreateSliderRow("B", BlueSlider),
            },
        };
        Grid.SetRow(components, 2);
        root.Children.Add(components);

        var status = new Dock
        {
            Children =
            {
                Preview,
                _status,
            },
        };
        Dock.SetSide(Preview, Side.Left);
        Grid.SetRow(status, 3);
        root.Children.Add(status);
        return root;
    }

    private static Stack CreateMonochromeRoot()
    {
        var swatch = new ColorSwatch { Value = Color.Default };
        return new Stack
        {
            IsEnabled = false,
            Spacing = 1,
            Children =
            {
                swatch,
                new Text("Monochrome terminal · default color only"),
            },
        };
    }

    private static Dock CreateSliderRow(string label, Slider slider)
    {
        var text = new Text(label) { Width = Length.Cells(2) };
        Dock.SetSide(text, Side.Left);
        return new Dock
        {
            Height = Length.Cells(1),
            Children =
            {
                text,
                slider,
            },
        };
    }

    private static Slider CreateSlider(int maximum) => new()
    {
        Maximum = maximum,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private void Subscribe()
    {
        Plane.Changed += OnPlaneChanged;
        _hue.ValueChanged += OnHueChanged;
        RedSlider.ValueChanged += OnRgbChanged;
        GreenSlider.ValueChanged += OnRgbChanged;
        BlueSlider.ValueChanged += OnRgbChanged;
        IndexedGrid.Changed += OnIndexedChanged;
        BasicGrid.Changed += OnBasicChanged;
    }

    #endregion

    #region Synchronization

    private bool Commit(Color requested, bool normalize)
    {
        VerifyMutable();
        var committed = normalize ? Normalize(requested, EffectiveColorDepth) : requested;
        var previous = _value;

        if (!SetProperty(ref _value, committed, ChangeImpact.Render, nameof(Value)))
        {
            SynchronizeParts();
            return false;
        }

        SynchronizeParts();
        ValueChanged?.Invoke(this, new ColorChangedEventArgs(previous, committed));
        return true;
    }

    private void SynchronizeParts()
    {
        var rgb = Palette.Resolve(_value);

        if (rgb.Kind != ColorKind.Rgb)
        {
            rgb = Color.Rgb(0, 0, 0);
        }

        ColorMath.ToHsv(rgb, out var hue, out var saturation, out var value);
        _synchronizing = true;

        try
        {
            Plane.SetSelection(hue, saturation, value);
            _hue.Value = hue;
            RedSlider.Value = rgb.Red;
            GreenSlider.Value = rgb.Green;
            BlueSlider.Value = rgb.Blue;
            Preview.Value = _value;
            _status.Content = _value.Kind == ColorKind.Default
                ? "DEFAULT"
                : $"#{rgb.Red:X2}{rgb.Green:X2}{rgb.Blue:X2}";

            if (_value.Kind == ColorKind.Indexed)
            {
                IndexedGrid.SetSelectedIndex(_value.Index);
                BasicGrid.SetSelectedIndex(Math.Min(15, _value.Index));
            }
            else
            {
                IndexedGrid.SetSelectedIndex(Palette.Project(rgb, ColorDepth.Indexed256).Index);
                BasicGrid.SetSelectedIndex(Palette.Project(rgb, ColorDepth.Basic16).Index);
            }
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void OnPlaneChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!_synchronizing)
        {
            _ = Commit(
                ColorMath.FromHsv(Plane.Hue, Plane.Saturation, Plane.Value),
                normalize: true);
        }
    }

    private void OnHueChanged(object? sender, SliderValueChangedEventArgs eventArgs)
    {
        _ = sender;

        if (!_synchronizing)
        {
            Plane.SetSelection(eventArgs.Value, Plane.Saturation, Plane.Value);
            _ = Commit(
                ColorMath.FromHsv(eventArgs.Value, Plane.Saturation, Plane.Value),
                normalize: true);
        }
    }

    private void OnRgbChanged(object? sender, SliderValueChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!_synchronizing)
        {
            _ = Commit(Color.Rgb(RedSlider.Value, GreenSlider.Value, BlueSlider.Value), normalize: true);
        }
    }

    private void OnIndexedChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!_synchronizing)
        {
            _ = Commit(Color.Indexed(IndexedGrid.SelectedIndex), normalize: true);
        }
    }

    private void OnBasicChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!_synchronizing)
        {
            _ = Commit(Color.Indexed(BasicGrid.SelectedIndex), normalize: true);
        }
    }

    private static Color Normalize(Color color, ColorDepth depth) => depth switch
    {
        ColorDepth.TrueColor => Palette.Resolve(color),
        ColorDepth.Indexed256 => Palette.Project(color, ColorDepth.Indexed256),
        ColorDepth.Basic16 => Palette.Project(color, ColorDepth.Basic16),
        ColorDepth.Monochrome => Color.Default,
        _ => throw new ArgumentOutOfRangeException(nameof(depth), depth, "The color depth is unknown."),
    };

    #endregion
}
