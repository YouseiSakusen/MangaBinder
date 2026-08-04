using System.Data.SQLite;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace MangaBinder;

/// <summary>
/// Generic Host 作成前に呼び出されるアプリ初期化クラスです。
/// DBファイルおよびサムネフォルダの存在を保証します。
/// </summary>
public class MangaBinderInitializer
{
    private readonly IConfiguration config;

    /// <summary>
    /// <see cref="MangaBinderInitializer"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="config">アプリケーション設定。</param>
    public MangaBinderInitializer(IConfiguration config)
        => this.config = config;

    /// <summary>
    /// アプリ初回起動時に必要な永続リソースを非同期で保証します。
    /// </summary>
    public async Task InitializeAsync()
    {
        this.deleteOldLogFiles();
        var connectionString = this.ensureDatabaseExists();
        var thumbnailPath = await this.getThumbnailDirectoryPath(connectionString);
        this.ensureThumbnailDirectoryExists(thumbnailPath);
        this.ensureWorkThumbnailDirectoryExists(thumbnailPath);
        this.ensureSpecialThumbnailFilesExist(thumbnailPath);
    }

    /// <summary>
    /// DBファイルの存在を保証し、接続文字列を返します。
    /// ファイルが存在しない場合は template.db からコピーして生成します。
    /// </summary>
    /// <returns>SQLite 接続文字列。</returns>
    private string ensureDatabaseExists()
    {
        var relativePath = this.config["Database:RelativePath"]!;
        var dbPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        var dbDir = Path.GetDirectoryName(dbPath)!;

        if (!File.Exists(dbPath))
        {
            Directory.CreateDirectory(dbDir);
            var templatePath = Path.Combine(AppContext.BaseDirectory, "template.db");
            File.Copy(templatePath, dbPath);
        }

        return new SQLiteConnectionStringBuilder
        {
            DataSource = dbPath,
            JournalMode = SQLiteJournalModeEnum.Wal,
            BusyTimeout = 5000,
        }.ToString();
    }

    /// <summary>
    /// AppSettings テーブルから ThumbnailFolderPath を取得し、絶対パスに解決して返します。
    /// </summary>
    /// <param name="connectionString">SQLite 接続文字列。</param>
    /// <returns>絶対パスに解決された ThumbnailFolderPath。</returns>
    private async Task<string> getThumbnailDirectoryPath(string connectionString)
    {
        using var connection = new SQLiteConnection(connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ThumbnailFolderPath FROM AppSettings LIMIT 1;";

        var result = await command.ExecuteScalarAsync();
        var rawPath = result as string ?? string.Empty;

        if (string.IsNullOrWhiteSpace(rawPath))
            return string.Empty;

        if (Path.IsPathRooted(rawPath))
            return rawPath;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rawPath));
    }

    /// <summary>
    /// サムネフォルダが存在しない場合に作成します。
    /// </summary>
    /// <param name="thumbnailPath">サムネフォルダのパス。</param>
    private void ensureThumbnailDirectoryExists(string thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath))
            return;

        Directory.CreateDirectory(thumbnailPath);
    }

    /// <summary>
    /// ワークサムネイルフォルダが存在しない場合に作成します。
    /// </summary>
    /// <param name="thumbnailPath">サムネフォルダのパス。</param>
    private void ensureWorkThumbnailDirectoryExists(string thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath))
            return;

        var workThumbnailPath = Path.Combine(thumbnailPath, "wk");
        Directory.CreateDirectory(workThumbnailPath);
    }

    /// <summary>
    /// Assets フォルダの特殊サムネイル画像を ThumbnailFolderPath へ補完コピーします。
    /// コピー先に同名ファイルが存在する場合はスキップします。
    /// </summary>
    /// <param name="thumbnailPath">コピー先のサムネフォルダのパス。</param>
    private void ensureSpecialThumbnailFilesExist(string thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath))
            return;

        var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
        if (!Directory.Exists(assetsPath))
            return;

        string[] specialFiles =
        [
            "00000!_limit-exceeded.jpg",
            "00000!_none.jpg",
            "00000!_failed.jpg",
            "00000!_nested-archive.jpg",
        ];

        foreach (var fileName in specialFiles)
        {
            var source = Path.Combine(assetsPath, fileName);
            if (!File.Exists(source))
                continue;

            var destination = Path.Combine(thumbnailPath, fileName);
            if (!File.Exists(destination))
                File.Copy(source, destination);
        }
    }

    /// <summary>
    /// 設定値で指定された保持期間を超えた古いログファイルを削除します。
    /// app_yyyyMMdd_sequence.log 形式のファイルのみを対象とします。
    /// </summary>
    private void deleteOldLogFiles()
    {
        var retentionDays = this.config.GetValue<int>("Logging:RetentionDays", 14);
        var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");

        if (!Directory.Exists(logsPath))
            return;

        var files = Directory.GetFiles(logsPath, "app_*.log");

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileName(filePath);

            if (!this.tryExtractLogDate(fileName, out var logDate))
                continue;

            var now = DateTime.Now.Date;
            var daysOld = (now - logDate).Days;

            if (daysOld > retentionDays)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// ログファイル名 (app_yyyyMMdd_sequence.log) から生成日を抽出します。
    /// ファイル名形式が一致しない場合は false を返します。
    /// </summary>
    /// <param name="fileName">ログファイル名。</param>
    /// <param name="logDate">抽出された生成日 (UTC 日付)。</param>
    /// <returns>抽出成功時は true、失敗時は false。</returns>
    private bool tryExtractLogDate(string fileName, out DateTime logDate)
    {
        logDate = default;

        const string prefix = "app_";
        if (!fileName.StartsWith(prefix))
            return false;

        var withoutPrefix = fileName.Substring(prefix.Length);
        var datePartEnd = withoutPrefix.IndexOf('_');

        if (datePartEnd < 0)
            return false;

        var datePart = withoutPrefix.Substring(0, datePartEnd);

        if (DateTime.TryParseExact(datePart, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
        {
            logDate = parsedDate;
            return true;
        }

        return false;
    }
}
