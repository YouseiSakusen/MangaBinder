using R3;

namespace MangaBinder.Controls;

/// <summary>
/// 作品最終更新日時表示用の ViewModel です。
/// MangaSeries からの入力を受け取り、その変更を自動的に表示値に反映します。
/// </summary>
public class SeriesLastUpdateViewModel : IDisposable
{
	/// <summary>リソース管理用の DisposableBag。</summary>
	private DisposableBag disposableBag = new();

	/// <summary>
	/// 表示対象となる MangaSeries を取得・設定します。
	/// 値が変更（または ForceNotify() が呼ばれた）際に、表示値が自動更新されます。
	/// </summary>
	public BindableReactiveProperty<MangaSeries?> Series { get; }

	/// <summary>最終編集日時が存在するかどうかを示します。</summary>
	public IReadOnlyBindableReactiveProperty<bool> HasLastUpdate { get; }

	/// <summary>最終編集日時をテキスト形式（yyyy/MM/dd HH:mm）で表します。</summary>
	public IReadOnlyBindableReactiveProperty<string> LastUpdate { get; }

	/// <summary>
	/// <see cref="SeriesLastUpdateViewModel"/> の新しいインスタンスを初期化します。
	/// 初期状態は空の日時情報を持ちます。
	/// </summary>
	public SeriesLastUpdateViewModel()
	{
		this.Series = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);

		// HasLastUpdate: Series が存在し、ManuallyEditedAt が null ではない場合に true
		this.HasLastUpdate = this.Series
			.Select(series => series != null && series.ManuallyEditedAt.HasValue)
			.ToReadOnlyBindableReactiveProperty(false)
			.AddTo(ref this.disposableBag);

		// LastUpdate: Series の ManuallyEditedAt を "yyyy/MM/dd HH:mm" 形式で返す
		this.LastUpdate = this.Series
			.Select(series => series?.ManuallyEditedAt?.ToString("yyyy/MM/dd HH:mm") ?? string.Empty)
			.ToReadOnlyBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
