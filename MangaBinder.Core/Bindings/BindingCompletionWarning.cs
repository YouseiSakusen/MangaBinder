namespace MangaBinder.Bindings;

/// <summary>
/// 製本完了前に確認する警告の判定結果を保持します。
/// </summary>
/// <param name="HasMissingVolume">製本対象の巻に抜け巻があるかどうか。</param>
/// <param name="HasZipFileNameVolumeMismatch">ZIPファイル名の巻数表記が製本対象の巻範囲と一致していないかどうか。</param>
public readonly record struct BindingCompletionWarning(
	bool HasMissingVolume,
	bool HasZipFileNameVolumeMismatch)
{
	/// <summary>警告が存在するかどうかを取得します。</summary>
	public bool HasAny => this.HasMissingVolume || this.HasZipFileNameVolumeMismatch;
}
