// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using System.Reflection;

/// <summary>Guards every public concrete control's explicit Hidden/Collapsed layout and mounted
/// coverage classification and its matching evidence.</summary>
/// <remarks>
/// <para>
/// <see cref="ControlBase"/> enforces the three-state <see cref="Visibility"/> leaf mechanics for
/// every control. A host that manages children or single content additionally owns
/// parent-specific effects - spacing, track contribution, desired-size aggregation, scroll
/// extents and offsets, item realization, and stale-cell cleanup - that are not structurally
/// required anywhere else. This gate requires every exported concrete control to declare which
/// family of those effects it owns (<see cref="ComponentVisibilityRole"/>) and to prove the exact
/// <see cref="ComponentVisibilityEvidence"/> that role demands.
/// </para>
/// <para>
/// Two classifications are exempt from attributed evidence entirely, because the classification
/// itself is the explicit exclusion the coverage matrix requires:
/// <see cref="ComponentVisibilityRole.Leaf"/> (no structural child or content; the whole contract
/// is proved once by <see cref="ControlBase"/>, so 30-odd no-op leaf tests would add nothing) and
/// <see cref="ComponentVisibilityRole.NotApplicable"/> (content that is not itself a tree of
/// <see cref="ControlBase"/> children, such as <c>JsonView</c> painting POCO rows onto one
/// internal surface). Their required evidence is
/// <see cref="ComponentVisibilityEvidence.ExcludedLeafCoveredByControlBase"/>, and the coverage
/// check below skips them entirely rather than requiring an attribute that carries that flag.
/// </para>
/// <para>
/// A third, narrower exemption applies to specific <see cref="ComponentVisibilityRole.SingleContent"/>
/// concretes - the floating surfaces (<c>Popup</c>, <c>Flyout</c>, <c>Tooltip</c>, <c>Window</c>,
/// <c>MessageBox</c>, <c>FilePickerDialog</c>, <c>SaveFileDialog</c>), <c>Prism</c>,
/// <c>StatusBarItem</c>, and <c>TabItem</c> - whose content-collapse path is exactly
/// <c>ContentControl</c>'s own, already proved once by <c>ContentControlTests</c>. Adding a
/// host-specific test for each would only repeat that base proof, so their required evidence is
/// also <see cref="ComponentVisibilityEvidence.ExcludedLeafCoveredByControlBase"/>. This is a
/// deliberate design ruling, not an oversight: it is why the required-evidence lookup keys off the
/// exact required value rather than only the role.
/// </para>
/// <para>
/// Abstract authoring bases with a layout contract of their own - <c>Container</c>,
/// <c>ContentControl</c>, <c>CompositeControlBase</c>, and <c>ItemsControl</c> - are not concrete,
/// so they never appear in <see cref="_classification"/> or <see cref="_requirements"/>. Their
/// generic contract is instead proved through a concrete or test-only subtype in their own
/// fixture (for example <c>ContainerTests</c>, <c>ContentControlTests</c>,
/// <c>CompositeControlBaseTests</c>), and a test proving that generic contract may still attribute
/// evidence toward a concrete descendant's requirement - contributing part of what that
/// descendant needs while its own dedicated fixture supplies the rest.
/// </para>
/// </remarks>
public sealed class ComponentVisibilityCoverageTests
{
    private const ComponentVisibilityEvidence _excluded =
        ComponentVisibilityEvidence.ExcludedLeafCoveredByControlBase;

    private const ComponentVisibilityEvidence _orderedOrTrackedChildren =
        ComponentVisibilityEvidence.HiddenRetainsSlot |
        ComponentVisibilityEvidence.CollapsedExcludesSize |
        ComponentVisibilityEvidence.CollapsedRemovesSpacingOrTrack |
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly |
        ComponentVisibilityEvidence.MountedTransitionCommittedGeometry |
        ComponentVisibilityEvidence.MountedTransitionHitTargets;

    private const ComponentVisibilityEvidence _layeredChildren =
        ComponentVisibilityEvidence.HiddenRetainsSlot |
        ComponentVisibilityEvidence.HiddenExcludesRenderInput |
        ComponentVisibilityEvidence.CollapsedExcludesSize |
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly |
        ComponentVisibilityEvidence.ZeroTinyConstraint |
        ComponentVisibilityEvidence.MountedTransitionCommittedGeometry |
        ComponentVisibilityEvidence.MountedTransitionHitTargets;

