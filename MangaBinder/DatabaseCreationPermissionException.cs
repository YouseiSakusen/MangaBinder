namespace MangaBinder;

/// <summary>
/// DB ファイルの作成時に書き込み権限が原因で失敗したことを表す例外です。
/// </summary>
public class DatabaseCreationPermissionException : Exception
{
	/// <summary>作成に失敗した DB ファイルのパスを取得します。</summary>
	public string DatabasePath { get; }

	/// <summary>
	/// <see cref="DatabaseCreationPermissionException"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="databasePath">作成に失敗した DB ファイルのパス。</param>
	/// <param name="innerException">元の例外。</param>
	public DatabaseCreationPermissionException(string databasePath, Exception innerException)
		: base($"データベースファイルの作成に失敗しました。書き込み権限がありません: {databasePath}", innerException)
	{
		this.DatabasePath = databasePath;
	}
}
