using MangaBinder.Settings;

namespace MangaBinder.Series;

/// <summary>
/// 保存前確認の結果を表すクラスです。
/// 確認種別と確認に必要なデータを保持します。
/// </summary>
public class SaveSeriesConfirmationResult
{
	/// <summary>
	/// 確認種別を取得します。
	/// </summary>
	public SaveSeriesConfirmationType ConfirmationType { get; init; }

	/// <summary>
	/// 確認に必要なデータを取得します。
	/// 確認種別に応じた型でキャストして使用してください。
	/// </summary>
	public object? ConfirmationData { get; init; }

	/// <summary>
	/// <see cref="SaveSeriesConfirmationResult"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="confirmationType">確認種別。</param>
	/// <param name="confirmationData">確認に必要なデータ。</param>
	public SaveSeriesConfirmationResult(SaveSeriesConfirmationType confirmationType, object? confirmationData = null)
	{
		this.ConfirmationType = confirmationType;
		this.ConfirmationData = confirmationData;
	}

	/// <summary>
	/// 確認データを指定された型として取得します。
	/// 型が一致しない場合は null を返します。
	/// </summary>
	/// <typeparam name="T">確認データの型。</typeparam>
	/// <returns>指定された型にキャストされたデータ。型が一致しない場合は null。</returns>
	public T? GetConfirmationDataAs<T>() where T : class
	{
		return this.ConfirmationData as T;
	}
}