    private const ComponentVisibilityEvidence _singleContentFull =
        ComponentVisibilityEvidence.HiddenRetainsSlot |
        ComponentVisibilityEvidence.HiddenExcludesRenderInput |
        ComponentVisibilityEvidence.CollapsedExcludesSize |
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly |
        ComponentVisibilityEvidence.MountedTransitionCommittedGeometry |
        ComponentVisibilityEvidence.MountedTransitionHitTargets;

    // StatusBar's alignment buckets own real per-child skip arithmetic on both sides, so its
    // requirement carries the full ordered-children facet set.
    private const ComponentVisibilityEvidence _statusBarItems =
        ComponentVisibilityEvidence.HiddenRetainsSlot |
        ComponentVisibilityEvidence.HiddenExcludesRenderInput |
        ComponentVisibilityEvidence.CollapsedExcludesSize |
        ComponentVisibilityEvidence.CollapsedRemovesSpacingOrTrack |
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly |
        ComponentVisibilityEvidence.MountedTransitionCommittedGeometry |
        ComponentVisibilityEvidence.MountedTransitionHitTargets;

    // TabControl realizes its headers inside a real Stack, which already proves slot and size
    // math once; TabControl's own obligations are the behaviors layered on top - header
    // visibility mirroring with divider suppression, selection repair, and the keyboard scan
    // that skips a Collapsed page.
    private const ComponentVisibilityEvidence _tabControlItems =
        ComponentVisibilityEvidence.CollapsedRemovesSpacingOrTrack |
        ComponentVisibilityEvidence.CollapsedExcludesInput |
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly |
        ComponentVisibilityEvidence.MountedTransitionCommittedGeometry;

    // Menu's items host is likewise a real Stack; Menu's own obligations are the shared label
    // column measure, the directional scan that skips Collapsed and Hidden items, and mounted
    // stale-row cleanup.
    private const ComponentVisibilityEvidence _menuItems =
        ComponentVisibilityEvidence.HiddenExcludesRenderInput |
        ComponentVisibilityEvidence.CollapsedExcludesSize |
        ComponentVisibilityEvidence.CollapsedExcludesInput |
        ComponentVisibilityEvidence.MountedTransitionCommittedGeometry |
        ComponentVisibilityEvidence.MountedTransitionHitTargets;

    // TreeView flattens items into one shared stack, re-parenting them away from their logical
    // ancestors; its ancestor-visibility behavior is therefore pinned explicitly alongside the
    // navigation scan and row-contribution oracles.
    // HiddenRetainsSlot and TransitionInvalidatesCorrectly arrive from the CompositeControlBase
    // contract proofs, which attribute their evidence toward this composite descendant.
    private const ComponentVisibilityEvidence _treeViewItems =
        ComponentVisibilityEvidence.HiddenRetainsSlot |
        ComponentVisibilityEvidence.HiddenExcludesRenderInput |
        ComponentVisibilityEvidence.CollapsedExcludesSize |
        ComponentVisibilityEvidence.CollapsedExcludesInput |
        ComponentVisibilityEvidence.AncestorInheritance |
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly |
        ComponentVisibilityEvidence.MountedTransitionCommittedGeometry |
        ComponentVisibilityEvidence.MountedTransitionHitTargets;

    // NavigationView's own obligation is selection repair around group visibility changes; its
    // mounted group-collapse rendering, hit-test, and tab-stop cleanup are attributed on the
    // pre-existing surface coverage.
    // HiddenRetainsSlot and CollapsedExcludesSize arrive from the CompositeControlBase contract
    // proofs, which attribute their evidence toward this composite descendant.
    private const ComponentVisibilityEvidence _navigationViewItems =
        ComponentVisibilityEvidence.HiddenRetainsSlot |
        ComponentVisibilityEvidence.HiddenExcludesRenderInput |
        ComponentVisibilityEvidence.CollapsedExcludesSize |
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly |
        ComponentVisibilityEvidence.FocusCaptureCleanup |
        ComponentVisibilityEvidence.MountedTransitionCommittedGeometry |
        ComponentVisibilityEvidence.MountedTransitionHitTargets;

