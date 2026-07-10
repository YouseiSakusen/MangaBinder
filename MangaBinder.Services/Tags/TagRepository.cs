using Dapper;
using MangaBinder.Settings;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder.Tags;

/// <summary>
/// MangaTags テーブルのタグ定義を操作する Repository クラスです。
/// DB への即時反映を行います。
/// </summary>
public sealed class TagRepository
{
	private readonly AppSettings appSettings;

	/// <summary>
	/// <see cref="TagRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	public TagRepository(AppSettings appSettings)
	{
		this.appSettings = appSettings;
	}

	/// <summary>
	/// 登録済みタグ一覧を DB から取得します。
	/// タグ一覧の並び順は MangaSeriesStore が管理するため、ここでは並び順を指定しません。
	/// </summary>
	/// <returns>タグ定義の読み取り専用リスト。</returns>
	public IReadOnlyList<MangaTag> GetAll()
	{
		var sql = new StringBuilder();
		sql.AppendLine(" SELECT ");
		sql.AppendLine(" 	  TagId ");
		sql.AppendLine(" 	, Name ");
		sql.AppendLine(" 	, DisplayOrder ");
		sql.AppendLine(" 	, ShowOnSeriesCard ");
		sql.AppendLine(" FROM ");
		sql.AppendLine(" 	MangaTags; ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		connection.Open();

		var rows = connection.Query<MangaTagRow>(sql.ToString()).ToList();
		var tags = rows
			.Select(r => new MangaTag
			{
				TagId = r.TagId,
				Name = r.Name,
				DisplayOrder = r.DisplayOrder,
				ShowOnSeriesCard = r.ShowOnSeriesCard != 0,
			})
			.ToList();

		return tags.AsReadOnly();
	}

	/// <summary>
	/// タグを DB に挿入し、採番済みの MangaTag を返します。
	/// </summary>
	/// <param name="name">タグ名。</param>
	/// <param name="displayOrder">表示順。</param>
	/// <param name="showOnSeriesCard">作品カードに表示するかどうか。</param>
	/// <returns>採番済み TagId を持つ MangaTag。</returns>
	public MangaTag Insert(string name, int displayOrder, bool showOnSeriesCard = true)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" INSERT INTO MangaTags ( ");
		sql.AppendLine(" 	  Name ");
		sql.AppendLine(" 	, DisplayOrder ");
		sql.AppendLine(" 	, ShowOnSeriesCard ");
		sql.AppendLine(" ) VALUES ( ");
		sql.AppendLine(" 	  :Name ");
		sql.AppendLine(" 	, :DisplayOrder ");
		sql.AppendLine(" 	, :ShowOnSeriesCard ");
		sql.AppendLine(" ); ");
		sql.AppendLine(" SELECT last_insert_rowid() as TagId; ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		connection.Open();

		var tagId = connection.QuerySingle<long>(sql.ToString(), new
		{
			Name = name,
			DisplayOrder = displayOrder,
			ShowOnSeriesCard = showOnSeriesCard ? 1 : 0,
		});

		return new MangaTag
		{
			TagId = tagId,
			Name = name,
			DisplayOrder = displayOrder,
			ShowOnSeriesCard = showOnSeriesCard,
		};
	}

	/// <summary>
	/// 指定したタグ ID のタグをリネームします。
	/// </summary>
	/// <param name="tagId">リネーム対象のタグ ID。</param>
	/// <param name="newName">新しいタグ名。</param>
	/// <returns>更新後の MangaTag。見つからない場合は null。</returns>
	public MangaTag? Rename(long tagId, string newName)
	{
		var selectSql = new StringBuilder();
		selectSql.AppendLine(" SELECT ");
		selectSql.AppendLine(" 	  TagId ");
		selectSql.AppendLine(" 	, Name ");
		selectSql.AppendLine(" 	, DisplayOrder ");
		selectSql.AppendLine(" 	, ShowOnSeriesCard ");
		selectSql.AppendLine(" FROM ");
		selectSql.AppendLine(" 	MangaTags ");
		selectSql.AppendLine(" WHERE ");
		selectSql.AppendLine(" 	TagId = :TagId; ");

		var updateSql = new StringBuilder();
		updateSql.AppendLine(" UPDATE MangaTags ");
		updateSql.AppendLine(" SET Name = :Name ");
		updateSql.AppendLine(" WHERE TagId = :TagId; ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		connection.Open();

		var existing = connection.QueryFirstOrDefault<MangaTagRow>(selectSql.ToString(), new { TagId = tagId });
		if (existing is null)
			return null;

		connection.Execute(updateSql.ToString(), new { TagId = tagId, Name = newName });

		return new MangaTag
		{
			TagId = existing.TagId,
			Name = newName,
			DisplayOrder = existing.DisplayOrder,
			ShowOnSeriesCard = existing.ShowOnSeriesCard != 0,
		};
	}

	/// <summary>
	/// 指定したタグ ID のタグを削除します。
	/// MangaSeriesTags の関連レコードを先に削除してから MangaTags を削除します。
	/// </summary>
	/// <param name="tagId">削除するタグの ID。</param>
	public void Delete(long tagId)
	{
		var deleteSeriesTagsSql = new StringBuilder();
		deleteSeriesTagsSql.AppendLine(" DELETE FROM MangaSeriesTags ");
		deleteSeriesTagsSql.AppendLine(" WHERE ");
		deleteSeriesTagsSql.AppendLine(" 	TagId = :TagId; ");

		var deleteTagsSql = new StringBuilder();
		deleteTagsSql.AppendLine(" DELETE FROM MangaTags ");
		deleteTagsSql.AppendLine(" WHERE ");
		deleteTagsSql.AppendLine(" 	TagId = :TagId; ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		connection.Open();

		using var transaction = connection.BeginTransaction();
		try
		{
			// 1. MangaSeriesTags から関連レコードを削除
			connection.Execute(deleteSeriesTagsSql.ToString(), new { TagId = tagId }, transaction);

			// 2. MangaTags からタグ定義を削除
			connection.Execute(deleteTagsSql.ToString(), new { TagId = tagId }, transaction);

			transaction.Commit();
		}
		catch
		{
			transaction.Rollback();
			throw;
		}
	}

	/// <summary>Dapper マッピング用の MangaTags 行モデル。</summary>
	private sealed class MangaTagRow
	{
		public long TagId { get; init; }
		public string Name { get; init; } = string.Empty;
		public int DisplayOrder { get; init; }
		public int ShowOnSeriesCard { get; init; }
	}
}
