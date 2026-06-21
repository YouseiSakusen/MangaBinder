using System.Data.SQLite;
using System.Text;
using Dapper;

namespace MangaBinder.Bindings;

/// <summary>
/// Archive内部フォルダ構造のキャッシュを管理するリポジトリクラスです。
/// </summary>
public class MaterialArchiveRepository
{
	/// <summary>接続文字列。</summary>
	private readonly string connectionString;

	/// <summary>
	/// <see cref="MaterialArchiveRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="connectionString">SQLite接続文字列。</param>
	public MaterialArchiveRepository(string connectionString)
	{
		this.connectionString = connectionString;
	}

	/// <summary>
	/// MaterialArchives テーブルの行を表す DTO。Dapper マッピング用。
	/// </summary>
	private sealed class ArchiveRow
	{
		/// <summary>MaterialArchiveId。</summary>
		public long MaterialArchiveId { get; init; }

		/// <summary>SourceId。</summary>
		public long SourceId { get; init; }

		/// <summary>アーカイブファイルパス。</summary>
		public string ArchivePath { get; init; } = string.Empty;

		/// <summary>ファイルサイズ。</summary>
		public long FileSize { get; init; }

		/// <summary>最後の更新日時。</summary>
		public DateTime LastWriteTime { get; init; }

		/// <summary>Nested Archive フラグ（0/1）。</summary>
		public int IsNestedArchive { get; init; }
	}

	/// <summary>
	/// MaterialArchiveEntries テーブルの行を表す DTO。Dapper マッピング用。
	/// </summary>
	private sealed class ArchiveEntryRow
	{
		/// <summary>MaterialArchiveEntryId。</summary>
		public long MaterialArchiveEntryId { get; init; }

		/// <summary>MaterialArchiveId。</summary>
		public long MaterialArchiveId { get; init; }

		/// <summary>エントリパス。</summary>
		public string EntryPath { get; init; } = string.Empty;

		/// <summary>親エントリパス。</summary>
		public string? ParentEntryPath { get; init; }

		/// <summary>このエントリ配下の画像ファイル数。</summary>
		public int FileCount { get; init; }

		/// <summary>選択可能かどうか（0/1）。</summary>
		public int IsSelectable { get; init; }

		/// <summary>選択不可の理由。</summary>
		public string? SelectionDisabledReason { get; init; }

		/// <summary>アーカイブファイルが存在するかどうか（0/1）。</summary>
		public int HasArchiveFile { get; init; }

		/// <summary>画像ファイルの合計サイズ（バイト）。</summary>
		public long TotalImageBytes { get; init; }
	}