    // Table rows are plain data, not ControlBase children, so the row axis is structurally not
    // applicable; per-cell collapse is ControlBase's own generic contract flowing through the
    // presenter's unconditional Measure/Arrange calls, pinned by one integration oracle.
    private const ComponentVisibilityEvidence _tableCells =
        ComponentVisibilityEvidence.CollapsedExcludesSize |
        ComponentVisibilityEvidence.ZeroTinyConstraint;

    // ZeroTinyConstraint arrives from the Container scrolling-contract proofs, which attribute
    // their evidence toward this scrolling descendant.
    private const ComponentVisibilityEvidence _scrollingRealizedItems =
        ComponentVisibilityEvidence.HiddenRetainsSlot |
        ComponentVisibilityEvidence.HiddenExcludesRenderInput |
        ComponentVisibilityEvidence.CollapsedExcludesSize |
        ComponentVisibilityEvidence.CollapsedRemovesSpacingOrTrack |
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly |
        ComponentVisibilityEvidence.ZeroTinyConstraint |
        ComponentVisibilityEvidence.MountedTransitionCommittedGeometry |
        ComponentVisibilityEvidence.MountedTransitionHitTargets;

    // An index initializer ([key] = value) calls the Dictionary indexer setter, which silently
    // overwrites a duplicate key instead of failing. A duplicate control mapping here would
    // silently drop the earlier classification from the catalog with no signal. An Add-based
    // initializer ({ key, value }) calls Dictionary.Add instead, which throws ArgumentException on
    // a duplicate key, so a catalog mistake fails the moment this type initializes.
    private static readonly Dictionary<Type, ComponentVisibilityRole> _classification = new()
    {
        // Leaf: no structural child or content. ControlBase alone proves the whole contract.
        { typeof(HorizontalBarChart), ComponentVisibilityRole.Leaf },
        { typeof(VerticalBarChart), ComponentVisibilityRole.Leaf },
        { typeof(LineChart), ComponentVisibilityRole.Leaf },
        { typeof(AreaChart), ComponentVisibilityRole.Leaf },
        { typeof(Sparkline), ComponentVisibilityRole.Leaf },
        { typeof(ChaseIndicator), ComponentVisibilityRole.Leaf },
        { typeof(FigletText), ComponentVisibilityRole.Leaf },
        { typeof(Image), ComponentVisibilityRole.Leaf },
        { typeof(ProgressBar), ComponentVisibilityRole.Leaf },
        { typeof(Spinner), ComponentVisibilityRole.Leaf },
        { typeof(Separator), ComponentVisibilityRole.Leaf },
        { typeof(Button), ComponentVisibilityRole.Leaf },
        { typeof(CheckBox), ComponentVisibilityRole.Leaf },
        { typeof(RadioButton), ComponentVisibilityRole.Leaf },
        { typeof(HyperlinkButton), ComponentVisibilityRole.Leaf },
        { typeof(TextInput), ComponentVisibilityRole.Leaf },
        { typeof(TimeInput), ComponentVisibilityRole.Leaf },
        { typeof(DateInput), ComponentVisibilityRole.Leaf },
        { typeof(DateTimeInput), ComponentVisibilityRole.Leaf },
        { typeof(NumberInput), ComponentVisibilityRole.Leaf },
        { typeof(CurrencyInput), ComponentVisibilityRole.Leaf },
        { typeof(ComboBox), ComponentVisibilityRole.Leaf },
        { typeof(ScrollBar), ComponentVisibilityRole.Leaf },
        { typeof(Slider), ComponentVisibilityRole.Leaf },
        { typeof(UiCalendar), ComponentVisibilityRole.Leaf },
        { typeof(ColorPicker), ComponentVisibilityRole.Leaf },
        { typeof(ControlText), ComponentVisibilityRole.Leaf },
        { typeof(TreeViewItem), ComponentVisibilityRole.Leaf },
        { typeof(MenuItem), ComponentVisibilityRole.Leaf },
        { typeof(MenuSeparator), ComponentVisibilityRole.Leaf },
        { typeof(NavigationViewGroup), ComponentVisibilityRole.Leaf },
        { typeof(NavigationViewItem), ComponentVisibilityRole.Leaf },
        { typeof(NavigationViewSeparator), ComponentVisibilityRole.Leaf },

        // NotApplicable: content is not a tree of ControlBase children (rows painted from plain
        // data onto one internal surface), so the child-visibility matrix does not apply.
        { typeof(JsonView), ComponentVisibilityRole.NotApplicable },

        // SingleContent, base-covered: the content-collapse path is exactly ContentControl's own.
        { typeof(Prism), ComponentVisibilityRole.SingleContent },
        { typeof(StatusBarItem), ComponentVisibilityRole.SingleContent },
        { typeof(TabItem), ComponentVisibilityRole.SingleContent },
        { typeof(Popup), ComponentVisibilityRole.SingleContent },
        { typeof(Flyout), ComponentVisibilityRole.SingleContent },
        { typeof(Tooltip), ComponentVisibilityRole.SingleContent },
        { typeof(Window), ComponentVisibilityRole.SingleContent },
        { typeof(MessageBox), ComponentVisibilityRole.SingleContent },
        { typeof(FilePickerDialog), ComponentVisibilityRole.SingleContent },
        { typeof(SaveFileDialog), ComponentVisibilityRole.SingleContent },

        // SingleContent, host-specific: chrome composed around the content adds its own reflow.
        { typeof(GroupBox), ComponentVisibilityRole.SingleContent },
        { typeof(Expander), ComponentVisibilityRole.SingleContent },

        // OrderedChildren / TrackedChildren / LayeredChildren: structural Container descendants.
        { typeof(Stack), ComponentVisibilityRole.OrderedChildren },
        { typeof(Dock), ComponentVisibilityRole.OrderedChildren },
        { typeof(Grid), ComponentVisibilityRole.TrackedChildren },
        { typeof(Overlay), ComponentVisibilityRole.LayeredChildren },

        // RealizedItems: a realized, virtualization-capable item collection. NavigationView and
        // TreeView compose a private CompositeControlBase root ahead of realizing their items, so
        // they additionally prove the base single-root Hidden contract.
        { typeof(StatusBar), ComponentVisibilityRole.RealizedItems },
        { typeof(TabControl), ComponentVisibilityRole.RealizedItems },
        { typeof(Menu), ComponentVisibilityRole.RealizedItems },
        { typeof(TreeView), ComponentVisibilityRole.RealizedItems },
        { typeof(NavigationView), ComponentVisibilityRole.RealizedItems },
        { typeof(Table), ComponentVisibilityRole.RealizedItems },
        { typeof(UiListView), ComponentVisibilityRole.ScrollingExtent | ComponentVisibilityRole.RealizedItems }
    };

