// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Collections.ObjectModel;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies the public scalar data-binding lifetime and update contract.</summary>
public sealed partial class BindingTests
{
    /// <summary>Verifies a Text binding initializes and observes one notifying model property.</summary>
    [Fact]
    public void Bind_WhenSourceChanges_UpdatesTextOnce()
    {
        // Arrange
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText("unused");

        // Act
        using var binding = target.Bind(model, source => source.Name);
        model.Name = "After";

        // Assert
        target.Content.ShouldBe("After");
        binding.Mode.ShouldBe(BindingMode.OneWay);
        binding.Target.ShouldBeSameAs(target);
        binding.IsDisposed.ShouldBeFalse();
    }

    /// <summary>Verifies explicit disposal stops later source notifications.</summary>
    [Fact]
    public void Dispose_WhenCalled_StopsSourceUpdates()
    {
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText();
        var binding = target.Bind(model, source => source.Name);

        binding.Dispose();
        model.Name = "After";

        binding.IsDisposed.ShouldBeTrue();
        target.Content.ShouldBe("Before");
    }

    /// <summary>Verifies plain objects still participate in deterministic initial synchronization.</summary>
    [Fact]
    public void Bind_WhenSourceDoesNotNotify_InitializesOnce()
    {
        var model = new { Name = "Plain" };
        var target = new ControlText();
        using var binding = target.Bind(model, source => source.Name);

        target.Content.ShouldBe("Plain");
    }

