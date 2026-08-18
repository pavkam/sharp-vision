// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

using Performance;

/// <summary>Gates <see cref="ShortcutManager.Process"/> against walking the owned-control tree on
/// an ordinary keypress when no attached MenuItem anywhere has a live keyboard shortcut.</summary>
/// <remarks>
/// <c>ShortcutManager</c>'s internal tree walk always allocates a <see cref="List{T}"/> of
/// matches before it descends one level - it cannot walk without first allocating. Bytes
/// allocated is therefore an exact, deterministic operation-count proxy for "did the walk run at
/// all," the same technique <c>RouterPerformanceTests</c> and <c>BindingPerformanceTests</c>
/// already use in this suite. A per-node visit counter would prove the same thing more directly,
/// but the owned-control accessors the walk uses are ordinary, non-virtual members - nothing a
/// probe subclass can intercept - so instrumenting per-node visits would mean changing shared
/// control-tree infrastructure, which is out of scope here.
/// </remarks>
[Collection(PerformanceGroup.Name)]
public sealed class ShortcutManagerPerformanceTests
{
    private const int _branchDepth = 8;
    private const int _branchWidth = 30;

    /// <summary>Verifies a repeated ordinary keypress against a realistically sized tree (in the
    /// hundreds of controls, matching a populated form or docked panel layout) with zero live
    /// shortcuts anywhere allocates no managed memory after warmup - proving
    /// <see cref="ShortcutManager.Process"/> skips its internal tree walk entirely rather than
    /// merely making it cheap.</summary>
    [Fact]
    public async Task Process_WhenNoLiveShortcutsExistInARealisticTree_AllocatesZeroBytesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = BuildRealisticTree(buriedShortcut: null, out _);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new ShortcutManager(root, focus, modality);
            var stroke = Character('x');

            manager.Process(stroke).ShouldBeFalse();
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var iteration = 0; iteration < 1_000; iteration++)
            {
                _ = manager.Process(stroke);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            allocated.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies one shortcut buried inside a closed submenu, surrounded by the same
    /// large shortcut-free decoy tree as the zero-allocation case, still matches and invokes - a
    /// regression guard proving the live-shortcut count never produces a false negative, including
    /// for an item reachable only through a closed submenu.</summary>
    [Fact]
    public async Task Process_WhenOneShortcutIsBuriedInsideAClosedSubmenu_StillInvokesItAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = BuildRealisticTree(CtrlS, out var buried);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var manager = new ShortcutManager(root, focus, modality);
            var invocations = 0;
            buried.Invoked += (_, _) => invocations++;

            var handled = manager.Process(Ctrl('s'));

            handled.ShouldBeTrue();
            invocations.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Builds an owned-control tree in the hundreds of controls - a chain of nested
    /// containers each carrying a run of ordinary leaves, mirroring a populated form or docked
    /// panel layout - with one MenuItem buried inside a closed submenu at the deepest level.</summary>
    /// <param name="buriedShortcut">The shortcut assigned to the buried item, or null for none.</param>
    /// <param name="buried">The buried item.</param>
    private static Stack BuildRealisticTree(KeyGesture? buriedShortcut, out MenuItem buried)
    {
        var root = new Stack();
        Container branch = root;

        for (var depth = 0; depth < _branchDepth; depth++)
        {
            var level = new ProbeContainer();
            branch.Children.Add(level);

            for (var sibling = 0; sibling < _branchWidth; sibling++)
            {
                level.Children.Add(new ProbeControl());
            }

            branch = level;
        }

        var menu = new Menu();
        var submenu = new Menu();
        buried = new MenuItem { Text = "Save", Shortcut = buriedShortcut };
        submenu.Items.Add(buried);
        var outer = new MenuItem { Text = "File", Submenu = submenu };
        menu.Items.Add(outer);
        branch.Children.Add(menu);

        return root;
    }

    private static KeyGesture CtrlS => new(Code.Character, Modifiers.Control, new Rune('s'));

    private static Stroke Ctrl(char value) =>
        new(Code.Character, new Rune(value), nativeCode: 0, Modifiers.Control, KeyAction.Press);

    private static Stroke Character(char value) =>
        new(Code.Character, new Rune(value), nativeCode: 0, Modifiers.None, KeyAction.Press);
}
