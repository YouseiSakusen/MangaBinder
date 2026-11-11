namespace MangaBinder.Migrations;

/// <summary>
/// 保存されているバージョンとアプリが要求するバージョンから、必要なマイグレーションを選択します。
/// マイグレーションはリフレクションやDIを使わず、ここへ明示的に追加します。
/// </summary>
public class MigrationFactory
{
	private readonly string connectionString;

	/// <summary>
	/// <see cref="MigrationFactory"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="connectionString">SQLite 接続文字列。</param>
	public MigrationFactory(string connectionString)
		=> this.connectionString = connectionString;

	/// <summary>
	/// 必要なマイグレーションを実行順のリストで返します。
	/// </summary>
	/// <param name="storedDatabaseVersion">保存されているDB構造バージョン。</param>
	/// <param name="storedTitleNormalizationVersion">保存されているタイトル正規化バージョン。</param>
	/// <returns>実行順のマイグレーション一覧。更新不要の場合は空。</returns>
	/// <exception cref="InvalidOperationException">保存バージョンが要求より新しい場合、または対応するマイグレーションが存在しない場合。</exception>
	public IReadOnlyList<IMigration> Create(string storedDatabaseVersion, string storedTitleNormalizationVersion)
	{
		var migrations = new List<IMigration>();

		// DB構造バージョン
		switch ((storedDatabaseVersion, Constants.CurrentDatabaseVersion))
		{
			case ("0", "0"):
				break;

			default:
				throw new InvalidOperationException(
					$"対応するDB構造マイグレーションがありません。保存：{storedDatabaseVersion} 要求：{Constants.CurrentDatabaseVersion}");
		}

		// タイトル正規化バージョン
		switch ((storedTitleNormalizationVersion, Constants.CurrentTitleNormalizationVersion))
		{
			case ("0", "0"):
			case ("1", "1"):
				break;

			case ("0", "1"):
				migrations.Add(new Migration0_1(this.connectionString));
				break;

			default:
				throw new InvalidOperationException(
					$"対応するタイトル正規化マイグレーションがありません。保存：{storedTitleNormalizationVersion} 要求：{Constants.CurrentTitleNormalizationVersion}");
		}

		return migrations;
	}
}
