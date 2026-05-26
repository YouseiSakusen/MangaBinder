using MangaBinder.Binding;
using R3;
using Wpf.Ui;

namespace MangaBinder.Binding.Prepress;

/// <summary>
/// <see cref="SpreadSplitterPage"/> の ViewModel です。
/// </summary>
public class SpreadSplitterPageViewModel : IDisposable, IDataInitializable
{
/// <summary>前後キャッシュ幅。</summary>
private const int CacheRange = 10;

/// <summary>作品選択状態ストア。</summary>
private readonly SeriesWorkspaceStore workspaceStore;

/// <summary>ナビゲーションサービス。</summary>
private readonly INavigationService navigationService;

/// <summary>画像バイト列読み込み担当。</summary>
private readonly SpreadSplitterImageLoader imageLoader;

private DisposableBag disposableBag;

/// <summary>全 PrepressImageItem のリスト（BitmapImage は保持しない）。</summary>
private List<PrepressImageItem> images = [];

/// <summary>近傍キャッシュ。キーはインデックス。</summary>
private readonly Dictionary<int, SpreadSplitterDisplayItem> imageCache = [];

/// <summary>現在表示中のインデックス。</summary>
private int currentIndex;

/// <summary>パンくずおよびヘッダーに表示するページタイトルを取得します。</summary>
public BindableReactiveProperty<string> PageTitle { get; }

/// <summary>作品タイトルを取得します。</summary>
public BindableReactiveProperty<string> SeriesTitle { get; }

/// <summary>ページ位置テキスト（例: 15 / 104）を取得します。</summary>
public BindableReactiveProperty<string> PagePositionText { get; }

/// <summary>現在表示中のファイル名を取得します。</summary>
public BindableReactiveProperty<string> CurrentFileName { get; }

/// <summary>現在表示中の DisplayItem を取得します。</summary>
public BindableReactiveProperty<SpreadSplitterDisplayItem?> CurrentDisplayItem { get; }

/// <summary>現在画像が分割対象かどうかを取得または設定します。</summary>
public BindableReactiveProperty<bool> IsSplitTarget { get; }

/// <summary>画像ロード中かどうかを取得します。</summary>
public BindableReactiveProperty<bool> IsLoading { get; }

/// <summary>前の画像ボタンが有効かどうかを取得します。</summary>
public BindableReactiveProperty<bool> CanGoPrevious { get; }

/// <summary>次の画像ボタンが有効かどうかを取得します。</summary>
public BindableReactiveProperty<bool> CanGoNext { get; }

/// <summary>前の画像に移動するコマンドを取得します。</summary>
public ReactiveCommand PreviousImageCommand { get; }

/// <summary>次の画像に移動するコマンドを取得します。</summary>
public ReactiveCommand NextImageCommand { get; }

/// <summary>キャンセルコマンドを取得します。</summary>
public ReactiveCommand CancelCommand { get; }

/// <summary>設定完了コマンドを取得します。</summary>
public ReactiveCommand CompleteCommand { get; }

/// <summary>
/// <see cref="SpreadSplitterPageViewModel"/> の新しいインスタンスを初期化します。
/// </summary>
/// <param name="workspaceStore">作品選択状態ストア。</param>
/// <param name="navigationService">ナビゲーションサービス。</param>
/// <param name="imageLoader">画像バイト列読み込み担当。</param>
public SpreadSplitterPageViewModel(
SeriesWorkspaceStore workspaceStore,
INavigationService navigationService,
SpreadSplitterImageLoader imageLoader)
{
this.workspaceStore = workspaceStore;
this.navigationService = navigationService;
this.imageLoader = imageLoader;

this.PageTitle = new BindableReactiveProperty<string>("見開き分割設定")
.AddTo(ref this.disposableBag);
this.SeriesTitle = new BindableReactiveProperty<string>(string.Empty)
.AddTo(ref this.disposableBag);
this.PagePositionText = new BindableReactiveProperty<string>(string.Empty)
.AddTo(ref this.disposableBag);
this.CurrentFileName = new BindableReactiveProperty<string>(string.Empty)
.AddTo(ref this.disposableBag);
this.CurrentDisplayItem = new BindableReactiveProperty<SpreadSplitterDisplayItem?>(null)
.AddTo(ref this.disposableBag);
this.IsSplitTarget = new BindableReactiveProperty<bool>(false)
	.AddTo(ref this.disposableBag);
this.IsLoading = new BindableReactiveProperty<bool>(true)
	.AddTo(ref this.disposableBag);
this.CanGoPrevious = new BindableReactiveProperty<bool>(false)
.AddTo(ref this.disposableBag);
this.CanGoNext = new BindableReactiveProperty<bool>(false)
.AddTo(ref this.disposableBag);

// IsSplitTarget 変更時に Workspace の元データへ反映
this.IsSplitTarget
	.Subscribe(v =>
	{
		var item = this.CurrentDisplayItem.Value;
		if (item is null)
			return;
		item.SourceItem.IsSplitTarget = v;
	})
	.AddTo(ref this.disposableBag);

this.PreviousImageCommand = new ReactiveCommand()
.AddTo(ref this.disposableBag);
this.PreviousImageCommand.Subscribe(_ =>
{
if (this.currentIndex <= 0)
return;
this.currentIndex--;
this.updateCurrentPage();
}).AddTo(ref this.disposableBag);

this.NextImageCommand = new ReactiveCommand()
.AddTo(ref this.disposableBag);
this.NextImageCommand.Subscribe(_ =>
{
if (this.currentIndex >= this.images.Count - 1)
return;
this.currentIndex++;
this.updateCurrentPage();
}).AddTo(ref this.disposableBag);

this.CancelCommand = new ReactiveCommand()
.AddTo(ref this.disposableBag);
this.CancelCommand.Subscribe(_ =>
this.navigationService.Navigate(typeof(VolumeThumbnailsPage)))
.AddTo(ref this.disposableBag);

this.CompleteCommand = new ReactiveCommand()
.AddTo(ref this.disposableBag);
this.CompleteCommand.Subscribe(_ =>
{
// TODO: 設定を保存して製本前確認画面へ遷移
}).AddTo(ref this.disposableBag);
}

/// <inheritdoc/>
public ValueTask InitializeDataAsync()
{
	this.IsLoading.Value = true;

	var series = this.workspaceStore.SelectedSeries.Count > 0
		? this.workspaceStore.SelectedSeries[0]
		: null;

	this.SeriesTitle.Value = series?.Title ?? string.Empty;

	var workspace = this.workspaceStore.GetCurrentPrepressWorkspace();
	this.images = workspace?.Images ?? [];
	this.imageCache.Clear();

	this.currentIndex = 0;
	this.updateCurrentPage();

	this.IsLoading.Value = false;

	return ValueTask.CompletedTask;
}

/// <summary>
/// 現在インデックスに合わせて画面バインド値を更新します。
/// 現在ページ ±<see cref="CacheRange"/> の範囲のみ BitmapImage を保持します。
/// </summary>
private void updateCurrentPage()
{
var total = this.images.Count;

if (total == 0)
{
this.CurrentDisplayItem.Value = null;
	this.CurrentFileName.Value = string.Empty;
	this.PagePositionText.Value = string.Empty;
	this.IsSplitTarget.Value = false;
	this.CanGoPrevious.Value = false;
	this.CanGoNext.Value = false;
return;
}

// キャッシュ範囲外を削除
var keepMin = Math.Max(0, this.currentIndex - CacheRange);
var keepMax = Math.Min(total - 1, this.currentIndex + CacheRange);
var keysToRemove = this.imageCache.Keys
.Where(k => k < keepMin || k > keepMax)
.ToList();
foreach (var key in keysToRemove)
this.imageCache.Remove(key);

// 現在ページをキャッシュから取得、なければ読み込んで登録
if (!this.imageCache.TryGetValue(this.currentIndex, out var displayItem))
{
var sourceItem = this.images[this.currentIndex];
displayItem = new SpreadSplitterDisplayItem(
sourceItem,
this.imageLoader.LoadImageBytes(sourceItem));
this.imageCache[this.currentIndex] = displayItem;
}

this.CurrentDisplayItem.Value = displayItem;
this.CurrentFileName.Value = displayItem.FileName;
this.PagePositionText.Value = $"{this.currentIndex + 1} / {total}";
this.IsSplitTarget.Value = displayItem.SourceItem.IsSplitTarget;
this.CanGoPrevious.Value = this.currentIndex > 0;
this.CanGoNext.Value = this.currentIndex < total - 1;
}

/// <inheritdoc/>
public void Dispose()
=> this.disposableBag.Dispose();
}
