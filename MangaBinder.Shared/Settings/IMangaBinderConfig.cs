using System.IO;

namespace MangaBinder.Settings;

/// <summary>
/// アプリケーションの共通設定へのアクセスを提供するインターフェースです。
/// </summary>
public interface IMangaBinderConfig
{
    /// <summary>DB接続文字列を取得します。</summary>
    string ConnectionString { get; }

    /// <summary>DBファイルの物理パスを取得します。</summary>
    string DatabasePath { get; }

    /// <summary>サポート対象の拡張子一覧を取得します。</summary>
    IReadOnlyList<SupportedFileExtension> SupportedExtensions { get; }

    /// <summary>サムネイル保存先フォルダのパスを取得します。</summary>
    string ThumbnailFolderPath { get; }

    /// <summary>指定されたファイル名のサムネイルフルパスを生成します。</summary>
    /// <param name="fileName">ファイル名（拡張子含む）。</param>
    /// <returns>サムネイルファイルのフルパス。</returns>
    string GetThumbnailFullPath(string fileName)
        => Path.Combine(this.ThumbnailFolderPath, fileName);

    /// <summary>サムネイル生成オプションを取得します。</summary>
    ThumbnailOptions ThumbnailOptions { get; }
}
