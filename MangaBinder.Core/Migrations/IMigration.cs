namespace MangaBinder.Migrations;

/// <summary>
/// 個別マイグレーションが実装する共通インターフェースです。
/// DB構造変更・データ補正・バージョン更新は、各マイグレーションが同一トランザクションで実行します。
/// </summary>
public interface IMigration
{
	/// <summary>
	/// マイグレーションを非同期で実行します。
	/// </summary>
	Task ExecuteAsync();
}
