namespace MangaBinder.Series;

/// <summary>
/// 作品編集画面へ渡す編集対象を保持する Singleton ストアです。
/// 画面間で共有する作品編集対象の受け渡しを担当します。
/// </summary>
public class EditStore
{
	/// <summary>編集対象作品を取得または設定します。</summary>
	public MangaSeries? EditTarget { get; set; }

	/// <summary>
	/// 作品カードへ渡す Store 所有の共有 MangaSeriesViewModel を取得または設定します。
	/// 設定する場合、Series.Value は EditTarget と同一インスタンスである必要があります。
	/// </summary>
	public MangaSeriesViewModel? EditTargetViewModel { get; set; }
}
