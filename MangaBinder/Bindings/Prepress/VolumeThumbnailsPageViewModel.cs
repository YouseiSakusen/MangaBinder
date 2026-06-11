using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using MangaBinder.Bindings;
using MangaBinder.Bindings.Inspection;
using ObservableCollections;
using R3;
using Wpf.Ui;

namespace MangaBinder.Bindings.Prepress;

/// <summary>
/// <see cref="VolumeThumbnailsPage"/> の ViewModel です。
/// </summary>
public class VolumeThumbnailsPageViewModel : IDisposable, IDataInitializable
{
	/// <summary>作品選択状態ストア。</summary>
	private readonly SeriesWorkspaceStore workspaceStore;

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>サムネイル読み込み担当。</summary>
	private readonly VolumeThumbnailLoader thumbnailLoader;

	private DisposableBag disposableBag;

	/// <summary>サムネイルアイテムの内部リスト。</summary>
	private readonly ObservableList<ThumbnailDisplayItem> items;

	/// <summary>SelectAllState の一括更新中は個別買読を抑制するフラグ。</summary>
	private bool suppressSelectAllUpdate;

	/// <summary>作品タイトルを取得します。</summary>
	public BindableReactiveProperty<string> SeriesTitle { get; }

	/// <summary>作品サムネイル表示用の MangaSeries を取得します。</summary>
	public BindableReactiveProperty<MangaSeries?> SelectedSeries { get; }

	/// <summary>巻名を取得します。</summary>
	public BindableReactiveProperty<string> VolumeName { get; }

	/// <summary>パンくずおよびヘッダーに表示するページタイトルを取得します。</summary>
	public BindableReactiveProperty<string> PageTitle { get; }

	/// <summary>分割対象として選択中の件数を取得します。</summary>
	public BindableReactiveProperty<int> CheckedCount { get; }

	/// <summary>WorkFolder 内の巻フォルダパスを取得します。</summary>
	public BindableReactiveProperty<string> WorkVolumeFolderPath { get; }

	/// <summary>画像ファイル数を取得します。</summary>
	public BindableReactiveProperty<int> ImageFileCount { get; }

	/// <summary>ローディング中かどうかを取得します。</summary>
	public BindableReactiveProperty<bool> IsLoading { get; }

	/// <summary>次へボタンが有効かどうかを取得します。</summary>
	public BindableReactiveProperty<bool> CanGoNext { get; }

	/// <summary>エラーがあるかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> HasError { get; }

	/// <summary>エラーメッセージを取得します。</summary>
	public BindableReactiveProperty<string> ErrorMessage { get; }

	/// <summary>サムネイル一覧（バインド用）を取得します。</summary>
	public NotifyCollectionChangedSynchronizedViewList<ThumbnailDisplayItem> Items { get; }

	/// <summary>戻るコマンドを取得します。</summary>
	public ReactiveCommand GoBackCommand { get; }

	/// <summary>次へコマンドを取得します（将来: SpreadSplitterPage へ遷移）。</summary>
	public ReactiveCommand GoNextCommand { get; }

	/// <summary>全選択状態（True=全チェック / False=全未チェック / null=一部チェック）を取得します。</summary>
	public BindableReactiveProperty<bool?> SelectAllState { get; }

	/// <summary>見開き分割後のページ順を取得または設定します。</summary>
	public BindableReactiveProperty<SpreadPageOrder> SelectedSpreadPageOrder { get; }

	/// <summary>全てチェックコマンドを取得します。</summary>
	public ReactiveCommand CheckAllCommand { get; }

	/// <summary>チェック解除コマンドを取得します。</summary>
	public ReactiveCommand UncheckAllCommand { get; }

	/// <summary>Explorerで開くコマンドを取得します。</summary>
	public ReactiveCommand OpenInExplorerCommand { get; }

	/// <summary>再読み込みコマンドを取得します。</summary>
	public ReactiveCommand ReloadCommand { get; }

	/// <summary>
	/// <see cref="VolumeThumbnailsPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="workspaceStore">作品選択状態ストア。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="thumbnailLoader">サムネイル読み込み担当。</param>
	public VolumeThumbnailsPageViewModel(
		SeriesWorkspaceStore workspaceStore,
		INavigationService navigationService,
		VolumeThumbnailLoader thumbnailLoader)
	{
		this.workspaceStore = workspaceStore;
		this.navigationService = navigationService;
		this.thumbnailLoader = thumbnailLoader;

		this.items = new ObservableList<ThumbnailDisplayItem>();

		this.SeriesTitle = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
		this.SelectedSeries = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);
		this.VolumeName = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
		this.PageTitle = new BindableReactiveProperty<string>("分割対象選択")
			.AddTo(ref this.disposableBag);
		this.CheckedCount = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.WorkVolumeFolderPath = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
		this.ImageFileCount = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.IsLoading = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.CanGoNext = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.HasError = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.ErrorMessage = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.SelectAllState = new BindableReactiveProperty<bool?>(false)
			.AddTo(ref this.disposableBag);
		this.SelectedSpreadPageOrder = new BindableReactiveProperty<SpreadPageOrder>(SpreadPageOrder.RightToLeft)
			.AddTo(ref this.disposableBag);

