using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace MangaBinder.Bindings;

/// <summary>
/// <see cref="FileType"/> をキーとして <see cref="IThumbnailExtractor"/> を解決・キャッシュするファクトリクラスです。
/// </summary>
public class SeriesExtractorFactory
{
    /// <summary>スコープファクトリ。</summary>
    private readonly IServiceScopeFactory scopeFactory;

    /// <summary>FileType ごとの Extractor キャッシュ。</summary>
    private readonly ConcurrentDictionary<FileType, IThumbnailExtractor> cache = [];

    /// <summary>
    /// <see cref="SeriesExtractorFactory"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="scopeFactory">スコープファクトリ。</param>
    public SeriesExtractorFactory(IServiceScopeFactory scopeFactory)
        => this.scopeFactory = scopeFactory;

    /// <summary>
    /// 指定された <see cref="FileType"/> に対応する <see cref="IThumbnailExtractor"/> を返します。
    /// キャッシュに存在しない場合のみスコープを生成してサービスを解決します。
    /// </summary>
    /// <param name="type">解決するエクストラクターの種別。</param>
    /// <returns>対応する <see cref="IThumbnailExtractor"/>。</returns>
    public IThumbnailExtractor GetExtractor(FileType type)
    {
        return this.cache.GetOrAdd(type, t =>
        {
            using var scope = this.scopeFactory.CreateScope();
            return scope.ServiceProvider.GetRequiredKeyedService<IThumbnailExtractor>(t);
        });
    }
}
