// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using System.ComponentModel;

/// <summary>Waits for a dialog condition driven by <see cref="INotifyPropertyChanged"/> instead of
/// a fixed poll interval, most commonly <c>FileDialogBase.IsLoading</c> settling.</summary>
internal static class DialogWait
{
    /// <summary>Waits until <paramref name="predicate"/> holds, signalled by
    /// <paramref name="control"/>'s <see cref="ControlBase.PropertyChanged"/> event rather than
    /// polling on a timer.</summary>
    /// <param name="surface">The component surface hosting the control's application dispatcher.</param>
    /// <param name="control">The mounted control whose property changes may satisfy the predicate.</param>
    /// <param name="predicate">The dispatcher-thread condition to await.</param>
    /// <returns>A task completed once the predicate holds.</returns>
    internal static async Task UntilAsync(ComponentSurface surface, ControlBase control, Func<bool> predicate)
    {
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;

            if (predicate())
            {
                _ = settled.TrySetResult();
            }
        }

        // Checked once before subscribing - the condition may already hold - and once again after,
        // because the property could have changed on the dispatcher thread in the window between
        // the first check and the subscription taking effect. Without the second check a change
        // landing in that window would raise PropertyChanged before OnPropertyChanged was attached
        // and the signal would be lost, leaving nothing to complete settled.
        if (await surface.Application.Dispatcher.InvokeAsync(predicate, TestContext.Current.CancellationToken))
        {
            return;
        }

        control.PropertyChanged += OnPropertyChanged;

        try
        {
            if (await surface.Application.Dispatcher.InvokeAsync(predicate, TestContext.Current.CancellationToken))
            {
                return;
            }

            await settled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            control.PropertyChanged -= OnPropertyChanged;
        }
    }
}