    /// <summary>Verifies target disposal owns and releases its live bindings.</summary>
    [Fact]
    public void Dispose_WhenTargetIsDisposed_DisposesBindingAndStopsUpdates()
    {
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText();
        var binding = target.Bind(model, source => source.Name);

        target.Dispose();
        model.Name = "After";

        binding.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies display and editable text adapters project null as empty text.</summary>
    [Fact]
    public void Bind_WhenTextSourceIsNull_ProjectsEmptyText()
    {
        var model = new BindingModel();
        var display = new ControlText("stale");
        var input = new TextInput { Text = "stale" };
        using var displayBinding = display.Bind(model, source => source.Name);
        using var inputBinding = input.Bind(model, source => source.Name);

        display.Content.ShouldBeEmpty();
        input.Text.ShouldBeEmpty();
    }

    /// <summary>Verifies CheckBox maps its nullable state two-way.</summary>
    [Fact]
    public void Bind_WhenCheckBoxChanges_SynchronizesNullableState()
    {
        var model = new BindingModel { Checked = true };
        var target = new CheckBox { ThreeState = true };
        using var binding = target.Bind(model, source => source.Checked);

        target.IsChecked.ShouldBe(true);

        target.IsChecked = null;

        model.Checked.ShouldBeNull();
    }

    /// <summary>Verifies Slider maps its bounded integer two-way.</summary>
    [Fact]
    public void Bind_WhenSliderChanges_SynchronizesValue()
    {
        var model = new BindingModel { Number = 25 };
        var target = new Slider { Maximum = 50 };
        using var binding = target.Bind(model, source => source.Number);

        target.Value.ShouldBe(25);

        target.Value = 40;

        model.Number.ShouldBe(40);
    }

    /// <summary>Verifies RadioButton maps Boolean state two-way.</summary>
    [Fact]
    public void Bind_WhenRadioButtonChanges_SynchronizesState()
    {
        var model = new BindingModel { IsEnabled = true };
        var target = new RadioButton();
        using var binding = target.Bind(model, source => source.IsEnabled);

        target.IsChecked.ShouldBeTrue();

        target.IsChecked = false;

        model.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies ScrollBar maps its bounded integer two-way.</summary>
    [Fact]
    public void Bind_WhenScrollBarChanges_SynchronizesValue()
    {
        var model = new BindingModel { Number = 20 };
        var target = new ScrollBar { Maximum = 50 };
        using var binding = target.Bind(model, source => source.Number);

        target.Value.ShouldBe(20);

        target.Value = 30;

        model.Number.ShouldBe(30);
    }

    /// <summary>Verifies ProgressBar maps its display value one-way.</summary>
    [Fact]
    public void Bind_WhenProgressChanges_UpdatesDisplayOnly()
    {
        var model = new BindingModel { Progress = 25.5 };
        var target = new ProgressBar { Maximum = 100 };
        using var binding = target.Bind(model, source => source.Progress);

        target.Value.ShouldBe(25.5);
        binding.Mode.ShouldBe(BindingMode.OneWay);
    }

    /// <summary>Verifies ColorPicker maps a typed color two-way while detached.</summary>
    [Fact]
    public void Bind_WhenColorPickerChanges_SynchronizesColor()
    {
        var blue = Color.Rgb(0, 0, 255);
        var red = Color.Rgb(255, 0, 0);
        var model = new BindingModel { Color = blue };
        var target = new ColorPicker();
        using var binding = target.Bind(model, source => source.Color);

        target.Value.ShouldBe(blue);

        target.Value = red;

        model.Color.ShouldBe(red);
    }

    /// <summary>Verifies ListView maps selected index two-way.</summary>
    [Fact]
    public void Bind_WhenListSelectionChanges_SynchronizesIndex()
    {
        var model = new BindingModel { Number = 1 };
        var target = new UiListView { Items = ["A", "B", "C"] };
        using var binding = target.Bind(model, source => source.Number);

        target.SelectedIndex.ShouldBe(1);

        target.SelectedIndex = 2;

        model.Number.ShouldBe(2);
    }

    /// <summary>Verifies ComboBox maps selected index two-way.</summary>
    [Fact]
    public void Bind_WhenComboSelectionChanges_SynchronizesIndex()
    {
        var model = new BindingModel { Number = 1 };
        var target = new ComboBox { Items = ["A", "B"] };
        using var binding = target.Bind(model, source => source.Number);

        target.SelectedIndex.ShouldBe(1);

        target.SelectedIndex = 0;

        model.Number.ShouldBe(0);
    }

    /// <summary>Verifies TabControl maps selected index two-way.</summary>
    [Fact]
    public void Bind_WhenTabSelectionChanges_SynchronizesIndex()
    {
        var model = new BindingModel { Number = 1 };
        var target = new TabControl();
        target.Items.Add(new TabItem { HeaderText = "A" });
        target.Items.Add(new TabItem { HeaderText = "B" });
        using var binding = target.Bind(model, source => source.Number);

        target.SelectedIndex.ShouldBe(1);

        target.SelectedIndex = 0;

        model.Number.ShouldBe(0);
    }

    /// <summary>Verifies Menu maps selected index two-way.</summary>
    [Fact]
    public void Bind_WhenMenuSelectionChanges_SynchronizesIndex()
    {
        var model = new BindingModel { Number = 0 };
        var target = new Menu { Items = { new MenuItem(), new MenuItem() } };
        using var binding = target.Bind(model, source => source.Number);

        target.SelectedIndex.ShouldBe(0);

        target.SelectedIndex = 1;

        model.Number.ShouldBe(1);
    }

    /// <summary>Verifies DateInput maps its nullable date two-way.</summary>
    [Fact]
    public void Bind_WhenDateInputChanges_SynchronizesValue()
    {
        var initial = new DateOnly(2024, 1, 1);
        var updated = new DateOnly(2024, 6, 15);
        var model = new BindingModel { DateValue = initial };
        var target = new DateInput();
        using var binding = target.Bind(model, source => source.DateValue);

        target.Value.ShouldBe(initial);

        target.Value = updated;

        model.DateValue.ShouldBe(updated);
    }

    /// <summary>Verifies TimeInput maps its nullable time two-way.</summary>
    [Fact]
    public void Bind_WhenTimeInputChanges_SynchronizesValue()
    {
        var initial = new TimeOnly(9, 30);
        var updated = new TimeOnly(14, 45);
        var model = new BindingModel { TimeValue = initial };
        var target = new TimeInput();
        using var binding = target.Bind(model, source => source.TimeValue);

        target.Value.ShouldBe(initial);

        target.Value = updated;

        model.TimeValue.ShouldBe(updated);
    }

    /// <summary>Verifies DateTimeInput maps its nullable date-time two-way.</summary>
    [Fact]
    public void Bind_WhenDateTimeInputChanges_SynchronizesValue()
    {
        var initial = new DateTime(2024, 1, 1, 9, 0, 0);
        var updated = new DateTime(2024, 6, 15, 14, 45, 0);
        var model = new BindingModel { DateTimeValue = initial };
        var target = new DateTimeInput();
        using var binding = target.Bind(model, source => source.DateTimeValue);

        target.Value.ShouldBe(initial);

        target.Value = updated;

        model.DateTimeValue.ShouldBe(updated);
    }

    /// <summary>Verifies JsonView maps its document text one-way.</summary>
    [Fact]
    public void Bind_WhenJsonModelChanges_UpdatesDisplayOnly()
    {
        var model = new BindingModel { Json = /*lang=json,strict*/ "{\"a\":1}" };
        var target = new JsonView();
        using var binding = target.Bind(model, source => source.Json);

        target.Json.ShouldBe(/*lang=json,strict*/ "{\"a\":1}");
        binding.Mode.ShouldBe(BindingMode.OneWay);
    }

    /// <summary>Verifies TreeView maps its selected item two-way by reference.</summary>
    [Fact]
    public void Bind_WhenTreeViewSelectionChanges_SynchronizesItem()
    {
        var first = new TreeViewItem("First");
        var second = new TreeViewItem("Second");
        var target = new TreeView { Items = { first, second } };
        var model = new BindingModel { SelectedTreeItem = first };
        using var binding = target.Bind(model, source => source.SelectedTreeItem);

        target.SelectedItem.ShouldBeSameAs(first);

        target.SelectItem(second);

        model.SelectedTreeItem.ShouldBeSameAs(second);
    }

    /// <summary>Verifies TreeViewItem maps its header text one-way.</summary>
    [Fact]
    public void TreeViewItemBind_WhenSourceChanges_SyncsHeader()
    {
        var model = new BindingModel { Name = "Documents" };
        var target = new TreeViewItem();
        using var binding = target.Bind(model, source => source.Name);

        target.Header.ShouldBe("Documents");

        model.Name = "Reports";

        target.Header.ShouldBe("Reports");
        binding.Mode.ShouldBe(BindingMode.OneWay);
    }

    /// <summary>Verifies a tree view item expansion binding initializes from the model.</summary>
    [Fact]
    public void TreeViewItemBindExpanded_WhenSourceChanges_SyncsIsExpanded()
    {
        var model = new BindingModel { IsExpanded = false };
        var item = new TreeViewItem("Item");
        using var binding = item.BindExpanded(model, source => source.IsExpanded);

        item.IsExpanded.ShouldBeFalse();
        binding.Mode.ShouldBe(BindingMode.TwoWay);
    }

    /// <summary>Verifies a tree view item expansion toggle propagates back to the model.</summary>
    [Fact]
    public void TreeViewItemBindExpanded_WhenTargetChanges_UpdatesModel()
    {
        var model = new BindingModel { IsExpanded = true };
        var item = new TreeViewItem("Item");
        using var binding = item.BindExpanded(model, source => source.IsExpanded);

        item.IsExpanded = false;

        model.IsExpanded.ShouldBeFalse();
    }

    /// <summary>Verifies Button command and parameter replacements remain live.</summary>
    [Fact]
    public void BindCommand_WhenModelReplacesValues_UsesLatestCommandAndParameter()
    {
        var first = new BindingCommand();
        var second = new BindingCommand();
        var parameter = new object();
        var model = new BindingModel { Command = first };
        var target = new Button();
        using var commandBinding = target.BindCommand(model, source => source.Command);
        using var parameterBinding = target.BindCommandParameter(model, source => source.Parameter);

        model.Command = second;
        model.Parameter = parameter;
        target.PerformClick();

        first.ExecutedParameter.ShouldBeNull();
        second.ExecutedParameter.ShouldBeSameAs(parameter);
    }

    /// <summary>Verifies BindCommand/BindCommandParameter/Bind(caption) surface on every InputBase at
    /// compile time but still throw the same "capability not enabled" exception the plain property
    /// setter throws, for a control that never called EnableCommand/EnableCaption.</summary>
    [Fact]
    public void BindCommand_WhenTargetNeverEnabledTheCommandCapability_ThrowsInvalidOperationException()
    {
        var model = new BindingModel { Command = new BindingCommand(), Name = "Caption" };
        var target = new ComboBox();

        _ = Should.Throw<InvalidOperationException>(() => target.BindCommand(model, source => source.Command));
        _ = Should.Throw<InvalidOperationException>(
            () => target.BindCommandParameter(model, source => source.Parameter));
        _ = Should.Throw<InvalidOperationException>(() => target.Bind(model, source => source.Name));
    }

    /// <summary>Verifies InputBase.Bind maps optional model text one-way to a caption-enabled
    /// control's Text.</summary>
    [Fact]
    public void InputBaseBind_WhenSourceChanges_SyncsCaption()
    {
        var model = new BindingModel { Name = "Caption" };
        var target = new Button();
        using var binding = target.Bind(model, source => source.Name);

        target.Text.ShouldBe("Caption");

        model.Name = "Updated";

        target.Text.ShouldBe("Updated");
        binding.Mode.ShouldBe(BindingMode.OneWay);
    }

    /// <summary>Verifies InputBase.Bind projects a null model caption as empty text.</summary>
    [Fact]
    public void InputBaseBind_WhenSourceIsNull_ProjectsEmptyCaption()
    {
        var model = new BindingModel { Name = "Caption" };
        var target = new Button();
        using var binding = target.Bind(model, source => source.Name);

        model.Name = null;

        target.Text.ShouldBeEmpty();
    }

    /// <summary>Verifies membership changes project the latest finite snapshot.</summary>
    [Fact]
    public void BindItems_WhenCollectionChanges_UpdatesListSnapshot()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var source = new ObservableCollection<BindingItem> { first };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        source.Add(second);

        target.Items.ShouldBe(new object?[] { first, second });

        _ = source.Remove(first);

        target.Items.ShouldBe(new object?[] { second });
    }

    /// <summary>Verifies collection replacement detaches the old collection.</summary>
    [Fact]
    public void BindItems_WhenCollectionIsReplaced_ObservesOnlyReplacement()
    {
        var oldSource = new ObservableCollection<BindingItem> { new("Old") };
        var replacement = new ObservableCollection<BindingItem> { new("New") };
        var model = new BindingModel { Items = oldSource };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        model.Items = replacement;
        oldSource.Add(new BindingItem("Ignored"));
        replacement.Add(new BindingItem("Current"));

        target.Items.Select(static item => item!.ToString()).ShouldBe(["New", "Current"]);
    }

    /// <summary>Verifies null and reset collections project to an empty snapshot.</summary>
    [Fact]
    public void BindItems_WhenSourceIsNullOrReset_ProjectsEmptySnapshot()
    {
        var model = new BindingModel();
        var target = new UiListView { Items = ["stale"] };
        using var binding = target.BindItems(model, value => value.Items);

        target.Items.ShouldBeEmpty();

        var source = new ObservableCollection<BindingItem> { new("A"), new("B") };
        model.Items = source;
        source.Clear();

        target.Items.ShouldBeEmpty();
    }

    /// <summary>Verifies replace and move actions read the latest complete ordering.</summary>
    [Fact]
    public void BindItems_WhenItemsReplaceAndMove_UsesLatestOrder()
    {
        var source = new ObservableCollection<BindingItem> { new("A"), new("B"), new("C") };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        source[1] = new BindingItem("X");
        source.Move(2, 0);

        target.Items.Select(static item => item!.ToString()).ShouldBe(["C", "A", "X"]);
    }

    /// <summary>Verifies ComboBox receives the same observable snapshot contract.</summary>
    [Fact]
    public void BindItems_WhenComboSourceChanges_UpdatesChoices()
    {
        var source = new ObservableCollection<BindingItem> { new("A") };
        var model = new BindingModel { Items = source };
        var target = new ComboBox();
        using var binding = target.BindItems(model, value => value.Items);

        source.Add(new BindingItem("B"));

        target.Items.Select(static item => item!.ToString()).ShouldBe(["A", "B"]);
    }

    /// <summary>Verifies a one-way converter projects a differently typed source value.</summary>
    [Fact]
    public void BindProperty_WhenTypesDiffer_ConvertsToTarget()
    {
        var model = new BindingModel { Number = 12 };
        var target = new ControlText();
        using var binding = target.BindProperty(
            control => control.Content,
            model,
            source => source.Number,
            value => value.ToString(CultureInfo.InvariantCulture),
            convertBack: null,
            BindingMode.OneWay,
            "missing");

        target.Content.ShouldBe("12");

        model.Number = 25;

        target.Content.ShouldBe("25");
    }

    /// <summary>Verifies a two-way converter parses committed target values back into the model.</summary>
    [Fact]
    public void BindProperty_WhenTwoWay_UsesReverseConverter()
    {
        var model = new BindingModel { Number = 12 };
        var target = new TextInput();
        using var binding = target.BindProperty(
            control => control.Text,
            model,
            source => source.Number,
            value => value.ToString(CultureInfo.InvariantCulture),
            value => int.Parse(value, CultureInfo.InvariantCulture),
            BindingMode.TwoWay,
            "0");

        target.Text.ShouldBe("12");

        target.Text = "34";

        model.Number.ShouldBe(34);
    }

    /// <summary>Verifies a target property that validates rather than clamps (e.g. Slider.Value
    /// outside its Minimum/Maximum) falls back to the declared fallback value instead of
    /// letting the write's own exception escape ApplySourceToTarget uncaught.</summary>
    [Fact]
    public void BindProperty_WhenTargetWriteThrows_AppliesFallback()
    {
        var model = new BindingModel { Number = 25 };
        var target = new Slider { Maximum = 50 };
        using var binding = target.BindProperty(
            control => control.Value,
            model,
            source => source.Number,
            value => value,
            convertBack: null,
            BindingMode.OneWay,
            0);

        target.Value.ShouldBe(25);

        _ = Should.NotThrow(() => model.Number = 999);

        target.Value.ShouldBe(0);
    }

    /// <summary>Verifies a reverse converter that throws on an unparseable target value is
    /// dropped rather than erupting out of the control's own property setter, leaving the
    /// model untouched.</summary>
    [Fact]
    public void BindProperty_WhenReverseConverterThrows_DropsTheUpdate()
    {
        var model = new BindingModel { Number = 12 };
        var target = new TextInput();
        using var binding = target.BindProperty(
            control => control.Text,
            model,
            source => source.Number,
            value => value.ToString(CultureInfo.InvariantCulture),
            value => int.Parse(value, CultureInfo.InvariantCulture),
            BindingMode.TwoWay,
            "0");

        target.Text.ShouldBe("12");

        _ = Should.NotThrow(() => target.Text = "12x");

        model.Number.ShouldBe(12);
    }

    /// <summary>Verifies a validating model setter that throws on the reverse write is dropped
    /// rather than erupting out of the control's own property setter, leaving the model
    /// untouched and the target keeping its already-committed value.</summary>
    [Fact]
    public void BindProperty_WhenReverseWriteThrows_DropsTheUpdate()
    {
        var model = new BindingModel { ValidatedNumber = 25 };
        var target = new Slider { Minimum = -100, Maximum = 100 };
        using var binding = target.BindProperty(
            control => control.Value,
            model,
            source => source.ValidatedNumber,
            value => value,
            value => value,
            BindingMode.TwoWay,
            0);

        target.Value.ShouldBe(25);

        _ = Should.NotThrow(() => target.Value = -5);

        model.ValidatedNumber.ShouldBe(25);
        target.Value.ShouldBe(-5);
    }

    /// <summary>Verifies an unavailable nested path uses only the declared fallback.</summary>
    [Fact]
    public void BindProperty_WhenPathIsUnavailable_UsesFallback()
    {
        var model = new BindingModel();
        var target = new ControlText();
        using var binding = target.BindProperty(
            control => control.Content,
            model,
            source => source.Address!.City,
            value => value ?? "null leaf",
            convertBack: null,
            BindingMode.OneWay,
            "missing branch");

        target.Content.ShouldBe("missing branch");
    }

    /// <summary>Verifies a throwing converter applies the declared fallback value.</summary>
    [Fact]
    public void BindProperty_WhenConverterThrows_AppliesFallback()
    {
        var model = new BindingModel { Number = 1 };
        var target = new ControlText();
        using var binding = target.BindProperty(
            control => control.Content,
            model,
            source => source.Number,
            _ => throw new InvalidOperationException("boom"),
            convertBack: null,
            BindingMode.OneWay,
            "fallback");

        target.Content.ShouldBe("fallback");
    }

    /// <summary>Verifies a throwing converter during update still applies the fallback.</summary>
    [Fact]
    public void BindProperty_WhenConverterThrowsDuringUpdate_AppliesFallback()
    {
        var callCount = 0;
        var model = new BindingModel { Number = 1 };
        var target = new ControlText();
        using var binding = target.BindProperty(
            control => control.Content,
            model,
            source => source.Number,
            value =>
            {
                callCount++;
                return callCount == 1
                    ? value.ToString(CultureInfo.InvariantCulture)
                    : throw new InvalidOperationException("boom");
            },
            convertBack: null,
            BindingMode.OneWay,
            "fallback");

        target.Content.ShouldBe("1");

        model.Number = 2;

        target.Content.ShouldBe("fallback");
    }

    /// <summary>Verifies a source change already on the dispatcher is visible before notification returns.</summary>
    [Fact]
    public async Task Source_WhenChangedOnDispatcher_UpdatesInlineAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var model = new BindingModel { Name = "Before" };
            var target = new ControlText();
            target.Attach(dispatcher);
            using var binding = target.Bind(model, source => source.Name);

            model.Name = "After";

            target.Content.ShouldBe("After");
            target.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a source change that races the owning dispatcher stopping is dropped
    /// instead of throwing into the thread that mutated the source model.</summary>
    [Fact]
    public async Task Source_WhenOwningDispatcherIsStopping_DropsUpdateWithoutThrowingAsync()
    {
        var dispatcher = Dispatcher.Start();
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText();

        await dispatcher.InvokeAsync(() =>
        {
            target.Attach(dispatcher);
            _ = target.Bind(model, source => source.Name);
        }, TestContext.Current.CancellationToken);

        await dispatcher.DisposeAsync();

        // The binding's own post-back-to-dispatcher path — not this test — is what must
        // tolerate the dispatcher already being disposed; a stopping dispatcher
        // is going away regardless, so the pending update is dropped, not surfaced here.
        _ = Should.NotThrow(() => model.Name = "After");
        target.Content.ShouldBe("Before");
    }

    /// <summary>Verifies explicit disposal of an attached binding is dispatcher-affine.</summary>
    [Fact]
    public async Task Dispose_WhenCalledFromWorker_ThrowsBeforeReleasingBindingAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText();
        Binding? binding = null;
        await dispatcher.InvokeAsync(() =>
        {
            target.Attach(dispatcher);
            binding = target.Bind(model, source => source.Name);
        }, TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(binding!.Dispose);

        await dispatcher.InvokeAsync(() =>
        {
            binding.IsDisposed.ShouldBeFalse();
            binding.Dispose();
            target.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a queued update becomes inert when an earlier queued callback disposes it.</summary>
    [Fact]
    public async Task Source_WhenBindingIsDisposedBeforeQueuedUpdate_SuppressesMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText();
        Binding? binding = null;
        await dispatcher.InvokeAsync(() =>
        {
            target.Attach(dispatcher);
            binding = target.Bind(model, source => source.Name);
        }, TestContext.Current.CancellationToken);
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
        });
        entered.Wait(TestContext.Current.CancellationToken);
        dispatcher.Post(binding!.Dispose);

        await Task.Run(() =>
        {
            model.Name = "Queued";
        }, TestContext.Current.CancellationToken);
        release.Set();

        await dispatcher.InvokeAsync(() =>
        {
            binding.IsDisposed.ShouldBeTrue();
            target.Content.ShouldBe("Before");
            target.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies queue saturation propagates and a later notification can retry.</summary>
    [Fact]
    public async Task Source_WhenDispatcherQueueIsFull_DropsUpdateAndPermitsRetryAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var model = new BindingModel { Name = "Before" };
        var target = new ControlText();
        Binding? binding = null;
        using ManualResetEventSlim updated = new();
        await dispatcher.InvokeAsync(() =>
        {
            target.Attach(dispatcher);
            binding = target.Bind(model, source => source.Name);
            target.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlText.Content) && target.Content == "Retried")
                {
                    updated.Set();
                }
            };
        }, TestContext.Current.CancellationToken);
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        using ManualResetEventSlim queuedCompleted = new();
        dispatcher.Post(() =>
        {
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
        });
        entered.Wait(TestContext.Current.CancellationToken);

        try
        {
            dispatcher.Post(queuedCompleted.Set);

            // A binding whose target dispatcher queue is momentarily full drops the pending
            // update instead of throwing into the thread that mutated the source model —
            // the same soft-fail contract as a stopping dispatcher.
            _ = Should.NotThrow(() => model.Name = "Rejected");
        }
        finally
        {
            release.Set();
        }

        queuedCompleted.Wait(TestContext.Current.CancellationToken);
        target.Content.ShouldNotBe("Rejected");

        model.Name = "Retried";
        updated.Wait(TestContext.Current.CancellationToken);

        await dispatcher.InvokeAsync(() =>
        {
            target.Content.ShouldBe("Retried");
            binding!.Dispose();
            target.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a worker burst posts one latest-value target mutation.</summary>
    [Fact]
    public async Task Source_WhenWorkerBurstArrives_CoalescesLatestOnDispatcherAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var model = new BindingModel { Name = "0" };
        var target = new ControlText();
        Binding? binding = null;
        var changes = 0;

        await dispatcher.InvokeAsync(() =>
        {
            target.Attach(dispatcher);
            binding = target.Bind(model, source => source.Name);
            target.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlText.Content))
                {
                    changes++;
                }
            };
        }, TestContext.Current.CancellationToken);

        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
        });
        entered.Wait(TestContext.Current.CancellationToken);

        try
        {
            await Task.Run(() =>
            {
                for (var index = 1; index <= 10_000; index++)
                {
                    model.Name = index.ToString(CultureInfo.InvariantCulture);
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            release.Set();
        }

        await dispatcher.InvokeAsync(() =>
        {
            target.Content.ShouldBe("10000");
            changes.ShouldBe(1);
            binding!.Dispose();
            target.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a calendar binding initializes from the model and syncs two-way.</summary>
    [Fact]
    public void CalendarBind_WhenSourceChanges_SyncsSelection()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var interval = new DateInterval(today, today);
        var model = new BindingModel { DateSelection = interval };
        var calendar = new UiCalendar();
        using var binding = calendar.Bind(model, source => source.DateSelection);

        calendar.Selection.ShouldBe(interval);
        binding.Mode.ShouldBe(BindingMode.TwoWay);
    }

    /// <summary>Verifies a calendar selection change propagates back to the model.</summary>
    [Fact]
    public void CalendarBind_WhenTargetChanges_UpdatesModel()
    {
        var model = new BindingModel();
        var calendar = new UiCalendar();
        using var binding = calendar.Bind(model, source => source.DateSelection);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var interval = new DateInterval(today, today);

        calendar.Selection = interval;

        model.DateSelection.ShouldBe(interval);
    }

    /// <summary>Verifies a null model selection clears the calendar.</summary>
    [Fact]
    public void CalendarBind_WhenSourceIsNull_ClearsSelection()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var model = new BindingModel { DateSelection = new DateInterval(today, today) };
        var calendar = new UiCalendar();
        using var binding = calendar.Bind(model, source => source.DateSelection);

        model.DateSelection = null;

        calendar.Selection.ShouldBeNull();
    }

    /// <summary>Verifies an expander binding initializes from the model.</summary>
    [Fact]
    public void ExpanderBind_WhenSourceChanges_SyncsIsExpanded()
    {
        var model = new BindingModel { IsExpanded = false };
        var expander = new Expander();
        using var binding = expander.Bind(model, source => source.IsExpanded);

        expander.IsExpanded.ShouldBeFalse();
        binding.Mode.ShouldBe(BindingMode.TwoWay);
    }

    /// <summary>Verifies an expander toggle propagates back to the model.</summary>
    [Fact]
    public void ExpanderBind_WhenTargetChanges_UpdatesModel()
    {
        var model = new BindingModel { IsExpanded = true };
        var expander = new Expander();
        using var binding = expander.Bind(model, source => source.IsExpanded);

        expander.IsExpanded = false;

        model.IsExpanded.ShouldBeFalse();
    }

    /// <summary>Verifies a FIGlet text binding initializes from the model.</summary>
    [Fact]
    public void FigletTextBind_WhenSourceChanges_SyncsContent()
    {
        var model = new BindingModel { Name = "Hello" };
        var font = FigletCatalog.Default.Load("small");
        var target = new FigletText(font);
        using var binding = target.Bind(model, source => source.Name);

        target.Content.ShouldBe("Hello");
        binding.Mode.ShouldBe(BindingMode.OneWay);
    }

    /// <summary>Verifies a FIGlet text binding converts null to empty.</summary>
    [Fact]
    public void FigletTextBind_WhenSourceIsNull_AppliesEmpty()
    {
        var model = new BindingModel();
        var font = FigletCatalog.Default.Load("small");
        var target = new FigletText(font);
        using var binding = target.Bind(model, source => source.Name);

        target.Content.ShouldBeEmpty();
    }

    /// <summary>Verifies a FIGlet text binding updates when the model changes.</summary>
    [Fact]
    public void FigletTextBind_WhenSourceUpdates_ReflectsChange()
    {
        var model = new BindingModel { Name = "Before" };
        var font = FigletCatalog.Default.Load("small");
        var target = new FigletText(font);
        using var binding = target.Bind(model, source => source.Name);

        model.Name = "After";

        target.Content.ShouldBe("After");
    }

    /// <summary>Verifies a single Add to ObservableCollection applies incrementally to the list.</summary>
    [Fact]
    public void BindItems_WhenSingleItemAdded_AppliesIncrementally()
    {
        var source = new ObservableCollection<BindingItem> { new("A"), new("B") };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);
        var third = new BindingItem("C");

        source.Add(third);

        target.Items.ShouldBe(new object?[] { source[0], source[1], third });
    }

    /// <summary>Verifies a single Remove from ObservableCollection applies incrementally.</summary>
    [Fact]
    public void BindItems_WhenSingleItemRemoved_AppliesIncrementally()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var third = new BindingItem("C");
        var source = new ObservableCollection<BindingItem> { first, second, third };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        _ = source.Remove(second);

        target.Items.ShouldBe(new object?[] { first, third });
    }

    /// <summary>Verifies a single Replace in ObservableCollection applies incrementally.</summary>
    [Fact]
    public void BindItems_WhenSingleItemReplaced_AppliesIncrementally()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var source = new ObservableCollection<BindingItem> { first, second };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);
        var replacement = new BindingItem("X");

        source[1] = replacement;

        target.Items.ShouldBe(new object?[] { first, replacement });
    }

    /// <summary>Verifies Insert at a specific position updates the list correctly.</summary>
    [Fact]
    public void BindItems_WhenInsertedAtPosition_InsertsAtCorrectIndex()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("C");
        var source = new ObservableCollection<BindingItem> { first, second };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);
        var inserted = new BindingItem("B");

        source.Insert(1, inserted);

        target.Items.ShouldBe(new object?[] { first, inserted, second });
    }

    /// <summary>Verifies Move falls back to full snapshot since it is not incrementally handled.</summary>
    [Fact]
    public void BindItems_WhenItemMoved_FallsBackToFullSnapshot()
    {
        var source = new ObservableCollection<BindingItem> { new("A"), new("B"), new("C") };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        source.Move(2, 0);

        target.Items.Select(static item => item!.ToString()).ShouldBe(["C", "A", "B"]);
    }

    /// <summary>Verifies Clear falls back to full snapshot.</summary>
    [Fact]
    public void BindItems_WhenCleared_FallsBackToFullSnapshot()
    {
        var source = new ObservableCollection<BindingItem> { new("A"), new("B") };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        source.Clear();

        target.Items.ShouldBeEmpty();
    }

    /// <summary>Verifies selection is preserved when inserting before the selected index.</summary>
    [Fact]
    public void BindItems_WhenInsertedBeforeSelection_ShiftsSelection()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var source = new ObservableCollection<BindingItem> { first, second };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);
        target.SelectedIndex = 1;

        source.Insert(0, new BindingItem("Z"));

        target.SelectedIndex.ShouldBe(2);
        target.Items[target.SelectedIndex].ShouldBe(second);
    }

    /// <summary>Verifies ComboBox's own cached SelectedIndex shifts with an insert that lands
    /// before the selected item, and not just the private drop-down list's index — regression
    /// coverage for the cache only ever refreshing from the list's SelectionChanged event, which
    /// a pure index shift never raises.</summary>
    [Fact]
    public void BindItems_WhenComboInsertedBeforeSelection_ShiftsSelectedIndex()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var source = new ObservableCollection<BindingItem> { first, second };
        var model = new BindingModel { Items = source };
        var target = new ComboBox { DropDownHeight = 4, IsOpen = true };
        using var binding = target.BindItems(model, value => value.Items);
        new LayoutEngine().Layout(target, new Size(24, 12));
        target.SelectedIndex = 1;

        source.Insert(0, new BindingItem("Z"));

        target.SelectedIndex.ShouldBe(2);
        target.SelectedItem.ShouldBeSameAs(second);
    }

    /// <summary>Verifies the remove-shift companion to the insert-shift case above: removing an
    /// item before the selection still shifts ComboBox's own cached SelectedIndex down, keeping
    /// item identity.</summary>
    [Fact]
    public void BindItems_WhenComboRemovedBeforeSelection_ShiftsSelectedIndex()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var third = new BindingItem("C");
        var source = new ObservableCollection<BindingItem> { first, second, third };
        var model = new BindingModel { Items = source };
        var target = new ComboBox { DropDownHeight = 4, IsOpen = true };
        using var binding = target.BindItems(model, value => value.Items);
        new LayoutEngine().Layout(target, new Size(24, 12));
        target.SelectedIndex = 2;

        source.RemoveAt(0);

        target.SelectedIndex.ShouldBe(1);
        target.SelectedItem.ShouldBeSameAs(third);
    }

    /// <summary>Verifies a later arrange pass does not force-write ComboBox's stale cached
    /// SelectedIndex back onto the private drop-down list, corrupting the list's own
    /// already-correct post-shift selection — the actual failure mode reported against this
    /// desync, not just the ComboBox-facing symptom covered above.</summary>
    [Fact]
    public void BindItems_WhenComboInsertedBeforeSelection_PreservesDropDownListSelectionAfterArrange()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var source = new ObservableCollection<BindingItem> { first, second };
        var model = new BindingModel { Items = source };
        var target = new ComboBox { DropDownHeight = 4, IsOpen = true };
        using var binding = target.BindItems(model, value => value.Items);
        new LayoutEngine().Layout(target, new Size(24, 12));
        target.SelectedIndex = 1;

        source.Insert(0, new BindingItem("Z"));
        new LayoutEngine().Layout(target, new Size(24, 12));

        target.SelectedIndex.ShouldBe(2);
        target.GetDropDownList().SelectedIndex.ShouldBe(2);
    }

    /// <summary>Verifies removing a selected item clears the selection through binding.</summary>
    [Fact]
    public void BindItems_WhenSelectedItemRemoved_ClearsSelection()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var third = new BindingItem("C");
        var source = new ObservableCollection<BindingItem> { first, second, third };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);
        target.SelectedIndex = 1;

        _ = source.Remove(second);

        target.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies collection replacement after incremental changes works correctly.</summary>
    [Fact]
    public void BindItems_WhenCollectionReplacedAfterIncrementalChanges_UsesNewCollection()
    {
        var first = new ObservableCollection<BindingItem> { new("A") };
        var model = new BindingModel { Items = first };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        first.Add(new BindingItem("B"));
        target.Items.Count.ShouldBe(2);

        var second = new ObservableCollection<BindingItem> { new("X"), new("Y"), new("Z") };
        model.Items = second;

        target.Items.Select(static item => item!.ToString()).ShouldBe(["X", "Y", "Z"]);

        second.Add(new BindingItem("W"));

        target.Items.Select(static item => item!.ToString()).ShouldBe(["X", "Y", "Z", "W"]);
    }

    /// <summary>
    /// Verifies a collection-changed notification from a source the binding has
    /// already moved past does not apply to the replacement target. The
    /// interleaving is deterministic: an application handler registered on the
    /// original collection before the binding subscribes replaces the bound
    /// source from within its own callback, so .NET's already-snapshotted
    /// invocation list still delivers the stale notification to the binding
    /// after it has re-observed the new source.
    /// </summary>
    [Fact]
    public void BindItems_WhenStaleAddArrivesAfterSourceReplacement_KeepsOnlyReplacementItems()
    {
        var first = new ObservableCollection<BindingItem> { new("A") };
        var replacementItem = new BindingItem("X");
        var second = new ObservableCollection<BindingItem> { replacementItem };
        var model = new BindingModel { Items = first };
        var target = new UiListView();

        first.CollectionChanged += (_, _) => model.Items = second;

        using var binding = target.BindItems(model, value => value.Items);

        first.Add(new BindingItem("stale"));

        target.Items.ShouldBe(new object?[] { replacementItem });
    }

    /// <summary>
    /// Verifies an Add notification whose reported index exceeds the target's current item
    /// count is ignored instead of throwing, matching the guard Remove and Replace already
    /// apply. Ordinary <see cref="ObservableCollection{T}"/> mutations always report a
    /// consistent index, so this uses a spoofable source to simulate the hand-rolled
    /// <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> source, coalesced/batched
    /// event, or diverged-count timing the underlying bug is actually reachable from.
    /// </summary>
    [Fact]
    public void BindItems_WhenAddIndexExceedsTargetCount_IsIgnoredWithoutThrowing()
    {
        var source = new SpoofableAddCollection<BindingItem> { new("A") };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        Should.NotThrow(() => source.RaiseSpoofedAdd(new BindingItem("out-of-range"), 5));

        target.Items.ShouldBe(new object?[] { source[0] });
    }

    /// <summary>
    /// Verifies the same stale-source interleaving does not mutate the target
    /// after the binding has been disposed.
    /// </summary>
    [Fact]
    public void BindItems_WhenStaleAddArrivesAfterDisposal_DoesNotMutateTarget()
    {
        var first = new ObservableCollection<BindingItem> { new("A") };
        var model = new BindingModel { Items = first };
        var target = new UiListView();
        Binding? binding = null;

        // Registered before the binding subscribes, so it runs first in the
        // snapshotted invocation list and disposes before the binding's own
        // handler (later in that same already-captured list) is reached.
        first.CollectionChanged += (_, _) => binding!.Dispose();
        binding = target.BindItems(model, value => value.Items);

        first.Add(new BindingItem("stale"));

        target.Items.Count.ShouldBe(1);
    }

    /// <summary>Verifies selected values synchronize by item equality in both directions.</summary>
    [Fact]
    public void BindSelection_WhenEitherEndpointChanges_SynchronizesItem()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var model = new BindingModel
        {
            Items = [first, second],
            SelectedItem = second
        };
        var target = new UiListView();
        using var items = target.BindItems(model, value => value.Items);
        using var selection = target.BindSelection(model, value => value.SelectedItem);

        target.SelectedIndex.ShouldBe(1);

        target.SelectedIndex = 0;

        model.SelectedItem.ShouldBeSameAs(first);

        target.SelectedIndex = -1;

        model.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies item replacement cannot write a transient empty selection to the model.</summary>
    [Fact]
    public void BindItems_WhenItemsAreReplaced_PreservesModelSelection()
    {
        var selected = new BindingItem("Selected");
        var model = new BindingModel
        {
            Items = [selected],
            SelectedItem = selected
        };
        var target = new UiListView();
        using var items = target.BindItems(model, value => value.Items);
        using var selection = target.BindSelection(model, value => value.SelectedItem);

        model.Items =
        [
            new BindingItem("Other"),
            selected
        ];

        model.SelectedItem.ShouldBeSameAs(selected);
        target.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies an unmatched model value clears target selection without changing the model.</summary>
    [Fact]
    public void BindSelection_WhenValueIsUnmatched_ClearsTarget()
    {
        var missing = new BindingItem("Missing");
        var model = new BindingModel
        {
            Items = [new BindingItem("A")],
            SelectedItem = missing
        };
        var target = new UiListView();
        using var items = target.BindItems(model, value => value.Items);
        using var selection = target.BindSelection(model, value => value.SelectedItem);

        target.SelectedIndex.ShouldBe(-1);
        model.SelectedItem.ShouldBeSameAs(missing);
    }

    /// <summary>Verifies unsupported ListView selection modes fail before binding registration.</summary>
    [Fact]
    public void BindSelection_WhenListAllowsMany_Throws()
    {
        var model = new BindingModel { Items = [] };
        var target = new UiListView { SelectionMode = ListSelectionMode.Multiple };

        _ = Should.Throw<ArgumentException>(() =>
            target.BindSelection(model, value => value.SelectedItem));
    }

    /// <summary>Verifies ComboBox selected-value binding follows its item snapshot.</summary>
    [Fact]
    public void BindSelection_WhenComboChanges_SynchronizesItem()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var model = new BindingModel
        {
            Items = [first, second],
            SelectedItem = first
        };
        var target = new ComboBox();
        using var items = target.BindItems(model, value => value.Items);
        using var selection = target.BindSelection(model, value => value.SelectedItem);

        target.SelectedIndex = 1;

        model.SelectedItem.ShouldBeSameAs(second);
    }

    /// <summary>Verifies method-call paths are rejected.</summary>
    [Fact]
    public void BindProperty_WhenSourceIsMethodCall_Throws()
    {
        var model = new BindingModel();
        var target = new ControlText();

        _ = Should.Throw<ArgumentException>(() => target.BindProperty(
            control => control.Content,
            model,
            source => source.ToString(),
            BindingMode.OneWay));
    }

    /// <summary>Verifies a read-only source cannot accept two-way updates.</summary>
    [Fact]
    public void BindProperty_WhenSourceLeafIsReadOnly_Throws()
    {
        var model = new BindingModel();
        var target = new TextInput();

        _ = Should.Throw<ArgumentException>(() => target.BindProperty(
            control => control.Text,
            model,
            source => source.IsReadOnly,
            BindingMode.TwoWay));
    }

    /// <summary>Verifies duplicate target writers are rejected without disturbing the first binding.</summary>
    [Fact]
    public void Bind_WhenTargetPropertyAlreadyBound_ThrowsAndKeepsFirst()
    {
        var first = new BindingModel { Name = "First" };
        var second = new BindingModel { Name = "Second" };
        var target = new ControlText();
        using var binding = target.Bind(first, source => source.Name);

        _ = Should.Throw<ArgumentException>(() => target.Bind(second, source => source.Name));
        first.Name = "Current";

        target.Content.ShouldBe("Current");
    }

    /// <summary>Verifies unknown direction values fail before target mutation.</summary>
    [Fact]
    public void Bind_WhenModeIsUnknown_ThrowsBeforeMutation()
    {
        var model = new BindingModel { Name = "Model" };
        var target = new TextInput { Text = "Target" };

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            target.Bind(model, source => source.Name, (BindingMode) 99));

        target.Text.ShouldBe("Target");
    }

    /// <summary>Verifies two-way conversion requires a reverse converter.</summary>
    [Fact]
    public void BindProperty_WhenReverseConverterIsMissing_Throws()
    {
        var model = new BindingModel { Number = 1 };
        var target = new ControlText();

        _ = Should.Throw<ArgumentNullException>(() => target.BindProperty(
            control => control.Content,
            model,
            source => source.Number,
            value => value.ToString(CultureInfo.InvariantCulture),
            convertBack: null,
            BindingMode.TwoWay,
            string.Empty));
    }

    /// <summary>Verifies disposed targets reject declaration before model access.</summary>
    [Fact]
    public void Bind_WhenTargetIsDisposed_Throws()
    {
        var model = new BindingModel { Name = "Model" };
        var target = new ControlText();
        target.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => target.Bind(model, source => source.Name));
    }

    /// <summary>Verifies binding copies the initial series snapshot into every chart family.</summary>
    [Theory]
    [MemberData(nameof(Charts))]
    public void Bind_WhenStarted_AppliesInitialSeries(ControlBase control)
    {
        // Arrange
        var series = new ChartSeries("CPU");
        var model = new ChartBindingModel { Series = new ObservableCollection<ChartSeries> { series } };
        var chart = control.ShouldBeAssignableTo<IChartControl>();

        // Act
        using var binding = Bind(control, model);

        // Assert
        chart.Series.ShouldBe([series]);
    }

    /// <summary>Verifies observable membership changes refresh the chart series snapshot.</summary>
    [Fact]
    public void Bind_WhenObservableSeriesAddsItem_UpdatesChart()
    {
        // Arrange
        var model = new ChartBindingModel();
        var chart = new LineChart();
        using var binding = chart.Bind(model, source => source.Series);
        var series = new ChartSeries("Live");

        // Act
        model.ObservableSeries.Add(series);

        // Assert
        chart.Series.ShouldBe([series]);
    }

    /// <summary>Verifies replacing the source property atomically replaces chart membership.</summary>
    [Fact]
    public void Bind_WhenSourcePropertyIsReplaced_ReplacesChartSeries()
    {
        // Arrange
        var original = new ChartSeries("Original");
        var replacement = new ChartSeries("Replacement");
        var model = new ChartBindingModel
        {
            Series = new ObservableCollection<ChartSeries> { original }
        };
        var chart = new AreaChart();
        using var binding = chart.Bind(model, source => source.Series);

        // Act
        model.Series = new ObservableCollection<ChartSeries> { replacement };

        // Assert
        chart.Series.ShouldBe([replacement]);
    }

    /// <summary>Verifies a null source maps to an empty chart rather than a null target.</summary>
    [Fact]
    public void Bind_WhenSourceBecomesNull_ClearsChartSeries()
    {
        // Arrange
        var model = new ChartBindingModel
        {
            Series = new ObservableCollection<ChartSeries> { new("Original") }
        };
        var chart = new VerticalBarChart();
        using var binding = chart.Bind(model, source => source.Series);

        // Act
        model.Series = null;

        // Assert
        chart.Series.ShouldBeEmpty();
    }

    /// <summary>Supplies all public chart target types.</summary>
    public static TheoryData<ControlBase> Charts =>
    [
        new HorizontalBarChart(),
        new VerticalBarChart(),
        new LineChart(),
        new AreaChart(),
        new Sparkline()
    ];

    private static Binding Bind(ControlBase control, ChartBindingModel model) => control switch
    {
        HorizontalBarChart chart => chart.Bind(model, source => source.Series),
        VerticalBarChart chart => chart.Bind(model, source => source.Series),
        LineChart chart => chart.Bind(model, source => source.Series),
        AreaChart chart => chart.Bind(model, source => source.Series),
        Sparkline chart => chart.Bind(model, source => source.Series),
        _ => throw new ArgumentOutOfRangeException(nameof(control), control, "The chart target is unknown.")
    };
}