	/// <summary>
	/// 指定SourceIdに紐づくArchiveキャッシュをまとめて取得し、ArchivePath をキーとした辞書で返します。
	/// </summary>
	/// <param name="sourceId">SourceId。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>ArchivePath をキーとした辞書。値は MaterialArchiveId, FileSize, LastWriteTime, IsNestedArchive, Entries を含む。</returns>
	public async ValueTask<Dictionary<string, ArchiveCacheInfo>> GetArchivesBySourceIdAsync(
		long sourceId,
		CancellationToken cancellationToken)
	{
		var archivesSql = new StringBuilder();
		archivesSql.AppendLine(" SELECT ");
		archivesSql.AppendLine(" 	  MaterialArchiveId ");
		archivesSql.AppendLine(" 	, SourceId ");
		archivesSql.AppendLine(" 	, ArchivePath ");
		archivesSql.AppendLine(" 	, FileSize ");
		archivesSql.AppendLine(" 	, LastWriteTime ");
		archivesSql.AppendLine(" 	, IsNestedArchive ");
		archivesSql.AppendLine(" FROM ");
		archivesSql.AppendLine(" 	MaterialArchives ");
		archivesSql.AppendLine(" WHERE ");
		archivesSql.AppendLine(" 		SourceId = :SourceId ");

		var entriesSql = new StringBuilder();
		entriesSql.AppendLine(" SELECT ");
		entriesSql.AppendLine(" 	  MaterialArchiveEntryId ");
		entriesSql.AppendLine(" 	, MaterialArchiveId ");
		entriesSql.AppendLine(" 	, EntryPath ");
		entriesSql.AppendLine(" 	, ParentEntryPath ");
		entriesSql.AppendLine(" 	, FileCount ");
		entriesSql.AppendLine(" 	, IsSelectable ");
		entriesSql.AppendLine(" 	, SelectionDisabledReason ");
		entriesSql.AppendLine(" 	, HasArchiveFile ");
		entriesSql.AppendLine(" 	, TotalImageBytes ");
		entriesSql.AppendLine(" FROM ");
		entriesSql.AppendLine(" 	MaterialArchiveEntries ");
		entriesSql.AppendLine(" WHERE ");
		entriesSql.AppendLine(" 		MaterialArchiveId = :MaterialArchiveId ");
		entriesSql.AppendLine(" ORDER BY ");
		entriesSql.AppendLine(" 	EntryPath ");

		using var connection = new SQLiteConnection(this.connectionString);
		await connection.OpenAsync(cancellationToken);

		var archives = await connection.QueryAsync<ArchiveRow>(
			archivesSql.ToString(),
			new { SourceId = sourceId });

		var result = new Dictionary<string, ArchiveCacheInfo>(StringComparer.OrdinalIgnoreCase);

		foreach (var archive in archives)
		{
			var entries = await connection.QueryAsync<ArchiveEntryRow>(
				entriesSql.ToString(),
				new { MaterialArchiveId = archive.MaterialArchiveId });

			var entryList = entries.Select(e => new ArchiveEntryCacheInfo
			{
				EntryPath = e.EntryPath,
				ParentEntryPath = e.ParentEntryPath,
				FileCount = e.FileCount,
				IsSelectable = e.IsSelectable != 0,
				SelectionDisabledReason = e.SelectionDisabledReason ?? string.Empty,
				HasArchiveFile = e.HasArchiveFile != 0,
				TotalImageBytes = e.TotalImageBytes,
			}).ToList();

			var cacheInfo = new ArchiveCacheInfo
			{
				MaterialArchiveId = archive.MaterialArchiveId,
				ArchivePath = archive.ArchivePath,
				FileSize = archive.FileSize,
				LastWriteTime = archive.LastWriteTime,
				IsNestedArchive = archive.IsNestedArchive != 0,
				HasArchiveFile = entryList.Any(e => e.HasArchiveFile),
				Entries = entryList,
			};

			result[archive.ArchivePath] = cacheInfo;
		}

		return result;
	}

