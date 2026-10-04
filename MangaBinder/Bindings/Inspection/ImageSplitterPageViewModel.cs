using HalationGhost.Wpf.Ui.Navigation;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using System.Windows.Media.Imaging;
using R3;
using Wpf.Ui;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 見開き分割画面の ViewModel です。
/// 編集用 Clone の準備は ImageSplitterManager、サムネイル・プレビューの BitmapSource 生成は本 ViewModel が担当します。
/// </summary>
public class ImageSplitterPageViewModel : INavigationDisposable, IDataInitializable
{
	private readonly BindingStore bindingStore;

	private readonly IServiceScopeFactory serviceScopeFactory;

	private readonly LoadingService loadingService;

	private readonly ObservableList<ImageSplitterThumbnailItemViewModel> thumbnailItemList = new();

	/// <summary>一覧表示用のサムネイル項目を取得します。</summary>
	public NotifyCollectionChangedSynchronizedViewList<ImageSplitterThumbnailItemViewModel> ThumbnailItems { get; }

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	private DisposableBag disposableBag;

	/// <summary>作品タイトルと作品カードの共通 ViewModel を取得します。</summary>
	public BindingSeriesViewModel BindingSeries { get; }

	/// <summary>
	/// 現在の編集用対象巻を取得します。View は Value.SplitSettings の ReactiveProperty へ直接 Binding します。
	/// BindingStore が所有するため、この ViewModel では Dispose しません。
	/// </summary>
	public BindableReactiveProperty<BindingVolume?> SplitTargetVolume => this.bindingStore.SplitTargetVolume;

	/// <summary>キャンセルコマンドを取得します。</summary>
	public ReactiveCommand CancelCommand { get; }

	/// <summary>分割実行コマンドを取得します。</summary>
	public ReactiveCommand ExecuteSplitCommand { get; }

	/// <summary>現在のプレビュー対象の ThumbnailItems 内インデックスを取得します。画像が0件の場合は -1 です。</summary>
	public BindableReactiveProperty<int> CurrentIndex { get; }

	/// <summary>現在のプレビュー対象を取得します。</summary>
	public BindableReactiveProperty<ImageSplitterThumbnailItemViewModel?> CurrentItem { get; }

	/// <summary>現在のファイル名を取得します。</summary>
	public BindableReactiveProperty<string> CurrentFileName { get; }

	/// <summary>現在位置表示（例：15 / 226）を取得します。</summary>
	public BindableReactiveProperty<string> CurrentPositionText { get; }

	/// <summary>現在のフルサイズプレビュー画像を取得します。</summary>
	public BindableReactiveProperty<BitmapSource?> PreviewSource { get; }

	/// <summary>前の画像へ移動するコマンドを取得します。</summary>
	public ReactiveCommand<Unit> PreviousImageCommand { get; }

	/// <summary>次の画像へ移動するコマンドを取得します。</summary>
	public ReactiveCommand<Unit> NextImageCommand { get; }

	/// <summary>
	/// <see cref="ImageSplitterPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	/// <param name="thumbnailImageLoader">サムネイル画像ローダー。</param>
	public ImageSplitterPageViewModel(
		INavigationService navigationService,
		BindingStore bindingStore,
		ThumbnailImageLoader thumbnailImageLoader,
		IServiceScopeFactory serviceScopeFactory,
		LoadingService loadingService)
	{
		this.navigationService = navigationService;
		this.bindingStore = bindingStore;
		this.serviceScopeFactory = serviceScopeFactory;
		this.loadingService = loadingService;

		this.ThumbnailItems = this.thumbnailItemList
			.CreateView(item => item)
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.BindingSeries = new BindingSeriesViewModel(bindingStore, thumbnailImageLoader)
			.AddTo(ref this.disposableBag);

		this.CurrentIndex = new BindableReactiveProperty<int>(-1)
			.AddTo(ref this.disposableBag);

		this.CurrentItem = new BindableReactiveProperty<ImageSplitterThumbnailItemViewModel?>(null)
			.AddTo(ref this.disposableBag);

		this.PreviewSource = new BindableReactiveProperty<BitmapSource?>(null)
			.AddTo(ref this.disposableBag);

		var itemCount = this.thumbnailItemList.ObserveCountChanged(notifyCurrentCount: true);

		this.CurrentFileName = this.CurrentItem
			.AsObservable()
			.Select(item => item?.FileName ?? string.Empty)
			.ToBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);

		this.CurrentPositionText = Observable
			.CombineLatest(this.CurrentIndex.AsObservable(), itemCount, (index, count) => $"{index + 1} / {count}")
			.ToBindableReactiveProperty("0 / 0")
			.AddTo(ref this.disposableBag);

