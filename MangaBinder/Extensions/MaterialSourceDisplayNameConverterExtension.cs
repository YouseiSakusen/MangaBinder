using MangaBinder.Converters;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Markup;

namespace MangaBinder.Extensions;

/// <summary>
/// DI コンテナから <see cref="MaterialSourceDisplayNameConverter"/> を取得する <see cref="MarkupExtension"/> です。
/// </summary>
public class MaterialSourceDisplayNameConverterExtension : MarkupExtension
{
	/// <inheritdoc/>
	public override object ProvideValue(IServiceProvider serviceProvider)
		=> App.Services.GetRequiredService<MaterialSourceDisplayNameConverter>();
}
