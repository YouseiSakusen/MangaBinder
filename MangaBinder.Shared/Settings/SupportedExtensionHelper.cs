using MangaBinder.Bindings;

namespace MangaBinder.Settings;

/// <summary>
/// アプリケーションがサポートするファイル拡張子の判定を担う静的ヘルパークラスです。
/// </summary>
public static class SupportedExtensionHelper
{
    /// <summary>拡張子（小文字・ドット正規化済み）→ FileType のマップ。</summary>
    private static Dictionary<string, FileType> extensionMap = [];

    /// <summary>拡張子（小文字・ドット正規化済み）→ RequiresConversion のマップ。</summary>
    private static Dictionary<string, bool> conversionMap = [];

    /// <summary>OpenFileDialog 用の画像フィルタ文字列（キャッシュ）。</summary>
    private static string? cachedImageOpenFileDialogFilter;

    /// <summary>
    /// 拡張子リストを受け取り、内部マップを初期化します。
    /// アプリ起動時に一度だけ呼び出してください。
    /// </summary>
    /// <param name="extensions">サポート対象の拡張子リスト。</param>
    public static void Initialize(IReadOnlyList<SupportedFileExtension> extensions)
    {
        extensionMap = extensions
            .Where(e => !string.IsNullOrWhiteSpace(e.Extension))
            .ToDictionary(
                e => Normalize(e.Extension),
                e => (FileType)e.FileType);

        conversionMap = extensions
            .Where(e => !string.IsNullOrWhiteSpace(e.Extension))
            .ToDictionary(
                e => Normalize(e.Extension),
                e => e.RequiresConversion);

        // キャッシュを無効化（新しい拡張子リストから再生成される）
        cachedImageOpenFileDialogFilter = null;
    }

    /// <summary>
    /// 指定された拡張子に対応する <see cref="FileType"/> を返します。
    /// 未登録の場合は <c>null</c> を返します。
    /// </summary>
    /// <param name="extension">拡張子（例: ".zip" または "zip"）。</param>
    /// <returns>対応する <see cref="FileType"/>。見つからない場合は <c>null</c>。</returns>
    public static FileType? GetFileType(string extension)
        => extensionMap.TryGetValue(Normalize(extension), out var type) ? type : null;

    /// <summary>
    /// 指定された拡張子が画像ファイル（<see cref="FileType.Image"/>）かどうかを返します。
    /// </summary>
    /// <param name="extension">拡張子（例: ".jpg" または "jpg"）。</param>
    /// <returns>画像ファイルの場合は <c>true</c>。</returns>
    public static bool IsImage(string extension)
        => GetFileType(extension) == FileType.Image;

    /// <summary>
    /// 指定された拡張子がアーカイブファイル（<see cref="FileType.Archive"/>）かどうかを返します。
    /// </summary>
    /// <param name="extension">拡張子（例: ".zip" または "zip"）。</param>
    /// <returns>アーカイブファイルの場合は <c>true</c>。</returns>
    public static bool IsArchive(string extension)
        => GetFileType(extension) == FileType.Archive;

    /// <summary>
    /// 指定された拡張子が製本前に既定フォーマットへの変換が必要かどうかを返します。
    /// </summary>
    /// <param name="extension">拡張子（例: ".avif" または "avif"）。</param>
    /// <returns>変換が必要な場合は <c>true</c>。未登録の場合は <c>false</c>。</returns>
    public static bool RequiresConversion(string extension)
        => conversionMap.TryGetValue(Normalize(extension), out var requires) && requires;

    /// <summary>
    /// OpenFileDialog 用の画像ファイルフィルタ文字列を取得します。
    /// 例: "画像ファイル|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.avif|すべてのファイル|*.*"
    /// </summary>
    public static string ImageOpenFileDialogFilter
    {
        get
        {
            if (cachedImageOpenFileDialogFilter != null)
                return cachedImageOpenFileDialogFilter;

            // 拡張子マップから画像拡張子のみをフィルタリング
            var imageExtensions = extensionMap
                .Where(kvp => kvp.Value == FileType.Image)
                .Select(kvp => kvp.Key)
                .OrderBy(ext => ext)
                .ToList();

            if (imageExtensions.Count == 0)
            {
                cachedImageOpenFileDialogFilter = "すべてのファイル|*.*";
                return cachedImageOpenFileDialogFilter;
            }

            // ワイルドカード形式で結合（例: *.jpg;*.jpeg;*.png;...）
            var wildcards = string.Join(";", imageExtensions.Select(ext => $"*{ext}"));

            cachedImageOpenFileDialogFilter = $"画像ファイル|{wildcards}|すべてのファイル|*.*";
            return cachedImageOpenFileDialogFilter;
        }
    }

    /// <summary>
    /// 拡張子をドット付き小文字に正規化します。
    /// </summary>
    /// <param name="extension">正規化対象の拡張子。</param>
    /// <returns>ドット付き小文字の拡張子。</returns>
    private static string Normalize(string extension)
    {
        var ext = extension.Trim().ToLowerInvariant();
        return ext.StartsWith('.') ? ext : $".{ext}";
    }
}