		// CheckBox の TwoWay バインドで true/false が書き込まれたとき一括操作
		this.SelectAllState
			.Where(v => v.HasValue)
			.Subscribe(v =>
			{
				this.suppressSelectAllUpdate = true;
				if (v == true)
				{
					foreach (var item in this.items)
						if (!item.IsUnsupported && !item.HasError)
							item.IsChecked.Value = true;
				}
				else
				{
					foreach (var item in this.items)
						item.IsChecked.Value = false;
				}
				this.suppressSelectAllUpdate = false;
			})
			.AddTo(ref this.disposableBag);

		this.Items = this.items
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.GoBackCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.GoBackCommand.Subscribe(_ => this.navigationService.GoBack())
			.AddTo(ref this.disposableBag);

		this.GoNextCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.GoNextCommand.Subscribe(_ =>
		{
			var volume = this.workspaceStore.GetCurrentPrepressVolume();
			if (volume is null)
				return;

			// 全アイテムを PrepressImageItem に変換して Workspace へ保存
			var workspace = new PrepressVolumeWorkspace(volume);
			workspace.Images.AddRange(this.items.Select(i => new PrepressImageItem
			{
				FilePath = i.FilePath,
				FileName = i.FileName,
				IsSplitTarget = i.IsChecked.Value,
				IsUnsupported = i.IsUnsupported,
				HasError = i.HasError,
				SpreadSplitInformation = new SpreadSplitInformation(),
			}));
			workspace.SpreadPageOrder = this.SelectedSpreadPageOrder.Value;
			this.workspaceStore.SetPrepressWorkspace(workspace);

			this.navigationService.Navigate(typeof(SpreadSplitterPage));
		}).AddTo(ref this.disposableBag);

		this.CheckAllCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.CheckAllCommand.Subscribe(_ =>
		{
			this.suppressSelectAllUpdate = true;
			foreach (var item in this.items)
			{
				if (!item.IsUnsupported && !item.HasError)
					item.IsChecked.Value = true;
			}
			this.suppressSelectAllUpdate = false;
			this.updateSelectAllState();
		}).AddTo(ref this.disposableBag);

		this.UncheckAllCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.UncheckAllCommand.Subscribe(_ =>
		{
			this.suppressSelectAllUpdate = true;
			foreach (var item in this.items)
				item.IsChecked.Value = false;
			this.suppressSelectAllUpdate = false;
			this.updateSelectAllState();
		}).AddTo(ref this.disposableBag);

		this.OpenInExplorerCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.OpenInExplorerCommand.Subscribe(_ =>
		{
			var path = this.WorkVolumeFolderPath.Value;
			if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
				Process.Start("explorer.exe", path);
		}).AddTo(ref this.disposableBag);

