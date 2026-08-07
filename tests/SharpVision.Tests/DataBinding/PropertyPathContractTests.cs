// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Linq.Expressions;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies expression grammar and observer lifetime boundaries.</summary>
public sealed class PropertyPathContractTests
{
    /// <summary>Verifies a lambda without one root parameter is rejected.</summary>
    [Fact]
    public void Create_WhenLambdaHasNoRoot_Throws()
    {
        var expression = Expression.Lambda(Expression.Constant("value"));

        _ = Should.Throw<ArgumentException>(() => PropertyPath.Create(expression, requireWritable: false));
    }

    /// <summary>Verifies boundary conversions unwrap to the same cached property path.</summary>
    [Fact]
    public void Create_WhenBoundaryConverts_ReadsProperty()
    {
        Expression<Func<BindingModel, object?>> expression = source => source.Name;
        var model = new BindingModel { Name = "Converted" };
        var path = PropertyPath.Create(expression, requireWritable: false);

        path.TryRead(model, out var value).ShouldBeTrue();

        value.ShouldBe("Converted");
    }

    /// <summary>Verifies checked numeric conversion is removed from property-path identity.</summary>
    [Fact]
    public void Create_WhenBoundaryUsesCheckedConversion_ReadsUnderlyingProperty()
    {
        var parameter = Expression.Parameter(typeof(BindingModel), "source");
        var property = Expression.Property(parameter, nameof(BindingModel.Number));
        var converted = Expression.ConvertChecked(property, typeof(long));
        var expression = Expression.Lambda<Func<BindingModel, long>>(converted, parameter);
        var model = new BindingModel { Number = 42 };
        var path = PropertyPath.Create(expression, requireWritable: false);

        path.TryRead(model, out var value).ShouldBeTrue();

        value.ShouldBe(42);
    }

    /// <summary>Verifies observer disposal is idempotent and suppresses later callbacks.</summary>
    [Fact]
    public void Dispose_WhenObserverDisposedTwice_SuppressesNotifications()
    {
        Expression<Func<BindingModel, string?>> expression = source => source.Name;
        var model = new BindingModel { Name = "Before" };
        var path = PropertyPath.Create(expression, requireWritable: false);
        var callbacks = 0;
        var observer = new PropertyPathObserver(model, path, () => callbacks++);

        observer.Dispose();
        observer.Dispose();
        model.Name = "After";

        callbacks.ShouldBe(0);
    }
}
