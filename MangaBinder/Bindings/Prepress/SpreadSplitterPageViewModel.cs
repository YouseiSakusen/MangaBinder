using MangaBinder.Bindings;
using R3;
using System.Linq;
using Wpf.Ui;

namespace MangaBinder.Bindings.Prepress;

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

/// <summary>画像サイズテキスト（例: 2800 x 2000）を取得します。</summary>
public BindableReactiveProperty<string> ImageSizeText { get; }

/// <summary>ページ順テキスト（例: 右→左）を取得します。</summary>
public BindableReactiveProperty<string> SpreadPageOrderText { get; }

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

// ---- トリミング値（共通設定） ----

/// <summary>左トリム量（ピクセル）を取得または設定します。</summary>
public BindableReactiveProperty<double> TrimLeft { get; }

/// <summary>右トリム量（ピクセル）を取得または設定します。</summary>
public BindableReactiveProperty<double> TrimRight { get; }

/// <summary>上トリム量（ピクセル）を取得または設定します。</summary>
public BindableReactiveProperty<double> TrimTop { get; }

/// <summary>下トリム量（ピクセル）を取得または設定します。</summary>
public BindableReactiveProperty<double> TrimBottom { get; }

// ---- 設定モード（今回は共通設定固定） ----

/// <summary>共通設定モードかどうかを取得します（現在固定 true）。</summary>
public BindableReactiveProperty<bool> IsCommonMode { get; }

/// <summary>完全個別設定モードかどうかを取得します（現在固定 false）。</summary>
public BindableReactiveProperty<bool> IsFullIndividualMode { get; }

/// <summary>このページのみ個別設定かどうかを取得します（現在固定 false）。</summary>
public BindableReactiveProperty<bool> IsThisPageIndividual { get; }

// ---- プレビュー領域の実サイズ（Behavior から Push） ----

/// <summary>プレビュー領域の実表示幅（ピクセル）を取得または設定します。</summary>
public BindableReactiveProperty<double> PreviewAreaWidth { get; }

/// <summary>プレビュー領域の実表示高さ（ピクセル）を取得または設定します。</summary>
public BindableReactiveProperty<double> PreviewAreaHeight { get; }

// ---- ガイド線描画座標（Canvas.Left / Canvas.Top） ----

/// <summary>左ガイド線の Canvas.Left 値を取得します。</summary>
public BindableReactiveProperty<double> GuideLeft { get; }

/// <summary>右ガイド線の Canvas.Left 値を取得します。</summary>
public BindableReactiveProperty<double> GuideRight { get; }

/// <summary>上ガイド線の Canvas.Top 値を取得します。</summary>
public BindableReactiveProperty<double> GuideTop { get; }

/// <summary>下ガイド線の Canvas.Top 値を取得します。</summary>
public BindableReactiveProperty<double> GuideBottom { get; }

/// <summary>中央赤線の Canvas.Left 値を取得します。</summary>
public BindableReactiveProperty<double> SplitCenter { get; }

/// <summary>現在表示画像の元ピクセル幅を取得します。</summary>
private int sourceImageWidth;

