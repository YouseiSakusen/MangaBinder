using MangaBinder.Settings;

namespace MangaBinder.Jobs.Contexts;

/// <summary>
/// Worker プロセス全体で共有する実行コンテキストクラスです。
/// </summary>
public class WorkerContext : IMangaBinderConfig
{
    /// <summary>SQLite データベースへの接続文字列を取得します。</summary>
    public required string ConnectionString { get; init; }

    /// <summary>DBファイルの物理パスを取得します。</summary>
    public required string DatabasePath { get; init; }

    /// <summary>サムネイル生成をスキップするファイルサイズ閾値（MB）を取得します。</summary>
    public long ThumbnailExtractLimitFileSizeMB { get; init; }

    /// <summary>サムネイル生成をスキップするファイルサイズ閾値（バイト）を取得します。</summary>
    public long ThumbnailExtractLimitFileSizeBytes => this.ThumbnailExtractLimitFileSizeMB * 1024 * 1024;

    /// <summary>タイトル区切り文字群を取得します。</summary>
    public string TitleSeparatorChars { get; init; } = string.Empty;

    /// <summary>ジョブ監視の実行間隔（秒）を取得します。</summary>
    public int IntervalSeconds { get; init; }

    /// <summary>サポート対象の拡張子一覧を取得します。</summary>
    public required IReadOnlyList<SupportedFileExtension> SupportedExtensions { get; init; }

    /// <inheritdoc/>
    public required string ThumbnailFolderPath { get; init; }

    /// <inheritdoc/>
    public required ThumbnailOptions ThumbnailOptions { get; init; }
}

