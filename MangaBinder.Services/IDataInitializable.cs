namespace MangaBinder;

/// <summary>
/// 画面遷移後に初期データ読み込みを行う ViewModel を識別するためのインターフェースです。
/// </summary>
/// <remarks>
/// コンストラクタで重いデータ取得を行わず、ナビゲーション完了後に
/// <see cref="InitializeDataAsync"/> を呼び出してデータを非同期で読み込む設計を実現します。
/// </remarks>
public interface IDataInitializable
{
    /// <summary>
    /// 画面表示後の初期データ読み込みを非同期で実行します。
    /// </summary>
    /// <returns>非同期操作を表す <see cref="ValueTask"/>。</returns>
    ValueTask InitializeDataAsync();
}
