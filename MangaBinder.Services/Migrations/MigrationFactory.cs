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

		this.addMigrations(migrations, "DB構造", storedDatabaseVersion, Constants.CurrentDatabaseVersion);
		this.addMigrations(migrations, "タイトル正規化", storedTitleNormalizationVersion, Constants.CurrentTitleNormalizationVersion);

		return migrations;
	}

	/// <summary>
	/// 1種類のバージョンについて、必要なマイグレーションを追加します。
	/// 新しいマイグレーションは、ここへ明示的に追加します。
	/// </summary>
	private void addMigrations(List<IMigration> migrations, string versionName, string storedVersion, string requiredVersion)
	{
		var stored = int.Parse(storedVersion);
		var required = int.Parse(requiredVersion);

		if (stored == required)
			return;

		if (stored > required)
			throw new InvalidOperationException($"{versionName}バージョンが、アプリの要求するバージョンより新しいため起動できません。保存：{storedVersion}　要求：{requiredVersion}");

		throw new InvalidOperationException($"{versionName}バージョン {storedVersion} から {requiredVersion} へのマイグレーションが存在しません。");
	}
}
