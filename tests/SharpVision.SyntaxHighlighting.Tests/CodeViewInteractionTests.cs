// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies CodeView pointer wheel interaction through a mounted surface.</summary>
public sealed class CodeViewInteractionTests
{
    /// <summary>Verifies a wheel notch over the rendered code content - past the fold gutter, over
    /// <c>_content</c> itself rather than the gutter column - still scrolls the view. The private
    /// scrolling host sits between the code content and CodeView on the routed ancestry, so its own
    /// automatic wheel handling sees the record before CodeView's own <c>OnEvent</c> override ever
    /// would; this proves that inner handling alone is sufficient.</summary>
    [Fact]
    public async Task Pointer_WhenWheelScrolledOverContent_ScrollsTheVerticalOffsetAsync()
    {
        var code = string.Join('\n', Enumerable.Range(0, 20).Select(index => $"line{index}")) + "\n";
        var view = new CodeView { Code = code, LineSize = 2, IsFoldingEnabled = true };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // The gutter reserves the leftmost two columns; wheel well past it, over the code text.
        await surface.Pointer.WheelAsync(view, new Point(5, 0), wheelY: -1);

        view.VerticalOffset.ShouldBe(2);
    }
}
