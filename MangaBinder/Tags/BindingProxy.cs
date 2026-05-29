using System.Windows;

namespace MangaBinder.Tags;

/// <summary>
/// Popup など切断されたビジュアルツリーでもバインディングを伝播させるプロキシ。
/// </summary>
public sealed class BindingProxy : Freezable
{
	public static readonly DependencyProperty DataProperty =
		DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

	public object? Data
	{
		get => GetValue(DataProperty);
		set => SetValue(DataProperty, value);
	}

	protected override Freezable CreateInstanceCore() => new BindingProxy();
}