    // See the Add-vs-indexer rationale above; the same duplicate-detection reasoning applies here.
    private static readonly Dictionary<Type, ComponentVisibilityEvidence> _requirements = new()
    {
        { typeof(HorizontalBarChart), _excluded },
        { typeof(VerticalBarChart), _excluded },
        { typeof(LineChart), _excluded },
        { typeof(AreaChart), _excluded },
        { typeof(Sparkline), _excluded },
        { typeof(ChaseIndicator), _excluded },
        { typeof(FigletText), _excluded },
        { typeof(Image), _excluded },
        { typeof(ProgressBar), _excluded },
        { typeof(Spinner), _excluded },
        { typeof(Separator), _excluded },
        { typeof(Button), _excluded },
        { typeof(CheckBox), _excluded },
        { typeof(RadioButton), _excluded },
        { typeof(HyperlinkButton), _excluded },
        { typeof(TextInput), _excluded },
        { typeof(TimeInput), _excluded },
        { typeof(DateInput), _excluded },
        { typeof(DateTimeInput), _excluded },
        { typeof(NumberInput), _excluded },
        { typeof(CurrencyInput), _excluded },
        { typeof(ComboBox), _excluded },
        { typeof(ScrollBar), _excluded },
        { typeof(Slider), _excluded },
        { typeof(UiCalendar), _excluded },
        { typeof(ColorPicker), _excluded },
        { typeof(ControlText), _excluded },
        { typeof(TreeViewItem), _excluded },
        { typeof(MenuItem), _excluded },
        { typeof(MenuSeparator), _excluded },
        { typeof(NavigationViewGroup), _excluded },
        { typeof(NavigationViewItem), _excluded },
        { typeof(NavigationViewSeparator), _excluded },
        { typeof(JsonView), _excluded },
        { typeof(Prism), _excluded },
        { typeof(StatusBarItem), _excluded },
        { typeof(TabItem), _excluded },
        { typeof(Popup), _excluded },
        { typeof(Flyout), _excluded },
        { typeof(Tooltip), _excluded },
        { typeof(Window), _excluded },
        { typeof(MessageBox), _excluded },
        { typeof(FilePickerDialog), _excluded },
        { typeof(SaveFileDialog), _excluded },
        { typeof(GroupBox), _singleContentFull },
        { typeof(Expander), _singleContentFull },
        { typeof(Stack), _orderedOrTrackedChildren },
        { typeof(Dock), _orderedOrTrackedChildren },
        { typeof(Grid), _orderedOrTrackedChildren },
        { typeof(Overlay), _layeredChildren },
        { typeof(StatusBar), _statusBarItems },
        { typeof(TabControl), _tabControlItems },
        { typeof(Menu), _menuItems },
        { typeof(Table), _tableCells },
        { typeof(TreeView), _treeViewItems },
        { typeof(NavigationView), _navigationViewItems },
        { typeof(UiListView), _scrollingRealizedItems }
    };

