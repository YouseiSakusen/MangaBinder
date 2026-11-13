using MangaBinder.Core.Formatters;
using MangaBinder.Series;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程で扱う作品と、その製本状態を保持するエンティティです。
/// </summary>
public sealed class BindingSeries
{
	/// <summary>
	/// <see cref="MangaSeriesStore"/> が所有する共有 <see cref="MangaSeriesNotifier"/> です。
	/// ここで生成・コピー・Dispose してはいけません。
	/// </summary>
	public MangaSeriesNotifier SeriesNotifier { get; init; } = default!;

	/// <summary>共有 Notifier が保持する正本の漫画作品情報。</summary>
	public MangaSeries Series => this.SeriesNotifier.Series.Value;

	/// <summary>製本開始キューの進行状態。</summary>
	public BindingStartStatus Status { get; init; }

	/// <summary>現在の製本工程番号。</summary>
	public int CurrentStep { get; init; }

	/// <summary>キューに追加した日時。</summary>
	public DateTime AddedAt { get; init; }

	/// <summary>最終更新日時。</summary>
	public DateTime UpdatedAt { get; init; }

	/// <summary>あらすじが存在するかどうかを示します。</summary>
	public bool HasSynopsis
		=> !string.IsNullOrWhiteSpace(this.Series.Description);

	/// <summary>
	/// StartPage 用のタグ表示テキストを取得します。
	/// </summary>
	public string TagDisplayText => SeriesTagDisplayFormatter.FormatForStartPage(this.Series.Tags);
}