		var canGoPrevious = this.CurrentIndex.AsObservable().Select(index => index > 0);
		var canGoNext = Observable
			.CombineLatest(this.CurrentIndex.AsObservable(), itemCount, (index, count) => index >= 0 && index < count - 1);

		this.PreviousImageCommand = new ReactiveCommand<Unit>(canGoPrevious, initialCanExecute: false)
			.AddTo(ref this.disposableBag);
		this.PreviousImageCommand
			.Subscribe(async _ => await this.showImageAsync(this.CurrentIndex.Value - 1))
			.AddTo(ref this.disposableBag);

		this.NextImageCommand = new ReactiveCommand<Unit>(canGoNext, initialCanExecute: false)
			.AddTo(ref this.disposableBag);
		this.NextImageCommand
			.Subscribe(async _ => await this.showImageAsync(this.CurrentIndex.Value + 1))
			.AddTo(ref this.disposableBag);

		this.CancelCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.CancelCommand
			.Subscribe(_ => this.cancel())
			.AddTo(ref this.disposableBag);

		this.ExecuteSplitCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.ExecuteSplitCommand
			.Subscribe(_ => this.executeSplit())
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 編集状態を終了して前の画面へ戻ります。
	/// </summary>
	private void cancel()
	{
		this.finishAndGoBack();
	}

	/// <summary>
	/// 分割実行の入口です。現時点では実画像処理を行わず、編集状態を終了して前の画面へ戻ります。
	/// </summary>
	private void executeSplit()
	{
		this.finishAndGoBack();
	}

	/// <summary>
	/// ImageSplitterManager の終了処理を通して SplitTargetVolume を null にし、前の画面へ戻ります。
	/// </summary>
	private void finishAndGoBack()
	{
		using (var scope = this.serviceScopeFactory.CreateScope())
		{
			scope.ServiceProvider.GetRequiredService<ImageSplitterManager>().Finish();
		}

		this.navigationService.GoBack();
	}

	/// <inheritdoc/>
	public async ValueTask InitializeDataAsync()
	{
		using (var managerScope = this.serviceScopeFactory.CreateScope())
		{
			managerScope.ServiceProvider.GetRequiredService<ImageSplitterManager>().Initialize();
		}

		var volume = this.bindingStore.SplitTargetVolume.Value
			?? throw new InvalidOperationException("BindingStore.SplitTargetVolume が null です。");

		using (this.loadingService.Begin("サムネイルを生成中..."))
		{
			using var scope = this.serviceScopeFactory.CreateScope();
			var loader = scope.ServiceProvider.GetRequiredService<ImageSplitterThumbnailLoader>();

			var images = volume.Images.ToArray();
			var thumbnails = await Task.WhenAll(images.Select(image => loader.LoadAsync(image)));

			var items = images
				.Select((image, index) => new ImageSplitterThumbnailItemViewModel(image, thumbnails[index]))
				.ToArray();

			this.thumbnailItemList.Clear();
			this.thumbnailItemList.AddRange(items);
		}

		if (this.thumbnailItemList.Count > 0)
		{
			await this.showImageAsync(0);
		}
	}

	/// <summary>
	/// 指定インデックスの画像を現在画像にし、PreviewSource をキャッシュ経由で反映します。
	/// </summary>
	/// <param name="index">ThumbnailItems 内のインデックス。</param>
	private async ValueTask showImageAsync(int index)
	{
		var item = this.thumbnailItemList[index];

		this.CurrentIndex.Value = index;
		this.CurrentItem.Value = item;

		if (item.PreviewSource is null)
		{
			using var scope = this.serviceScopeFactory.CreateScope();
			var loader = scope.ServiceProvider.GetRequiredService<ImageSplitterPreviewImageLoader>();
			item.PreviewSource = await loader.LoadAsync(item.Image);
		}

		if (this.CurrentItem.Value == item)
		{
			this.PreviewSource.Value = item.PreviewSource;
		}
	}

	/// <summary>
	/// ナビゲーション時に、1セッションで保持しているUI用画像参照とReactiveリソースを解放します。
	/// </summary>
	public void Dispose()
	{
		this.PreviewSource.Value = null;
		this.CurrentItem.Value = null;

		foreach (var item in this.thumbnailItemList)
		{
			item.PreviewSource = null;
		}

		this.thumbnailItemList.Clear();
		this.CurrentIndex.Value = -1;

		this.disposableBag.Dispose();
	}
}