/// <summary>現在表示画像の元ピクセル高さを取得します。</summary>
private int sourceImageHeight;

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
	this.ImageSizeText = new BindableReactiveProperty<string>(string.Empty)
		.AddTo(ref this.disposableBag);
	this.SpreadPageOrderText = new BindableReactiveProperty<string>(string.Empty)
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

	// ---- トリミング値 ----
	this.TrimLeft = new BindableReactiveProperty<double>(0)
		.AddTo(ref this.disposableBag);
	this.TrimRight = new BindableReactiveProperty<double>(0)
		.AddTo(ref this.disposableBag);
	this.TrimTop = new BindableReactiveProperty<double>(0)
		.AddTo(ref this.disposableBag);
	this.TrimBottom = new BindableReactiveProperty<double>(0)
		.AddTo(ref this.disposableBag);

	// ---- 設定モード固定 ----
	this.IsCommonMode = new BindableReactiveProperty<bool>(true)
		.AddTo(ref this.disposableBag);
	this.IsFullIndividualMode = new BindableReactiveProperty<bool>(false)
		.AddTo(ref this.disposableBag);
	this.IsThisPageIndividual = new BindableReactiveProperty<bool>(false)
		.AddTo(ref this.disposableBag);

	// ---- プレビューサイズ ----
	this.PreviewAreaWidth = new BindableReactiveProperty<double>(0)
		.AddTo(ref this.disposableBag);
	this.PreviewAreaHeight = new BindableReactiveProperty<double>(0)
		.AddTo(ref this.disposableBag);

	// ---- ガイド線座標 ----
	this.GuideLeft = new BindableReactiveProperty<double>(0)
		.AddTo(ref this.disposableBag);
	this.GuideRight = new BindableReactiveProperty<double>(0)
		.AddTo(ref this.disposableBag);
	this.GuideTop = new BindableReactiveProperty<double>(0)
		.AddTo(ref this.disposableBag);
	this.GuideBottom = new BindableReactiveProperty<double>(0)
		.AddTo(ref this.disposableBag);
	this.SplitCenter = new BindableReactiveProperty<double>(0)
		.AddTo(ref this.disposableBag);

	// トリム値変更 → 全画像反映 & ガイド再計算
	Observable.Merge(
		this.TrimLeft.AsObservable().Select(_ => 0),
		this.TrimRight.AsObservable().Select(_ => 0),
		this.TrimTop.AsObservable().Select(_ => 0),
		this.TrimBottom.AsObservable().Select(_ => 0))
		.Subscribe(_ =>
		{
			this.applyTrimToAllImages();
			this.recalculateGuides();
		})
		.AddTo(ref this.disposableBag);

	// プレビューサイズ変更 → ガイド再計算
	Observable.Merge(
		this.PreviewAreaWidth.AsObservable().Select(_ => 0),
		this.PreviewAreaHeight.AsObservable().Select(_ => 0))
		.Subscribe(_ => this.recalculateGuides())
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
	this.SpreadPageOrderText.Value = workspace?.SpreadPageOrder switch
	{
		SpreadPageOrder.LeftToRight => "ページ順: 左⇒右",
		_ => "ページ順: 右⇒左",
	};

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
		this.ImageSizeText.Value = string.Empty;
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
	this.ImageSizeText.Value = displayItem.DisplayImage is { } img
		? $"{img.PixelWidth} x {img.PixelHeight}"
		: string.Empty;
	this.PagePositionText.Value = $"{this.currentIndex + 1} / {total}";
	this.IsSplitTarget.Value = displayItem.SourceItem.IsSplitTarget;
	this.CanGoPrevious.Value = this.currentIndex > 0;
	this.CanGoNext.Value = this.currentIndex < total - 1;

	// 元画像サイズを保持
	this.sourceImageWidth = displayItem.DisplayImage?.PixelWidth ?? 0;
	this.sourceImageHeight = displayItem.DisplayImage?.PixelHeight ?? 0;

	// 共通設定としてトリム値を読み込む（全画像共通なので現在ページの値を使用）
	var trim = displayItem.SourceItem.SpreadSplitInformation;
	this.TrimLeft.Value = trim.TrimLeft;
	this.TrimRight.Value = trim.TrimRight;
	this.TrimTop.Value = trim.TrimTop;
	this.TrimBottom.Value = trim.TrimBottom;

	this.recalculateGuides();
}

/// <inheritdoc/>
public void Dispose()
	=> this.disposableBag.Dispose();

/// <summary>
/// 現在のトリム値を全画像の SpreadSplitInformation へ反映します（共通設定）。
/// </summary>
private void applyTrimToAllImages()
{
	var left = (int)this.TrimLeft.Value;
	var right = (int)this.TrimRight.Value;
	var top = (int)this.TrimTop.Value;
	var bottom = (int)this.TrimBottom.Value;

	foreach (var item in this.images)
	{
		item.SpreadSplitInformation.TrimLeft = left;
		item.SpreadSplitInformation.TrimRight = right;
		item.SpreadSplitInformation.TrimTop = top;
		item.SpreadSplitInformation.TrimBottom = bottom;
	}
}

/// <summary>
/// トリム値とプレビューサイズからガイド線の描画座標を再計算します。
/// </summary>
private void recalculateGuides()
{
	var pw = this.PreviewAreaWidth.Value;
	var ph = this.PreviewAreaHeight.Value;
	var sw = (double)this.sourceImageWidth;
	var sh = (double)this.sourceImageHeight;

	if (pw <= 0 || ph <= 0 || sw <= 0 || sh <= 0)
		return;

	// Uniform Stretch の表示 Scale を計算
	var scaleX = pw / sw;
	var scaleY = ph / sh;
	var scale = Math.Min(scaleX, scaleY);

	// 画像が中央に配置されるため、オフセットを計算
	var displayW = sw * scale;
	var displayH = sh * scale;
	var offsetX = (pw - displayW) / 2.0;
	var offsetY = (ph - displayH) / 2.0;

	var left = this.TrimLeft.Value;
	var right = this.TrimRight.Value;
	var top = this.TrimTop.Value;
	var bottom = this.TrimBottom.Value;

	this.GuideLeft.Value = offsetX + left * scale;
	this.GuideRight.Value = offsetX + (sw - right) * scale;
	this.GuideTop.Value = offsetY + top * scale;
	this.GuideBottom.Value = offsetY + (sh - bottom) * scale;

	// 赤線: トリム後有効領域の中央
	var centerX = (left + (sw - right)) / 2.0;
	this.SplitCenter.Value = offsetX + centerX * scale;
}
}