	/// <summary>
	/// Archive 1件分のキャッシュを保存します。
	/// 既存キャッシュがある場合は削除してから再INSERTします。
	/// </summary>
	/// <param name="seriesId">SeriesId。</param>
	/// <param name="sourceId">SourceId。</param>
	/// <param name="archiveFile">保存するアーカイブ情報。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	public async ValueTask SaveArchiveAsync(
		long seriesId,
		long sourceId,
		MaterialArchiveFile archiveFile,
		CancellationToken cancellationToken)
	{
		using var connection = new SQLiteConnection(this.connectionString);
		await connection.OpenAsync(cancellationToken);
		using var transaction = connection.BeginTransaction();

		try
		{
			// 既存キャッシュがあれば削除
			await this.deleteArchiveIfExistsAsync(connection, transaction, sourceId, archiveFile.ArchivePath, cancellationToken);

			// MaterialArchives にINSERT（MaterialArchiveId は AUTOINCREMENT）
			var insertArchiveSql = new StringBuilder();
			insertArchiveSql.AppendLine(" INSERT INTO MaterialArchives ( ");
			insertArchiveSql.AppendLine(" 	  SeriesId ");
			insertArchiveSql.AppendLine(" 	, SourceId ");
			insertArchiveSql.AppendLine(" 	, ArchivePath ");
			insertArchiveSql.AppendLine(" 	, FileSize ");
			insertArchiveSql.AppendLine(" 	, LastWriteTime ");
			insertArchiveSql.AppendLine(" 	, IsNestedArchive ");
			insertArchiveSql.AppendLine(" ) VALUES ( ");
			insertArchiveSql.AppendLine(" 	  :SeriesId ");
			insertArchiveSql.AppendLine(" 	, :SourceId ");
			insertArchiveSql.AppendLine(" 	, :ArchivePath ");
			insertArchiveSql.AppendLine(" 	, :FileSize ");
			insertArchiveSql.AppendLine(" 	, :LastWriteTime ");
			insertArchiveSql.AppendLine(" 	, :IsNestedArchive ");
			insertArchiveSql.AppendLine(" ) ");
			insertArchiveSql.AppendLine(" RETURNING MaterialArchiveId; ");

			var materialArchiveId = await connection.QuerySingleAsync<long>(
				insertArchiveSql.ToString(),
				new
				{
					SeriesId = seriesId,
					SourceId = sourceId,
					ArchivePath = archiveFile.ArchivePath,
					FileSize = archiveFile.FileSize,
					LastWriteTime = archiveFile.LastWriteTime,
					IsNestedArchive = archiveFile.IsNestedArchive ? 1 : 0,
				},
				transaction);

			// MaterialArchiveEntries にINSERT（フォルダエントリのみ）
			await this.insertEntriesAsync(connection, transaction, materialArchiveId, archiveFile.Folders, cancellationToken);

			transaction.Commit();
		}
		catch
		{
			transaction.Rollback();
			throw;
		}
	}

	/// <summary>
	/// Archive単位で削除します。
	/// </summary>
	/// <param name="sourceId">SourceId。</param>
	/// <param name="archivePath">削除対象のアーカイブパス。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	public async ValueTask DeleteArchiveAsync(
		long sourceId,
		string archivePath,
		CancellationToken cancellationToken)
	{
		using var connection = new SQLiteConnection(this.connectionString);
		await connection.OpenAsync(cancellationToken);
		using var transaction = connection.BeginTransaction();

		try
		{
			await this.deleteArchiveIfExistsAsync(connection, transaction, sourceId, archivePath, cancellationToken);
			transaction.Commit();
		}
		catch
		{
			transaction.Rollback();
			throw;
		}
	}

	/// <summary>
	/// 既存のArchiveキャッシュがある場合は削除します（内部用）。
	/// </summary>
	private async ValueTask deleteArchiveIfExistsAsync(
		SQLiteConnection connection,
		SQLiteTransaction transaction,
		long sourceId,
		string archivePath,
		CancellationToken cancellationToken)
	{
		var selectArchiveSql = new StringBuilder();
		selectArchiveSql.AppendLine(" SELECT ");
		selectArchiveSql.AppendLine(" 	MaterialArchiveId ");
		selectArchiveSql.AppendLine(" FROM ");
		selectArchiveSql.AppendLine(" 	MaterialArchives ");
		selectArchiveSql.AppendLine(" WHERE ");
		selectArchiveSql.AppendLine(" 		SourceId = :SourceId ");
		selectArchiveSql.AppendLine(" 	AND ArchivePath = :ArchivePath ");

		var materialArchiveId = await connection.QuerySingleOrDefaultAsync<long?>(
			selectArchiveSql.ToString(),
			new { SourceId = sourceId, ArchivePath = archivePath },
			transaction);

		if (materialArchiveId.HasValue)
		{
			var deleteEntriesSql = new StringBuilder();
			deleteEntriesSql.AppendLine(" DELETE FROM ");
			deleteEntriesSql.AppendLine(" 	MaterialArchiveEntries ");
			deleteEntriesSql.AppendLine(" WHERE ");
			deleteEntriesSql.AppendLine(" 	MaterialArchiveId = :MaterialArchiveId ");

			var deleteArchiveSql = new StringBuilder();
			deleteArchiveSql.AppendLine(" DELETE FROM ");
			deleteArchiveSql.AppendLine(" 	MaterialArchives ");
			deleteArchiveSql.AppendLine(" WHERE ");
			deleteArchiveSql.AppendLine(" 	MaterialArchiveId = :MaterialArchiveId ");

			await connection.ExecuteAsync(deleteEntriesSql.ToString(), new { MaterialArchiveId = materialArchiveId.Value }, transaction);
			await connection.ExecuteAsync(deleteArchiveSql.ToString(), new { MaterialArchiveId = materialArchiveId.Value }, transaction);
		}
	}

	/// <summary>
	/// ArchiveEntriesを再帰的にINSERTします。
	/// </summary>
	private async ValueTask insertEntriesAsync(
		SQLiteConnection connection,
		SQLiteTransaction transaction,
		long materialArchiveId,
		List<ArchiveFolderItem> folders,
		CancellationToken cancellationToken)
	{
		if (folders.Count == 0)
			return;

		var insertEntrySql = new StringBuilder();
		insertEntrySql.AppendLine(" INSERT INTO MaterialArchiveEntries ( ");
		insertEntrySql.AppendLine(" 	  MaterialArchiveId ");
		insertEntrySql.AppendLine(" 	, EntryPath ");
		insertEntrySql.AppendLine(" 	, ParentEntryPath ");
		insertEntrySql.AppendLine(" 	, FileCount ");
		insertEntrySql.AppendLine(" 	, IsSelectable ");
		insertEntrySql.AppendLine(" 	, SelectionDisabledReason ");
		insertEntrySql.AppendLine(" 	, HasArchiveFile ");
		insertEntrySql.AppendLine(" 	, TotalImageBytes ");
		insertEntrySql.AppendLine(" ) VALUES ( ");
		insertEntrySql.AppendLine(" 	  :MaterialArchiveId ");
		insertEntrySql.AppendLine(" 	, :EntryPath ");
		insertEntrySql.AppendLine(" 	, :ParentEntryPath ");
		insertEntrySql.AppendLine(" 	, :FileCount ");
		insertEntrySql.AppendLine(" 	, :IsSelectable ");
		insertEntrySql.AppendLine(" 	, :SelectionDisabledReason ");
		insertEntrySql.AppendLine(" 	, :HasArchiveFile ");
		insertEntrySql.AppendLine(" 	, :TotalImageBytes ");
		insertEntrySql.AppendLine(" ); ");

		foreach (var folder in folders)
		{
			cancellationToken.ThrowIfCancellationRequested();

			await connection.ExecuteAsync(
				insertEntrySql.ToString(),
				new
				{
					MaterialArchiveId = materialArchiveId,
					EntryPath = folder.EntryPath,
					ParentEntryPath = folder.ParentEntryPath == string.Empty ? null : folder.ParentEntryPath,
					FileCount = folder.FileCount,
					IsSelectable = folder.IsSelectable ? 1 : 0,
					SelectionDisabledReason = string.IsNullOrEmpty(folder.SelectionDisabledReason) ? null : folder.SelectionDisabledReason,
					HasArchiveFile = folder.HasArchiveFile ? 1 : 0,
					TotalImageBytes = folder.TotalImageBytes,
				},
				transaction);

			// 再帰的に子エントリをINSERT
			await this.insertEntriesAsync(connection, transaction, materialArchiveId, folder.Children, cancellationToken);
		}
	}

	/// <summary>
	/// Archiveキャッシュ情報。
	/// </summary>
	public class ArchiveCacheInfo
	{
		/// <summary>MaterialArchiveId。</summary>
		public long MaterialArchiveId { get; set; }

		/// <summary>アーカイブファイルパス。</summary>
		public string ArchivePath { get; set; } = string.Empty;

		/// <summary>ファイルサイズ。</summary>
		public long FileSize { get; set; }

		/// <summary>最後の更新日時。</summary>
		public DateTime LastWriteTime { get; set; }

		/// <summary>Nested Archive フラグ。</summary>
		public bool IsNestedArchive { get; set; }

		/// <summary>アーカイブファイルが存在するかどうか。</summary>
		public bool HasArchiveFile { get; set; }

		/// <summary>キャッシュ内エントリ一覧。</summary>
		public List<ArchiveEntryCacheInfo> Entries { get; set; } = [];
	}

	/// <summary>
	/// 作品の HasNestedArchive を更新します。
	/// </summary>
	/// <param name="series">更新対象の作品。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	public async Task UpdateMangaSeriesAsync(MangaSeries series, CancellationToken cancellationToken)
	{
		var updateSql = new StringBuilder();
		updateSql.AppendLine(" UPDATE MangaSeries ");
		updateSql.AppendLine(" SET HasNestedArchive = :HasNestedArchive ");
		updateSql.AppendLine(" WHERE SeriesId = :SeriesId ");

		using var connection = new SQLiteConnection(this.connectionString);
		await connection.OpenAsync(cancellationToken);

		await connection.ExecuteAsync(
			updateSql.ToString(),
			new
			{
				HasNestedArchive = series.HasNestedArchive ? 1 : 0,
				SeriesId = series.SeriesId,
			});
	}

	/// <summary>
	/// Archiveエントリキャッシュ情報。
	/// </summary>
	public class ArchiveEntryCacheInfo
	{
		/// <summary>エントリパス（/ で区切られた階層パス）。</summary>
		public string EntryPath { get; set; } = string.Empty;

		/// <summary>親エントリパス。</summary>
		public string? ParentEntryPath { get; set; }

		/// <summary>このエントリ配下の画像ファイル数。</summary>
		public int FileCount { get; set; }

		/// <summary>選択可能かどうか。</summary>
		public bool IsSelectable { get; set; }

		/// <summary>選択不可の理由。</summary>
		public string SelectionDisabledReason { get; set; } = string.Empty;

		/// <summary>アーカイブファイルが存在するかどうか。</summary>
		public bool HasArchiveFile { get; set; }

		/// <summary>このエントリ配下の画像ファイルの合計サイズ（バイト）。</summary>
		public long TotalImageBytes { get; set; }
	}
}
