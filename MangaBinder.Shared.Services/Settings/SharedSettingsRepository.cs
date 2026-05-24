using Dapper;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder.Settings;

/// <summary>
/// UI・Worker 共用の設定リポジトリクラスです。
/// </summary>
public class SharedSettingsRepository
{
    /// <summary>SQLite データベースへの接続文字列。</summary>
    private readonly string connectionString;

    /// <summary>
    /// <see cref="SharedSettingsRepository"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="appSettings">接続文字列の取得元となるアプリケーション設定。</param>
    public SharedSettingsRepository(string connectionString)
        => this.connectionString = connectionString;

    /// <summary>
    /// SupportedFileExtensions テーブルから全件を非同期で取得します。
    /// </summary>
    public async ValueTask<IReadOnlyList<SupportedFileExtension>> GetSupportedFileExtensionsAsync()
    {
        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine(" 	  Extension ");
        sql.AppendLine(" 	, FileType ");
        sql.AppendLine(" 	, RequiresConversion ");
        sql.AppendLine(" FROM ");
        sql.AppendLine(" 	SupportedFileExtensions; ");

        using var connection = new SQLiteConnection(this.connectionString);
        var result = await connection.QueryAsync<SupportedFileExtension>(sql.ToString());
        return result.AsList();
    }

    /// <summary>
    /// AppSettings テーブルからサムネイル関連設定を非同期で取得します。
    /// </summary>
    public async ValueTask<ThumbnailSettingsRow> GetThumbnailSettingsAsync()
    {
        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine("     ThumbnailFolderPath ");
        sql.AppendLine("   , ThumbnailWidth ");
        sql.AppendLine("   , ThumbnailHeight ");
        sql.AppendLine("   , ThumbnailJpegQuality ");
        sql.AppendLine("   , ThumbnailBackgroundColor ");
        sql.AppendLine(" FROM ");
        sql.AppendLine("     AppSettings ");
        sql.AppendLine(" LIMIT 1; ");

        using var connection = new SQLiteConnection(this.connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<ThumbnailSettingsRow>(sql.ToString());
        return row ?? new ThumbnailSettingsRow(string.Empty, 0, 0, 0, string.Empty);
    }

    /// <summary>サムネイル設定の取得結果を表すレコードです。</summary>
    public sealed record ThumbnailSettingsRow(
        string ThumbnailFolderPath,
        long ThumbnailWidth,
        long ThumbnailHeight,
        long ThumbnailJpegQuality,
        string ThumbnailBackgroundColor);

    /// <summary>
    /// GoogleBooksExcludeWords テーブルから有効な除外ワードを非同期で取得します。
    /// </summary>
    public async ValueTask<IReadOnlyList<string>> GetGoogleBooksExcludeWordsAsync(CancellationToken cancellationToken = default)
    {
        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine("     Word ");
        sql.AppendLine(" FROM ");
        sql.AppendLine("     GoogleBooksExcludeWords ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine("     IsEnabled = 1 ");
        sql.AppendLine(" ORDER BY WordId; ");

        using var connection = new SQLiteConnection(this.connectionString);
        var rows = await connection.QueryAsync<string>(sql.ToString());
        return rows.Where(w => !string.IsNullOrWhiteSpace(w)).ToList();
    }

    /// <summary>
    /// SourceFolders テーブルから全件を非同期で取得します。
    /// </summary>
    public async ValueTask<IReadOnlyList<SourceFolderRecord>> GetSourceFoldersAsync()
    {
        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine(" 	  FolderPath ");
        sql.AppendLine(" 	, DisplayName ");
        sql.AppendLine(" 	, Role ");
        sql.AppendLine(" FROM ");
        sql.AppendLine(" 	SourceFolders; ");

        using var connection = new SQLiteConnection(this.connectionString);
        var rows = await connection.QueryAsync<(string FolderPath, string DisplayName, int Role)>(sql.ToString());
        return rows.Select(r => new SourceFolderRecord(r.FolderPath, r.DisplayName, (FolderRole)r.Role))
            .ToList();
    }
}
