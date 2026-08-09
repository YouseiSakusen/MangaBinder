using R3;

namespace MangaBinder.Controls;

/// <summary>
/// メモ表示用の ViewModel です。
/// MangaSeries からの入力を受け取り、その変更を自動的にメモ表示値に反映します。
/// </summary>
public class SeriesMemoViewModel : IDisposable
{
	/// <summary>リソース管理用の DisposableBag。</summary>
	private DisposableBag disposableBag = new();

	/// <summary>
	/// 表示対象となる MangaSeries を取得・設定します。
	/// 値が変更（または ForceNotify() が呼ばれた）際に、表示値が自動更新されます。
	/// </summary>
	public BindableReactiveProperty<MangaSeries?> Series { get; }

	/// <summary>メモが存在するかどうかを示します。</summary>
	public IReadOnlyBindableReactiveProperty<bool> HasMemo { get; }

	/// <summary>メモの本文です。</summary>
	public IReadOnlyBindableReactiveProperty<string?> Memo { get; }

	/// <summary>
	/// <see cref="SeriesMemoViewModel"/> の新しいインスタンスを初期化します。
	/// 初期状態は空のメモ情報を持ちます。
	/// </summary>
	public SeriesMemoViewModel()
	{
		this.Series = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);

		// HasMemo: Series が存在し、Memo が null / 空文字 / 空白のみではない場合に true
		this.HasMemo = this.Series
			.Select(series => series != null && !string.IsNullOrWhiteSpace(series.Memo))
			.ToReadOnlyBindableReactiveProperty(false)
			.AddTo(ref this.disposableBag);

		// Memo: Series の Memo を返す
		this.Memo = this.Series
			.Select(series => series?.Memo)
			.ToReadOnlyBindableReactiveProperty(null)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// <see cref="MangaSeries"/> から <see cref="SeriesMemoViewModel"/> を生成します。
	/// このメソッドは従来の利用方法との互換性を保つため提供されています。
	/// </summary>
	/// <param name="series">変換元の MangaSeries。</param>
	/// <returns>生成された ViewModel。Series プロパティに series が設定された状態。</returns>
	public static SeriesMemoViewModel FromSeries(MangaSeries series)
	{
		var viewModel = new SeriesMemoViewModel();
		viewModel.Series.Value = series;
		return viewModel;
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
