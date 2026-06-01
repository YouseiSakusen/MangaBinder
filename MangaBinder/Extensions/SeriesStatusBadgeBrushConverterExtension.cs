using System.Windows.Markup;
using MangaBinder.Converters;

namespace MangaBinder.Extensions;

/// <summary>
/// <see cref="SeriesStatusBadgeBrushConverter"/> を XAML から直接使用するための <see cref="MarkupExtension"/> です。
/// </summary>
public class SeriesStatusBadgeBrushConverterExtension : MarkupExtension
{
    /// <summary>シングルトンインスタンスです。</summary>
    private static readonly SeriesStatusBadgeBrushConverter instance = new();

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider)
        => instance;
}
