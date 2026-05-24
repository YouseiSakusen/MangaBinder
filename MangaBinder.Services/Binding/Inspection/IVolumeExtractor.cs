namespace MangaBinder.Binding.Inspection;

/// <summary>
/// 製本用ボリュームからページ順に画像を列挙するインターフェースです。
/// </summary>
public interface IVolumeExtractor
{
	/// <summary>
	/// ボリュームのページをファイル名順に列挙し、<paramref name="onPageAsync"/> を呼び出します。
	/// 保存処理は行いません。Stream の Dispose は呼び出し元が行います。
	/// </summary>
	/// <param name="volume">対象ボリューム情報。</param>
	/// <param name="onPageAsync">ページごとに呼び出されるコールバック。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	ValueTask ExtractPagesAsync(
		BindingSourceVolume volume,
		Func<BindingPageSource, ValueTask> onPageAsync,
		CancellationToken cancellationToken = default);
}
