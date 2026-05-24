using System.Windows.Markup;

namespace MangaBinder.Binding;

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