    /// <summary>Verifies every exported concrete control has exactly one visibility classification
    /// and one matching required-evidence entry.</summary>
    [Fact]
    public void Catalog_WhenPublicConcreteControlsChange_RequiresExplicitVisibilityClassification()
    {
        var controls = typeof(ControlBase).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && typeof(ControlBase).IsAssignableFrom(type))
            .ToHashSet();

        _classification.Keys.ShouldBe(controls, ignoreOrder: true);
        _requirements.Keys.ShouldBe(controls, ignoreOrder: true);
    }

    /// <summary>Verifies every catalogued requirement is backed by exact attributed evidence, and
    /// that a control catalogued with no host-specific evidence carries none.</summary>
    [Fact]
    public void Evidence_WhenVisibilityRequirementsChange_RequiresExactCoverage()
    {
        var evidence = new Dictionary<Type, ComponentVisibilityEvidence>();

        foreach (var (_, attribute) in CollectEvidenceAttributes())
        {
            _requirements.ContainsKey(attribute.ControlType).ShouldBeTrue(
                $"{attribute.ControlType.Name} evidence references an uncatalogued control.");
            _ = evidence.TryGetValue(attribute.ControlType, out var previous);
            evidence[attribute.ControlType] = previous | attribute.Evidence;
        }

        // Collected across every control instead of asserted inline, so one failing run reports
        // every incomplete host at once rather than stopping at the first.
        var failures = new List<string>();

        foreach (var (control, required) in _requirements)
        {
            if (required == _excluded)
            {
                // Leaf, NotApplicable, and base-covered SingleContent hosts need no attributed
                // evidence - the classification itself is the explicit exclusion.
                continue;
            }

            _ = evidence.TryGetValue(control, out var proved);
            var missing = required & ~proved;
            var unexpected = proved & ~required;

            if (missing != ComponentVisibilityEvidence.None || unexpected != ComponentVisibilityEvidence.None)
            {
                failures.Add($"{control.Name} - missing: {missing}, unexpected: {unexpected}");
            }
        }

        failures.ShouldBeEmpty();
    }

    /// <summary>Verifies no attributed evidence targets a control whose classification is itself
    /// the explicit no-evidence exclusion - an attribute there would contradict the classification
    /// instead of narrowing an already-required set.</summary>
    [Fact]
    public void Evidence_WhenAttributed_NeverTargetsExemptControls()
    {
        foreach (var (method, attribute) in CollectEvidenceAttributes())
        {
            _ = _classification.TryGetValue(attribute.ControlType, out var role);

            (role is ComponentVisibilityRole.Leaf or ComponentVisibilityRole.NotApplicable).ShouldBeFalse(
                $"{method.DeclaringType?.Name}.{method.Name} attributes {attribute.ControlType.Name}, " +
                "but that control is classified Leaf or NotApplicable and needs no evidence.");
        }
    }

    private static IEnumerable<(MethodInfo Method, ComponentVisibilityEvidenceAttribute Attribute)>
        CollectEvidenceAttributes() =>
        from type in typeof(ComponentVisibilityCoverageTests).Assembly.GetTypes()
        from method in type.GetMethods()
        from attribute in method
            .GetCustomAttributes(typeof(ComponentVisibilityEvidenceAttribute), inherit: false)
            .Cast<ComponentVisibilityEvidenceAttribute>()
        select (method, attribute);
}
