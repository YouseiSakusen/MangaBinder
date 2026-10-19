using HalationGhost.Wpf.Ui;
using MangaBinder.Controls;
using R3;
using Wpf.Ui;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程の画面 ViewModel の共通基底クラスです。
/// 作品カード、対象作品、マウスサイドボタンの戻る処理を提供します。
/// </summary>
public abstract class BindingPageViewModelBase : IDisposable, IBackRequestHandler
{
	/// <summary>製本工程の正本状態ストア。</summary>
	protected readonly BindingStore bindingStore;

	/// <summary>ナビゲーションサービス。</summary>
	protected readonly INavigationService navigationService;

	/// <summary>基底クラスが所有するリソースの DisposableBag。</summary>
	private DisposableBag baseDisposableBag;

	/// <summary>作品カード用 ViewModel を取得します。</summary>
	public MangaSeriesCardViewModel MangaSeriesCard { get; }

	/// <summary>BindingStore が所有する共有 MangaSeriesViewModel を取得します。</summary>
	public BindableReactiveProperty<MangaBinder.Series.MangaSeriesViewModel?> BindingTargetViewModel => this.bindingStore.BindingTargetViewModel;

	/// <summary>マウスサイドボタンで戻る処理を有効にするかどうかを取得または設定します。</summary>
	protected bool IsMouseBackEnabled { get; set; } = true;

	/// <summary>
	/// <see cref="BindingPageViewModelBase"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="thumbnailImageLoader">サムネイル画像ローダー。</param>
	protected BindingPageViewModelBase(
		BindingStore bindingStore,
		INavigationService navigationService,
		ThumbnailImageLoader thumbnailImageLoader)
	{
		this.bindingStore = bindingStore;
		this.navigationService = navigationService;

		this.MangaSeriesCard = new MangaSeriesCardViewModel(this.bindingStore.BindingTargetViewModel, thumbnailImageLoader)
			.AddTo(ref this.baseDisposableBag);
	}

	/// <inheritdoc/>
	public ValueTask OnBackRequestedAsync()
	{
		if (this.IsMouseBackEnabled)
		{
			this.navigationService.GoBack();
		}

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// 基底クラスが所有するリソースを解放します。派生クラスは自身の解放後に base.Dispose() を呼び出してください。
	/// </summary>
	public virtual void Dispose()
	{
		this.baseDisposableBag.Dispose();
	}
}
