using R3;
using System.Diagnostics.CodeAnalysis;

namespace MangaBinder.Series;

/// <summary>
/// MangaSeries の Reactive な共有 ViewModel です。
/// アプリケーション全体で MangaSeries を参照する際に使用される薄いラッパーです。
/// 状態は最小限に抑え、MangaSeries インスタンス自体と Series.ForceNotify() による通知機構を活用します。
/// </summary>
public class MangaSeriesViewModel : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// ViewModel が保持する MangaSeries インスタンスを取得します。
	/// 同一インスタンスを各表示先で参照し、ForceNotify() で内容変更を通知します。
	/// </summary>
	public BindableReactiveProperty<MangaSeries> Series { get; }

	/// <summary>
	/// <see cref="MangaSeriesViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="series">保持する MangaSeries インスタンス。</param>
	public MangaSeriesViewModel(MangaSeries series)
	{
		this.Series = new BindableReactiveProperty<MangaSeries>(series)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// このビューモデルが保持するリソースを解放します。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
