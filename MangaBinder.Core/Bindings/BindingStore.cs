using ObservableCollections;
using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程を画面間で共有する正本状態を保持する Store です。
/// </summary>
public class BindingStore : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>現在 Children.CollectionChanged を購読している Root の集合。</summary>
	private readonly HashSet<MaterialItem> subscribedRoots = new();

	/// <summary>Root.Children 直下のフォルダ数（内部状態）。</summary>
	private readonly BindableReactiveProperty<int> materialFolderCount;

	/// <summary>Root.Children 直下の圧縮ファイル数（内部状態）。</summary>
	private readonly BindableReactiveProperty<int> materialArchiveCount;

	/// <summary>Root.Children 直下の圧縮ファイルの合計物理ファイルサイズ（内部状態）。</summary>
	private readonly BindableReactiveProperty<long> materialArchiveTotalBytes;

	/// <summary>Root.Children 直下の EPUB 数（内部状態）。</summary>
	private readonly BindableReactiveProperty<int> materialEpubCount;

	/// <summary>選択済みの巻の合計画像ファイルサイズ（内部状態）。</summary>
	private readonly BindableReactiveProperty<long> selectedVolumeTotalImageBytes;

	/// <summary>次工程へ進めるかどうか（内部状態）。</summary>
	private readonly BindableReactiveProperty<bool> canGoNext;

	/// <summary>選択済み巻数テキスト（内部状態、「20巻」形式）。</summary>
	private readonly BindableReactiveProperty<string> selectedVolumeCountText;

	/// <summary>
	/// 現在の製本工程で扱っている BindingSeries を取得または設定します。
	/// </summary>
	public BindableReactiveProperty<BindingSeries?> BindingTarget { get; }

	/// <summary>
	/// 巻選択工程で扱う素材ツリーの正本を取得します。
	/// </summary>
	public ObservableList<MaterialItem> Materials { get; }

	/// <summary>
	/// 巻選択工程で選択された巻一覧の正本を取得します。
	/// BindingVolumes 内の各 BindingVolume は、Materials 内の MaterialItem と同一インスタンスを参照します。
	/// </summary>
	public ObservableList<BindingVolume> BindingVolumes { get; }

	/// <summary>
	/// 巻選択工程中に、ユーザーが選択巻一覧を手動並び替えしたかどうかを示します。
	/// true の場合、SelectMaterial() は新規選択巻を常に末尾に追加します。
	/// false の場合、巻番号の昇順（null末尾）で自動挿入されます。
	/// </summary>
	public BindableReactiveProperty<bool> IsManualVolumeOrder { get; }

	/// <summary>
	/// 製本用Workフォルダ内に作成する巻フォルダ名の巻番号桁数を取得します。
	/// </summary>
	public BindableReactiveProperty<int> VolumeFolderDigits { get; }

	/// <summary>
	/// 製本完了時に使用する出力 ZIP ファイル名を取得します。
	/// </summary>
	public BindableReactiveProperty<string> ZipOutputFileName { get; }

	/// <summary>
	/// 製本完了後に対象作品を製本待ちから削除するかどうかを取得または設定します。
	/// </summary>
	public BindableReactiveProperty<bool> RemoveFromBindingQueueAfterCompletion { get; }

	/// <summary>
	/// 巻選択工程を完了し、製本前確認工程へ到達済みかどうかを取得または設定します。
	/// </summary>
	public BindableReactiveProperty<bool> VolumeSelectionCompleted { get; }

	/// <summary>
	/// 製本前確認工程の展開・変換・Inspection処理が正常完了済みかどうかを取得または設定します。
	/// </summary>
	public BindableReactiveProperty<bool> SeriesInspectionCompleted { get; }

	/// <summary>
	/// Root.Children 直下のフォルダ数を取得します。
	/// Materials の変更に追従して自動更新されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<int> MaterialFolderCount { get; }

	/// <summary>
	/// Root.Children 直下の圧縮ファイル数を取得します。
	/// Materials の変更に追従して自動更新されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<int> MaterialArchiveCount { get; }

	/// <summary>
	/// Root.Children 直下の圧縮ファイルの合計物理ファイルサイズ（バイト）を取得します。
	/// Materials の変更に追従して自動更新されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<long> MaterialArchiveTotalBytes { get; }

	/// <summary>
	/// Root.Children 直下の EPUB 数を取得します。
	/// Materials の変更に追従して自動更新されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<int> MaterialEpubCount { get; }

	/// <summary>
	/// 選択済みの巻の合計画像ファイルサイズ（バイト）を取得します。
	/// BindingVolumes の変更に追従して自動更新されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<long> SelectedVolumeTotalImageBytes { get; }

	/// <summary>
	/// 次工程へ進めるかどうかを取得します。
	/// BindingVolumes に1件以上の要素が存在する場合に true になります。
	/// BindingVolumes の変更に追従して自動更新されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<bool> CanGoNext { get; }

	/// <summary>
	/// 選択済みの巻数を「XX巻」形式のテキストで取得します。
	/// BindingVolumes の変更に追従して自動更新されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<string> SelectedVolumeCountText { get; }

	/// <summary>
	/// Work 作品フォルダが既に存在するかどうかを取得または設定します。
	/// 初期値は false です。
	/// </summary>
	public BindableReactiveProperty<bool> HasExistingWorkFolder { get; }

	/// <summary>
	/// 素材展開方法を取得または設定します。
	/// 初期値は <see cref="global::MangaBinder.Bindings.ImageExpansionMethod.Recreate"/> です。
	/// </summary>
	public BindableReactiveProperty<ImageExpansionMethod> ImageExpansionMethod { get; }

	/// <summary>
	/// 選択可能な素材展開方法のオプション一覧を取得します。
	/// </summary>
	public IReadOnlyList<ImageExpansionOption> ImageExpansionOptions { get; }

	/// <summary>
	/// 巻フォルダ名の桁数選択肢一覧を取得します。
	/// </summary>
	public IReadOnlyList<VolumeFolderDigitOption> VolumeFolderDigitOptions { get; }

	/// <summary>
	/// <see cref="BindingStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	public BindingStore()
	{
		this.BindingTarget = new BindableReactiveProperty<BindingSeries?>(null)
			.AddTo(ref this.disposableBag);
		this.Materials = new ObservableList<MaterialItem>();
		this.BindingVolumes = new ObservableList<BindingVolume>();
		this.IsManualVolumeOrder = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.VolumeFolderDigits = new BindableReactiveProperty<int>(2)
			.AddTo(ref this.disposableBag);
		this.ZipOutputFileName = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
		this.RemoveFromBindingQueueAfterCompletion = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);
		this.VolumeSelectionCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.SeriesInspectionCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// 素材サマリ派生状態を初期化（private フィールド経由）
		this.materialFolderCount = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.materialArchiveCount = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.materialArchiveTotalBytes = new BindableReactiveProperty<long>(0)
			.AddTo(ref this.disposableBag);
		this.materialEpubCount = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		this.MaterialFolderCount = this.materialFolderCount;
		this.MaterialArchiveCount = this.materialArchiveCount;
		this.MaterialArchiveTotalBytes = this.materialArchiveTotalBytes;
		this.MaterialEpubCount = this.materialEpubCount;

		// Materials の CollectionChanged を購読して派生状態を自動更新
		// 新規 Root が追加される際にも、その Children の変更を購読設定
		this.Materials.CollectionChanged += this.onMaterialsCollectionChanged;

		// 初期段階で既に存在する Root の Children も購読
		foreach (var root in this.Materials)
		{
			root.Children.CollectionChanged += this.onRootChildrenCollectionChanged;
			this.subscribedRoots.Add(root);
		}

		// 選択済み巻の合計画像サイズ派生状態を初期化（private フィールド経由）
		this.selectedVolumeTotalImageBytes = new BindableReactiveProperty<long>(0)
			.AddTo(ref this.disposableBag);
		this.SelectedVolumeTotalImageBytes = this.selectedVolumeTotalImageBytes;

		// BindingVolumes の CollectionChanged を購読して自動更新
		this.BindingVolumes.CollectionChanged += this.onBindingVolumesCollectionChangedForSize;

		// 次へ進めるかどうかの派生状態を初期化（private フィールド経由）
		this.canGoNext = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.CanGoNext = this.canGoNext;

		// BindingVolumes の Count 変化で CanGoNext を自動更新
		this.BindingVolumes.CollectionChanged += this.onBindingVolumesCollectionChangedForCanGoNext;

		// 選択済み巻数テキスト派生状態を初期化（private フィールド経由）
		this.selectedVolumeCountText = new BindableReactiveProperty<string>("0巻")
			.AddTo(ref this.disposableBag);
		this.SelectedVolumeCountText = this.selectedVolumeCountText;

		// BindingVolumes の Count 変化で SelectedVolumeCountText を自動更新
		this.BindingVolumes.CollectionChanged += this.onBindingVolumesCollectionChangedForCountText;

		// 素材展開関連の状態を初期化
		this.HasExistingWorkFolder = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.ImageExpansionMethod = new BindableReactiveProperty<global::MangaBinder.Bindings.ImageExpansionMethod>(
			global::MangaBinder.Bindings.ImageExpansionMethod.Recreate)
			.AddTo(ref this.disposableBag);

		this.ImageExpansionOptions = new[]
		{
			new ImageExpansionOption(
				global::MangaBinder.Bindings.ImageExpansionMethod.Recreate,
				"作品フォルダを新規作成する（既存フォルダ削除）"),
			new ImageExpansionOption(
				global::MangaBinder.Bindings.ImageExpansionMethod.UseExisting,
				"既存の画像を使用する"),
		}.AsReadOnly();

		this.VolumeFolderDigitOptions = new[]
		{
			new VolumeFolderDigitOption(1, "1桁", "（例：1巻）"),
			new VolumeFolderDigitOption(2, "2桁", "（例：01巻）"),
			new VolumeFolderDigitOption(3, "3桁", "（例：001巻）"),
		}.AsReadOnly();

		// BindingTarget の変更を購読し、変更されたら現在のセッション状態を初期化
		// Skip(1) で構築時の初期値通知を無視して、その後の変更だけを捕捉
		this.BindingTarget
			.Skip(1)
			.Subscribe(_ => this.clearCurrentBindingSession())
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// Materials から素材サマリ統計を計算し、派生状態を更新します。
	/// Root.Children 直下のみを集計対象とします。
	/// </summary>
	private void updateMaterialSummary()
	{
		// Root直下の子要素を集計
		var rootChildren = this.Materials.SelectMany(root => root.Children).ToList();

		var folderCount = rootChildren.Count(item => item.ItemType == MaterialItemType.Folder);
		var archiveCount = rootChildren.Count(item => item.ItemType == MaterialItemType.Archive);
		var epubCount = rootChildren.Count(item => item.ItemType == MaterialItemType.Epub);
		var archiveTotalBytes = rootChildren
			.Where(item => item.ItemType == MaterialItemType.Archive)
			.Sum(item => item.FileSizeBytes);

		this.materialFolderCount.Value = folderCount;
		this.materialArchiveCount.Value = archiveCount;
		this.materialEpubCount.Value = epubCount;
		this.materialArchiveTotalBytes.Value = archiveTotalBytes;
	}

	/// <summary>

	/// <summary>
	/// Materials の CollectionChanged イベントハンドラ。
	/// Root の追加・削除に応じて Children の購読状態を同期し、サマリを更新します。
	/// </summary>
	private void onMaterialsCollectionChanged(in NotifyCollectionChangedEventArgs<MaterialItem> e)
	{
		switch (e.Action)
		{
			case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
				// Root が追加された場合、その Children.CollectionChanged を購読
				if (e.IsSingleItem)
				{
					if (e.NewItem != null)
					{
						e.NewItem.Children.CollectionChanged += this.onRootChildrenCollectionChanged;
						this.subscribedRoots.Add(e.NewItem);
					}
				}
				else
				{
					foreach (MaterialItem newRoot in e.NewItems)
					{
						newRoot.Children.CollectionChanged += this.onRootChildrenCollectionChanged;
						this.subscribedRoots.Add(newRoot);
					}
				}
				break;

			case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
				// Root が削除された場合、その Children.CollectionChanged の購読を解除
				if (e.IsSingleItem)
				{
					if (e.OldItem != null)
					{
						e.OldItem.Children.CollectionChanged -= this.onRootChildrenCollectionChanged;
						this.subscribedRoots.Remove(e.OldItem);
					}
				}
				else
				{
					foreach (MaterialItem oldRoot in e.OldItems)
					{
						oldRoot.Children.CollectionChanged -= this.onRootChildrenCollectionChanged;
						this.subscribedRoots.Remove(oldRoot);
					}
				}
				break;

			case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
				// Clear() 時は全 Root の購読を解除
				foreach (var root in this.subscribedRoots)
				{
					root.Children.CollectionChanged -= this.onRootChildrenCollectionChanged;
				}
				this.subscribedRoots.Clear();
				break;
		}

		// サマリを更新（Add/Remove/Reset 共通）
		this.updateMaterialSummary();
	}

	/// <summary>
	/// Root の Children の CollectionChanged イベントハンドラ。
	/// 素材サマリを更新します。
	/// </summary>
	private void onRootChildrenCollectionChanged(in NotifyCollectionChangedEventArgs<MaterialItem> e)
	{
		// サマリを更新
		this.updateMaterialSummary();
	}

	/// <summary>
	/// BindingVolumes の CollectionChanged イベントハンドラ。選択済み巻の合計画像サイズを更新します。
	/// </summary>
	private void onBindingVolumesCollectionChangedForSize(in NotifyCollectionChangedEventArgs<BindingVolume> e)
	{
		var totalBytes = this.BindingVolumes
			.Sum(volume => volume.Material.TotalImageBytes);
		this.selectedVolumeTotalImageBytes.Value = totalBytes;
	}

	/// <summary>
	/// BindingVolumes の CollectionChanged イベントハンドラ。CanGoNext を更新します。
	/// </summary>
	private void onBindingVolumesCollectionChangedForCanGoNext(in NotifyCollectionChangedEventArgs<BindingVolume> e)
	{
		this.canGoNext.Value = this.BindingVolumes.Count > 0;
	}

	/// <summary>
	/// BindingVolumes の CollectionChanged イベントハンドラ。SelectedVolumeCountText を更新します。
	/// </summary>
	private void onBindingVolumesCollectionChangedForCountText(in NotifyCollectionChangedEventArgs<BindingVolume> e)
	{
		var count = this.BindingVolumes.Count;
		this.selectedVolumeCountText.Value = $"{count}巻";
	}

	/// <summary>
	/// 製本セッション固有の状態を初期値へ戻します。
	/// BindingTarget 自身は変更しません。
	/// </summary>
	private void clearCurrentBindingSession()
	{
		// BindingVolumes が Materials を参照しているため、先に BindingVolumes を破棄
		foreach (var volume in this.BindingVolumes)
		{
			volume.Dispose();
		}
		this.BindingVolumes.Clear();

		// 次に Materials を破棄
		foreach (var material in this.Materials)
		{
			material.Dispose();
		}
		this.Materials.Clear();

		// セッション固有の mutable 状態をリセット
		this.IsManualVolumeOrder.Value = false;
		this.HasExistingWorkFolder.Value = false;
		this.ImageExpansionMethod.Value = global::MangaBinder.Bindings.ImageExpansionMethod.Recreate;
		this.VolumeFolderDigits.Value = 2;
		this.ZipOutputFileName.Value = string.Empty;
		this.RemoveFromBindingQueueAfterCompletion.Value = true;
		this.VolumeSelectionCompleted.Value = false;
		this.SeriesInspectionCompleted.Value = false;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// イベント購読を解除
		// Materials.CollectionChanged
		this.Materials.CollectionChanged -= this.onMaterialsCollectionChanged;

		// 全 Root の Children.CollectionChanged（subscribedRoots に保持している旧 Root を含む）
		foreach (var root in this.subscribedRoots)
		{
			root.Children.CollectionChanged -= this.onRootChildrenCollectionChanged;
		}
		this.subscribedRoots.Clear();

		// BindingVolumes.CollectionChanged
		this.BindingVolumes.CollectionChanged -= this.onBindingVolumesCollectionChangedForSize;
		this.BindingVolumes.CollectionChanged -= this.onBindingVolumesCollectionChangedForCanGoNext;
		this.BindingVolumes.CollectionChanged -= this.onBindingVolumesCollectionChangedForCountText;

		// 所有関係に従って破棄する
		// BindingVolumes が Materials を参照しているため、先に BindingVolumes を破棄
		foreach (var volume in this.BindingVolumes)
		{
			volume.Dispose();
		}
		this.BindingVolumes.Clear();

		// 次に Materials を破棄
		foreach (var material in this.Materials)
		{
			material.Dispose();
		}
		this.Materials.Clear();

		// 最後に ReactiveProperty を破棄
		this.disposableBag.Dispose();
	}
}
