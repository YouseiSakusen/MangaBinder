using ObservableCollections;
using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程を画面間で共有する正本状態を保持する Store です。
/// </summary>
public class BindingStore : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>BindingVolumes に含まれる各 BindingVolume の VolumeNumber 購読。</summary>
	private readonly Dictionary<BindingVolume, IDisposable> volumeNumberSubscriptions = new();

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
	/// 現在の見開き分割画面で扱う対象巻を取得または設定します。
	/// 値の切り替え時に、旧巻の画像は見開き分割対象を解除し、新巻の画像へデフォルトを適用します。
	/// </summary>
	public BindableReactiveProperty<BindingVolume?> SplitTargetVolume { get; }

	/// <summary>
	/// ImageSplitter で使用する編集用 BindingVolume Clone の保持先を取得します。
	/// BindingStore が所有し、SplitTargetVolume が null になった時点、または製本セッション初期化・Dispose 時に破棄されます。
	/// </summary>
	public ObservableList<BindingVolume> SplitVolumes { get; }

	private readonly Subject<BindingVolume> volumeUpdated = new();

	/// <summary>正本 BindingVolume が更新されたことの通知を取得します。</summary>
	public Observable<BindingVolume> VolumeUpdated => this.volumeUpdated;

	/// <summary>正本 BindingVolume が更新されたことを通知します。</summary>
	/// <param name="volume">更新された正本 BindingVolume。</param>
	public void NotifyVolumeUpdated(BindingVolume volume) => this.volumeUpdated.OnNext(volume);

	/// <summary>ImageSplitter の左右トリミング Maximum（px）を取得します。</summary>
	public BindableReactiveProperty<int> SplitTrimHorizontalMaximum { get; }

	/// <summary>ImageSplitter の上下トリミング Maximum（px）を取得します。</summary>
	public BindableReactiveProperty<int> SplitTrimVerticalMaximum { get; }

	/// <summary>ImageSplitter の SplitOffset Minimum（px）を取得します。</summary>
	public BindableReactiveProperty<int> SplitOffsetMinimum { get; }

	/// <summary>ImageSplitter の SplitOffset Maximum（px）を取得します。</summary>
	public BindableReactiveProperty<int> SplitOffsetMaximum { get; }

	/// <summary>直前の SplitTargetVolume。</summary>
	private BindingVolume? previousSplitTargetVolume;

	/// <summary>
	/// 巻選択工程で素材ツリーの正本を取得します。
	/// </summary>
	public ObservableList<MaterialItem> Materials { get; }

	/// <summary>
	/// 巻選択工程で選択された巻一覧の正本を取得します。
	/// BindingVolumes 内の各 BindingVolume は、Materials 内の MaterialItem と同一インスタンスを参照します。
	/// </summary>
	public ObservableList<BindingVolume> BindingVolumes { get; }

	/// <summary>
	/// 製本用Workフォルダ内に作成する巻フォルダ名の巻番号桁数を取得します。
	/// </summary>
	public BindableReactiveProperty<int> VolumeFolderDigits { get; }

	/// <summary>
	/// 製本完了時に使用する出力 ZIP ファイル名を取得します。
	/// </summary>
	public BindableReactiveProperty<string> ZipOutputFileName { get; }

	/// <summary>
	/// 今回の製本セッションでZIPを作成するかどうかを取得または設定します。
	/// 初期値は true です。
	/// </summary>
	public BindableReactiveProperty<bool> CreateZip { get; }

	/// <summary>
	/// 製本完了後に出力先を開くかどうかを取得または設定します。
	/// 初期値は true です。
	/// </summary>
	public BindableReactiveProperty<bool> OpenOutputAfterCompletion { get; }

	/// <summary>
	/// 製本完了後に対象作品を製本待ちから削除するかどうかを取得または設定します。
	/// </summary>
	public BindableReactiveProperty<bool> RemoveFromBindingQueueAfterCompletion { get; }

	/// <summary>
	/// 巻選択工程を完了し、製本前確認工程へ到達済みかどうかを取得または設定します。
	/// </summary>
	public BindableReactiveProperty<bool> VolumeSelectionCompleted { get; }

	/// <summary>
	/// 現在の製本セッションについて、巻選択画面で使用する Materials 等の初期状態構築が正常完了済みかどうかを取得または設定します。
	/// </summary>
	public BindableReactiveProperty<bool> VolumeSelectionInitialized { get; }

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
	/// Root.Children 直下の素材総数（フォルダ + 圧縮ファイル + EPUB）を取得します。
	/// MaterialFolderCount、MaterialArchiveCount、MaterialEpubCount から CombineLatest で自動導出されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<int> MaterialTotalCount { get; }

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
	/// 現在のBindingTargetに対応する、作品単位のWorkフォルダパスを取得または設定します。
	/// 製本セッション中の正本として機能し、初期値は string.Empty です。
	/// </summary>
	public BindableReactiveProperty<string> WorkSeriesFolderPath { get; }

	/// <summary>
	/// 製本前確認画面で表示する、作品単位の展開先フォルダ直下の巻フォルダ数を取得または設定します。
	/// </summary>
	public BindableReactiveProperty<int> WorkVolumeFolderCount { get; }

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
	/// SplitTargetVolume の変更時に、旧巻をOFFへ戻し、新巻へデフォルトを適用します。
	/// </summary>
	private void onSplitTargetVolumeChanged(BindingVolume? newVolume)
	{
		if (this.previousSplitTargetVolume is not null)
		{
			foreach (var image in this.previousSplitTargetVolume.Images)
			{
				image.IsSpreadSplitTarget.Value = false;
			}
		}

		if (newVolume is not null)
		{
			foreach (var image in newVolume.Images)
			{
				image.ApplyDefaultSpreadSplitTarget();
			}
		}

		this.previousSplitTargetVolume = newVolume;

		if (newVolume is null)
		{
			this.disposeSplitVolumes();
			this.SplitTrimHorizontalMaximum.Value = 0;
			this.SplitTrimVerticalMaximum.Value = 0;
			this.SplitOffsetMinimum.Value = 0;
			this.SplitOffsetMaximum.Value = 0;
		}
	}

	/// <summary>
	/// SplitVolumes 内の編集用 BindingVolume を Dispose し、SplitVolumes を Clear します。
	/// </summary>
	private void disposeSplitVolumes()
	{
		foreach (var volume in this.SplitVolumes)
		{
			volume.Dispose();
		}
		this.SplitVolumes.Clear();
	}

	/// <summary>
	/// <see cref="BindingStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	public BindingStore()
	{
		this.BindingTarget = new BindableReactiveProperty<BindingSeries?>(null)
			.AddTo(ref this.disposableBag);
		this.Materials = new ObservableList<MaterialItem>();
		this.BindingVolumes = new ObservableList<BindingVolume>();
		this.SplitVolumes = new ObservableList<BindingVolume>();
		this.volumeUpdated.AddTo(ref this.disposableBag);
		this.VolumeFolderDigits = new BindableReactiveProperty<int>(2)
			.AddTo(ref this.disposableBag);
		this.ZipOutputFileName = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
		this.CreateZip = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);
		this.OpenOutputAfterCompletion = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);
		this.RemoveFromBindingQueueAfterCompletion = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);
		this.VolumeSelectionCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.VolumeSelectionInitialized = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.SeriesInspectionCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.SplitTargetVolume = new BindableReactiveProperty<BindingVolume?>(null)
			.AddTo(ref this.disposableBag);
		this.SplitTrimHorizontalMaximum = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.SplitTrimVerticalMaximum = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.SplitOffsetMinimum = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.SplitOffsetMaximum = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.SplitTargetVolume.Subscribe(this.onSplitTargetVolumeChanged)
			.AddTo(ref this.disposableBag);

		// 素材サマリ
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

		// 素材総数を CombineLatest で導出
		this.MaterialTotalCount = Observable.CombineLatest(
			this.materialFolderCount,
			this.materialArchiveCount,
			this.materialEpubCount,
			(folder, archive, epub) => folder + archive + epub)
			.ToReadOnlyBindableReactiveProperty(0)
			.AddTo(ref this.disposableBag);
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

		// BindingVolumes 内の各 BindingVolume の VolumeNumber 変更を監視
		this.BindingVolumes.CollectionChanged += this.onBindingVolumesCollectionChangedForVolumeNumber;

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

		this.WorkSeriesFolderPath = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.WorkVolumeFolderCount = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		this.ImageExpansionMethod = new BindableReactiveProperty<global::MangaBinder.Bindings.ImageExpansionMethod>(
			global::MangaBinder.Bindings.ImageExpansionMethod.Recreate)
			.AddTo(ref this.disposableBag);

		this.ImageExpansionOptions = new[]
		{
			new ImageExpansionOption(
				global::MangaBinder.Bindings.ImageExpansionMethod.Recreate,
				"作品フォルダを新規作成（既存フォルダ削除）"),
			new ImageExpansionOption(
				global::MangaBinder.Bindings.ImageExpansionMethod.UseExisting,
				"既存のフォルダを再利用する"),
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
	/// BindingVolume を VolumeNumber の昇順（null 末尾）で、既存の並びを保ったまま1件だけ挿入します。
	/// </summary>
	/// <param name="bindingVolume">挿入対象の BindingVolume。</param>
	public void InsertBindingVolumeInOrder(BindingVolume bindingVolume)
	{
		var insertIndex = this.findInsertIndex(bindingVolume.VolumeNumber.Value, -1);
		this.BindingVolumes.Insert(insertIndex, bindingVolume);
	}

	/// <summary>
	/// 指定位置を除いた BindingVolumes に対する、VolumeNumber 順の挿入位置を返します。
	/// </summary>
	private int findInsertIndex(decimal? targetVolumeNumber, int ignoreIndex)
	{
		var position = 0;
		for (int i = 0; i < this.BindingVolumes.Count; i++)
		{
			if (i == ignoreIndex)
			{
				continue;
			}

			var existingVolumeNumber = this.BindingVolumes[i].VolumeNumber.Value;

			if (targetVolumeNumber is null)
			{
				if (existingVolumeNumber is null)
				{
					return position;
				}
			}
			else if (existingVolumeNumber is null || targetVolumeNumber < existingVolumeNumber)
			{
				return position;
			}

			position++;
		}

		return position;
	}

	/// <summary>
	/// VolumeNumber が変更された BindingVolume 1件だけを再配置します。
	/// </summary>
	private void repositionBindingVolume(BindingVolume volume)
	{
		var currentIndex = this.BindingVolumes.IndexOf(volume);
		if (currentIndex < 0)
		{
			return;
		}

		var newIndex = this.findInsertIndex(volume.VolumeNumber.Value, currentIndex);
		if (newIndex == currentIndex)
		{
			return;
		}

		this.BindingVolumes.RemoveAt(currentIndex);
		this.BindingVolumes.Insert(newIndex, volume);
	}

	private void subscribeVolumeNumber(BindingVolume volume)
	{
		if (this.volumeNumberSubscriptions.ContainsKey(volume))
		{
			return;
		}

		// Skip(1) で購読開始時の現在値通知を無視し、実際の変更のみ再配置の契機とする
		this.volumeNumberSubscriptions[volume] = volume.VolumeNumber
			.Skip(1)
			.Subscribe(_ => this.repositionBindingVolume(volume));
	}

	private void unsubscribeVolumeNumber(BindingVolume volume)
	{
		if (this.volumeNumberSubscriptions.Remove(volume, out var subscription))
		{
			subscription.Dispose();
		}
	}

	private void disposeAllVolumeNumberSubscriptions()
	{
		foreach (var subscription in this.volumeNumberSubscriptions.Values)
		{
			subscription.Dispose();
		}
		this.volumeNumberSubscriptions.Clear();
	}

	/// <summary>
	/// BindingVolumes の CollectionChanged イベントハンドラ。VolumeNumber の購読を追加・削除に同期します。
	/// </summary>
	private void onBindingVolumesCollectionChangedForVolumeNumber(in NotifyCollectionChangedEventArgs<BindingVolume> e)
	{
		switch (e.Action)
		{
			case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
				foreach (BindingVolume added in e.IsSingleItem ? new[] { e.NewItem } : e.NewItems.ToArray())
				{
					this.subscribeVolumeNumber(added);
				}
				break;

			case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
				foreach (BindingVolume removed in e.IsSingleItem ? new[] { e.OldItem } : e.OldItems.ToArray())
				{
					this.unsubscribeVolumeNumber(removed);
				}
				break;

			case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
				foreach (BindingVolume removed in e.IsSingleItem ? new[] { e.OldItem } : e.OldItems.ToArray())
				{
					this.unsubscribeVolumeNumber(removed);
				}
				foreach (BindingVolume added in e.IsSingleItem ? new[] { e.NewItem } : e.NewItems.ToArray())
				{
					this.subscribeVolumeNumber(added);
				}
				break;

			case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
				this.disposeAllVolumeNumberSubscriptions();
				break;
		}
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
		// 編集用 Clone の終了（SplitVolumes は購読により破棄される）
		this.SplitTargetVolume.Value = null;

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
		this.HasExistingWorkFolder.Value = false;
		this.WorkSeriesFolderPath.Value = string.Empty;
		this.WorkVolumeFolderCount.Value = 0;
		this.ImageExpansionMethod.Value = global::MangaBinder.Bindings.ImageExpansionMethod.Recreate;
		this.VolumeFolderDigits.Value = 2;
		this.ZipOutputFileName.Value = string.Empty;
		this.CreateZip.Value = true;
		this.OpenOutputAfterCompletion.Value = true;
		this.RemoveFromBindingQueueAfterCompletion.Value = true;
		this.VolumeSelectionCompleted.Value = false;
		this.VolumeSelectionInitialized.Value = false;
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
		this.BindingVolumes.CollectionChanged -= this.onBindingVolumesCollectionChangedForVolumeNumber;
		this.disposeAllVolumeNumberSubscriptions();

		// 所有関係に従って破棄する
		// 編集用 Clone は Material を参照するだけなので、先に破棄
		this.disposeSplitVolumes();

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
