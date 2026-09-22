namespace MangaBinder.Bindings;

/// <summary>
/// 巻選択工程の検証結果を保持するクラスです。
/// </summary>
public class VolumeSelectionValidationResult
{
	/// <summary>
	/// 検証エラーを取得します。
	/// </summary>
	public VolumeSelectionValidationError Error { get; }

	/// <summary>
	/// 検証警告を取得します。
	/// </summary>
	public VolumeSelectionValidationWarning Warning { get; }

	/// <summary>
	/// 重複している巻番号の一覧を取得します。
	/// </summary>
	public IReadOnlyList<decimal> DuplicateVolumeNumbers { get; }

	/// <summary>
	/// エラーが存在するかどうかを取得します。
	/// </summary>
	public bool HasError => this.Error != VolumeSelectionValidationError.None;

	/// <summary>
	/// 警告が存在するかどうかを取得します。
	/// </summary>
	public bool HasWarning => this.Warning != VolumeSelectionValidationWarning.None;

	/// <summary>
	/// <see cref="VolumeSelectionValidationResult"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="error">検証エラー。</param>
	/// <param name="warning">検証警告。</param>
	/// <param name="duplicateVolumeNumbers">重複している巻番号の一覧。</param>
	public VolumeSelectionValidationResult(
		VolumeSelectionValidationError error = VolumeSelectionValidationError.None,
		VolumeSelectionValidationWarning warning = VolumeSelectionValidationWarning.None,
		IReadOnlyList<decimal>? duplicateVolumeNumbers = null)
	{
		this.Error = error;
		this.Warning = warning;
		this.DuplicateVolumeNumbers = duplicateVolumeNumbers ?? Array.Empty<decimal>();
	}
}