		this.ReloadCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.ReloadCommand.Subscribe(_ =>
		{
			var volume = this.workspaceStore.GetCurrentPrepressVolume();
			if (volume is null)
				return;
			this.IsLoading.Value = true;
			Task.Run(() => this.loadAsync(volume));
		}).AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public ValueTask InitializeDataAsync()
	{
		var volume = this.workspaceStore.GetCurrentPrepressVolume();

		var series = this.workspaceStore.SelectedSeries.Count > 0
			? this.workspaceStore.SelectedSeries[0]
			: null;

		this.SelectedSeries.Value = series;
		this.SeriesTitle.Value = series?.Title ?? string.Empty;
		this.VolumeName.Value = volume?.VolumeName ?? string.Empty;
		this.PageTitle.Value = string.IsNullOrEmpty(volume?.VolumeName)
			? "分割対象選択"
			: $"分割対象選択（{volume.VolumeName}）";
		this.WorkVolumeFolderPath.Value = volume?.WorkVolumeFolderPath ?? string.Empty;
		this.ImageFileCount.Value = volume?.ImageFileCount ?? 0;

		this.items.Clear();
		this.HasError.Value = false;
		this.ErrorMessage.Value = string.Empty;
		this.CanGoNext.Value = false;

		if (volume is null)
			return ValueTask.CompletedTask;

		this.IsLoading.Value = true;
		_ = this.loadAsync(volume);

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// バックグラウンドでリネーム前処理とサムネイル読み込みを実行し、完了後 UI へ反映します。
	/// </summary>
	private async Task loadAsync(VolumeInspectionResult volume)
	{
		try
		{
			var (loadedItems, hasError, errorMessage) = await Task.Run(() =>
			{
				// 1. 桁揃えリネーム前処理
				var normalizeResults = this.thumbnailLoader.NormalizeFileNames(volume);
				var renameError = normalizeResults.Any(r => r.HasError);
				string? renameErrorMsg = renameError
					? "桁揃えに失敗したファイルがあります。ファイル名を修正してから再読み込みしてください。"
					: null;

				// 2. サムネイル読み込み
				var thumbnailItems = this.thumbnailLoader.LoadItems(volume.WorkVolumeFolderPath);

				// エラー状態：リネームエラー or 対応外/読み込み失敗ファイルが存在する場合
				var itemError = thumbnailItems.Any(i => i.HasError || i.IsUnsupported);
				var err = renameError || itemError;
				var errMsg = renameErrorMsg
					?? (itemError ? "対応外または読み込みに失敗したファイルがあります。" : null);

				// リネームエラー対象アイテムに HasError を付与
				if (renameError)
				{
					var errorPaths = new HashSet<string>(
						normalizeResults.Where(r => r.HasError).Select(r => r.OriginalPath),
						StringComparer.OrdinalIgnoreCase);

					thumbnailItems = thumbnailItems
						.Select(i => errorPaths.Contains(i.FilePath)
							? new VolumeThumbnailItem
							{
								FilePath = i.FilePath,
								FileName = i.FileName,
								ThumbnailBytes = i.ThumbnailBytes,
								FallbackResourceKey = i.FallbackResourceKey,
								IsChecked = false,
								HasError = true,
								IsUnsupported = i.IsUnsupported,
							}
							: i)
						.ToArray();
				}

				return (thumbnailItems, err, errMsg);
			});

			this.items.Clear();
			foreach (var item in loadedItems)
			{
				var displayItem = new ThumbnailDisplayItem(item, toBitmapImage(item));
				displayItem.IsChecked
					.Subscribe(_ => this.updateSelectAllState())
					.AddTo(ref this.disposableBag);
				this.items.Add(displayItem);
			}
			this.updateSelectAllState();

			this.HasError.Value = hasError;
			this.ErrorMessage.Value = errorMessage ?? string.Empty;
			this.CanGoNext.Value = !hasError;
			this.ImageFileCount.Value = this.items.Count;
		}
		finally
		{
			this.IsLoading.Value = false;
		}
	}

	/// <summary>
	/// <see cref="VolumeThumbnailItem.ThumbnailBytes"/> または <see cref="VolumeThumbnailItem.FallbackResourceKey"/>
	/// から WPF の <see cref="BitmapImage"/> を生成して返します。
	/// </summary>
	private static BitmapImage toBitmapImage(VolumeThumbnailItem item)
	{
		if (item.ThumbnailBytes is { } bytes)
		{
			var bmp = new BitmapImage();
			using var ms = new System.IO.MemoryStream(bytes);
			bmp.BeginInit();
			bmp.CacheOption = BitmapCacheOption.OnLoad;
			bmp.StreamSource = ms;
			bmp.EndInit();
			bmp.Freeze();
			return bmp;
		}

		var key = item.FallbackResourceKey ?? VolumeThumbnailImageProcessor.LoadFailedResource;
		var packUri = $"pack://application:,,,/Resources/Prepress/{key}.png";
		var fallback = new BitmapImage();
		fallback.BeginInit();
		fallback.CacheOption = BitmapCacheOption.OnLoad;
		fallback.UriSource = new Uri(packUri);
		fallback.EndInit();
		fallback.Freeze();
		return fallback;
	}

	/// <summary>
	/// アイテムのチェック状態から <see cref="SelectAllState"/> を再計算します。
	/// </summary>
	private void updateSelectAllState()
	{
		if (this.suppressSelectAllUpdate)
			return;

		var checkable = this.items.Where(i => !i.IsUnsupported && !i.HasError).ToList();
		if (checkable.Count == 0)
		{
			this.CheckedCount.Value = 0;
			this.SelectAllState.Value = false;
			return;
		}

		var checkedCount = checkable.Count(i => i.IsChecked.Value);
		this.CheckedCount.Value = checkedCount;
		this.SelectAllState.Value = checkedCount == 0 ? false
			: checkedCount == checkable.Count ? true
			: null;
	}

	/// <inheritdoc/>
	public void Dispose()
		=> this.disposableBag.Dispose();
}
