using MangaBinder.Settings;
using ObservableCollections;
using R3;

namespace MangaBinder.Series;

/// <summary>
/// 作品編集画面の編集セッション状態を保持する Singleton ストアです。
/// 編集開始時に Manager が準備した状態を保持し、画面はこのストアを参照して表示します。
/// <see cref="EditTarget"/> に null が設定されると、ストア自身が編集セッション固有の状態をクリアします。
/// NumberBox の入力状態・素材サイズ・ボタンの有効状態は、ストア内部で Reactive に監視して更新します。
/// </summary>
public sealed class EditorStore : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>編集セッションの初期化中、または自動推定値の設定中は、入力値の変更を編集用クローンへ反映しません。</summary>
	private bool isApplyingInternally;

	/// <summary>
	/// 編集用クローンの <see cref="MangaSeries"/> を取得します。
	/// null を設定すると、編集セッション固有の状態がすべてクリアされます。
	/// タイトル等の通常の編集項目は、画面がこのインスタンスへ直接バインドします。
	/// </summary>
	public BindableReactiveProperty<MangaSeries?> EditTarget { get; }

	/// <summary>
	/// <see cref="EditTarget"/> のクローン元になった <see cref="MangaSeries"/> を取得します。
	/// 正式登録済み・登録待ち作品の場合は <c>MangaSeriesStore</c> が所有する正本、新規作品の場合は呼び出し元が作成したインスタンスです。
	/// 編集中にこのインスタンスを変更してはいけません。
	/// </summary>
	public MangaSeries? OriginalSeries { get; private set; }

	/// <summary>
	/// 編集用クローン専用の <see cref="MangaSeriesNotifier"/> を取得します。
	/// 正本の共有 Notifier ではなく、このストアが所有して <see cref="EditTarget"/> のクリア時に破棄します。
	/// </summary>
	public MangaSeriesNotifier? EditTargetNotifier { get; private set; }

	/// <summary>素材一覧の正本を取得します。</summary>
	public ObservableList<MaterialFileItem> MaterialFiles { get; } = new();

	/// <summary>登録先に選択可能な素材フォルダ候補を取得します。要素は <c>AppSettings</c> 所有のため破棄しません。</summary>
	public ObservableList<SourceFolder> MaterialSourceFolders { get; } = new();

	/// <summary>選択中の登録先素材フォルダを取得します。</summary>
	public BindableReactiveProperty<SourceFolder?> SelectedMaterialSourceFolder { get; }

	/// <summary>登録先素材フォルダを変更できるかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> CanSelectMaterialSourceFolder { get; }

	/// <summary>製本待ちに登録するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> IsBindingQueued { get; }

	/// <summary>素材の合計サイズ（バイト）を取得します。サイズ確定済みの素材だけを合計します。</summary>
	public BindableReactiveProperty<long> MaterialTotalSizeBytes { get; }

	/// <summary>素材サイズを計算中の素材が存在するかどうかを取得します。<see cref="MaterialFiles"/> の変更から自動で更新されます。</summary>
	public BindableReactiveProperty<bool> IsMaterialSizeCalculating { get; }

	/// <summary>開始巻の入力値を取得します。NumberBox の <c>Value</c> と同じ型です。</summary>
	public BindableReactiveProperty<double?> StartVolume { get; }

	/// <summary>完結巻の入力値を取得します。未入力の場合は null です。</summary>
	public BindableReactiveProperty<double?> EndVolume { get; }

	/// <summary>所持推定巻数の入力値を取得します。未入力の場合は null です。</summary>
	public BindableReactiveProperty<double?> OwnedMaxVolume { get; }

	/// <summary>全巻所持しているかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> IsOwnedCompleted { get; }

	/// <summary>全巻所持を編集できるかどうかを取得します。完結巻が入力されている場合のみ編集できます。</summary>
	public BindableReactiveProperty<bool> CanEditOwnedCompleted { get; }

	/// <summary>未完（連載中・不完全）かどうかを取得します。左の作品カードのバッジ色に反映されます。</summary>
	public BindableReactiveProperty<bool> IsIncomplete { get; }

	/// <summary>登録ボタンを実行できるかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> RegisterSeriesCommandCanExecute { get; }

	/// <summary>一時保存ボタンを実行できるかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> SaveWorkSeriesCommandCanExecute { get; }

	/// <summary>キャンセル操作を実行できるかどうかを取得します。素材サイズ計算中は実行できません。</summary>
	public BindableReactiveProperty<bool> CancelCommandCanExecute { get; }

	/// <summary>
	/// 作品の削除操作を表示するかどうかを取得します。
	/// 編集対象が存在し、SeriesId != 0 または WorkId != 0 の場合に true です。
	/// 製本待ち登録中などの実際の削除可否は表さず、削除実行時に別途確認します。
	/// </summary>
	public BindableReactiveProperty<bool> CanDeleteSeries { get; }

	/// <summary>保存用のサムネイル画像（JPEG）を取得または設定します。サムネイルが指定されていない場合は null です。</summary>
	public byte[]? ThumbnailJpegBytes { get; set; }

	/// <summary>
	/// 編集対象として特定した、実在する素材フォルダの <see cref="MangaSource"/> を取得します。
	/// 素材フォルダを持たない作品の場合は null です。
	/// </summary>
	public MangaSource? MaterialSource { get; private set; }

	/// <summary>
	/// <see cref="EditorStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	public EditorStore()
	{
		this.EditTarget = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);

		this.SelectedMaterialSourceFolder = new BindableReactiveProperty<SourceFolder?>(null)
			.AddTo(ref this.disposableBag);

		this.CanSelectMaterialSourceFolder = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.IsBindingQueued = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.MaterialTotalSizeBytes = new BindableReactiveProperty<long>(0)
			.AddTo(ref this.disposableBag);

		this.IsMaterialSizeCalculating = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.StartVolume = new BindableReactiveProperty<double?>(1)
			.AddTo(ref this.disposableBag);

		this.EndVolume = new BindableReactiveProperty<double?>(null)
			.AddTo(ref this.disposableBag);

		this.OwnedMaxVolume = new BindableReactiveProperty<double?>(null)
			.AddTo(ref this.disposableBag);

		this.IsOwnedCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.CanEditOwnedCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.IsIncomplete = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.RegisterSeriesCommandCanExecute = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.SaveWorkSeriesCommandCanExecute = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.CancelCommandCanExecute = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);

		this.CanDeleteSeries = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// EditTarget が null になったら、編集セッション固有の状態を自身でクリアする
		this.EditTarget
			.Where(static editTarget => editTarget is null)
			.Subscribe(_ => this.clearSession())
			.AddTo(ref this.disposableBag);

		// 編集対象・素材一覧の変更から、素材サイズ計算中の状態とボタンの有効状態を更新する
		this.EditTarget
			.Subscribe(_ => this.updateButtonStates())
			.AddTo(ref this.disposableBag);

		this.MaterialFiles.ObserveChanged()
			.Subscribe(_ => this.updateButtonStates())
			.AddTo(ref this.disposableBag);

		// NumberBox 等の入力値の変更を、編集用クローンへ反映して左の作品カードへ通知する
		this.StartVolume
			.Subscribe(this.onStartVolumeChanged)
			.AddTo(ref this.disposableBag);

		this.EndVolume
			.Subscribe(this.onEndVolumeChanged)
			.AddTo(ref this.disposableBag);

		this.OwnedMaxVolume
			.Subscribe(this.onOwnedMaxVolumeChanged)
			.AddTo(ref this.disposableBag);

		this.IsOwnedCompleted
			.Subscribe(this.onIsOwnedCompletedChanged)
			.AddTo(ref this.disposableBag);

		this.IsIncomplete
			.Subscribe(this.onIsIncompleteChanged)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 編集セッションを初期化します。
	/// 前回のセッション状態をクリアしたうえで各状態を設定し、最後に <see cref="EditTarget"/> を設定します。
	/// 先に null クリアを行うため、初期化済みのデータが null クリアで消えることはありません。
	/// 巻情報の入力値は <paramref name="editTarget"/> から設定します。この設定では編集用クローンの更新・通知・手動変更判定を行いません。
	/// </summary>
	/// <param name="originalSeries">クローン元の作品。</param>
	/// <param name="editTarget">編集用クローン。</param>
	/// <param name="notifier">編集用クローン専用の Notifier。このストアが所有します。</param>
	/// <param name="materialFiles">初期ソート済みの素材一覧。サイズ未確定の素材を含められます。</param>
	/// <param name="materialSourceFolders">登録先素材フォルダ候補。</param>
	/// <param name="selectedMaterialSourceFolder">初期選択の登録先素材フォルダ。</param>
	/// <param name="canSelectMaterialSourceFolder">登録先素材フォルダを変更できるかどうか。</param>
	/// <param name="isBindingQueued">製本待ち状態。</param>
	/// <param name="materialTotalSizeBytes">サイズ確定済みの素材の合計サイズ（バイト）。</param>
	/// <param name="materialSource">編集対象として特定した実在する素材フォルダの MangaSource。</param>
	/// <exception cref="InvalidOperationException">クローン元とクローンが同一、または Notifier がクローンを保持していない場合。</exception>
	public void Initialize(
		MangaSeries originalSeries,
		MangaSeries editTarget,
		MangaSeriesNotifier notifier,
		IReadOnlyList<MaterialFileItem> materialFiles,
		IReadOnlyList<SourceFolder> materialSourceFolders,
		SourceFolder? selectedMaterialSourceFolder,
		bool canSelectMaterialSourceFolder,
		bool isBindingQueued,
		long materialTotalSizeBytes,
		MangaSource? materialSource)
	{
		ArgumentNullException.ThrowIfNull(originalSeries);
		ArgumentNullException.ThrowIfNull(editTarget);
		ArgumentNullException.ThrowIfNull(notifier);
		ArgumentNullException.ThrowIfNull(materialFiles);
		ArgumentNullException.ThrowIfNull(materialSourceFolders);

		if (ReferenceEquals(originalSeries, editTarget))
		{
			throw new InvalidOperationException("EditTarget には OriginalSeries と別のクローンインスタンスを指定する必要があります。");
		}

		if (!ReferenceEquals(notifier.Series.Value, editTarget))
		{
			throw new InvalidOperationException("EditTargetNotifier は EditTarget のクローンを保持している必要があります。");
		}

		// 前回のセッションをクリアしてから各状態を設定する
		this.EditTarget.Value = null;

		this.OriginalSeries = originalSeries;
		this.EditTargetNotifier = notifier;
		this.MaterialFiles.AddRange(materialFiles);
		this.MaterialSourceFolders.AddRange(materialSourceFolders);
		this.SelectedMaterialSourceFolder.Value = selectedMaterialSourceFolder;
		this.CanSelectMaterialSourceFolder.Value = canSelectMaterialSourceFolder;
		this.IsBindingQueued.Value = isBindingQueued;
		this.MaterialTotalSizeBytes.Value = materialTotalSizeBytes;
		this.MaterialSource = materialSource;

		// 巻情報の入力値を設定する（編集用クローンへの反映と通知は行わない）
		this.isApplyingInternally = true;
		try
		{
			this.StartVolume.Value = editTarget.StartVolume;
			this.EndVolume.Value = VolumeInputConverter.ToInputValue(editTarget.EndVolume);
			this.OwnedMaxVolume.Value = VolumeInputConverter.ToInputValue(editTarget.OwnedMaxVolume);
			this.IsOwnedCompleted.Value = editTarget.IsOwnedCompleted;
			this.CanEditOwnedCompleted.Value = editTarget.EndVolume > 0;
			this.IsIncomplete.Value = editTarget.IsIncomplete;
		}
		finally
		{
			this.isApplyingInternally = false;
		}

		// 全状態の設定後に EditTarget を設定する
		this.EditTarget.Value = editTarget;
	}

	/// <summary>
	/// 自動推定した所持最大巻数を、手動変更として扱わずに反映します。
	/// 編集用クローンと入力値の両方を更新し、左の作品カードへ通知します。
	/// </summary>
	/// <param name="ownedMaxVolume">推定した所持最大巻数。0 以下の場合は推定不能として現在の値を維持します。</param>
	/// <returns>所持最大巻数を更新した場合は true。</returns>
	public bool SetEstimatedOwnedMaxVolume(int ownedMaxVolume)
	{
		if (ownedMaxVolume <= 0 || this.EditTarget.Value is not { } target)
		{
			return false;
		}

		this.isApplyingInternally = true;
		try
		{
			target.OwnedMaxVolume = ownedMaxVolume;
			this.OwnedMaxVolume.Value = ownedMaxVolume;
		}
		finally
		{
			this.isApplyingInternally = false;
		}

		this.notifyEditTargetChanged();
		return true;
	}

	/// <summary>
	/// 非同期で計算した素材サイズを反映します。
	/// 計算を開始した編集対象と現在の編集対象が異なる場合（編集セッションが終了・切替済み）は、何も反映しません。
	/// </summary>
	/// <param name="calculatedFor">サイズ計算を開始した時点の編集対象。</param>
	/// <param name="fullPath">サイズを確定した素材のフルパス。</param>
	/// <param name="sizeBytes">確定したサイズ（バイト）。</param>
	public void ApplyCalculatedMaterialSize(MangaSeries calculatedFor, string fullPath, long sizeBytes)
	{
		ArgumentNullException.ThrowIfNull(calculatedFor);

		if (!ReferenceEquals(this.EditTarget.Value, calculatedFor))
		{
			return;
		}

		var index = -1;
		for (var i = 0; i < this.MaterialFiles.Count; i++)
		{
			if (string.Equals(this.MaterialFiles[i].FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
			{
				index = i;
				break;
			}
		}

		if (index < 0)
		{
			return;
		}

		// 計算中の素材は削除できないため、見つからないケースは発生しない。合計の加算後に要素を置換して表示へ通知する
		this.MaterialTotalSizeBytes.Value += sizeBytes;
		this.MaterialFiles[index] = this.MaterialFiles[index].WithSizeBytes(sizeBytes);
	}

	/// <summary>
	/// 編集用クローンの変更を、編集用 Notifier 経由で左の作品カードへ通知します。
	/// </summary>
	private void notifyEditTargetChanged()
		=> this.EditTargetNotifier?.Series.ForceNotify();

	/// <summary>
	/// 開始巻の入力値を編集用クローンへ反映します。
	/// </summary>
	/// <param name="inputValue">NumberBox の入力値。</param>
	private void onStartVolumeChanged(double? inputValue)
	{
		if (this.isApplyingInternally || this.EditTarget.Value is not { } target)
		{
			return;
		}

		// 開始巻は 1 以上とする
		target.StartVolume = Math.Max(1, VolumeInputConverter.ToSeriesVolume(inputValue));
		this.notifyEditTargetChanged();
	}

	/// <summary>
	/// 完結巻の入力値を編集用クローンへ反映し、関連状態（完結フラグ・全巻所持の編集可否）を更新します。
	/// </summary>
	/// <param name="inputValue">NumberBox の入力値。未入力の場合は null。</param>
	private void onEndVolumeChanged(double? inputValue)
	{
		if (this.isApplyingInternally || this.EditTarget.Value is not { } target)
		{
			return;
		}

		var endVolume = VolumeInputConverter.ToSeriesVolume(inputValue);
		if (endVolume >= 1)
		{
			target.EndVolume = endVolume;
			target.SeriesCompleted = true;
			this.CanEditOwnedCompleted.Value = true;
		}
		else
		{
			target.EndVolume = 0;
			target.SeriesCompleted = false;
			target.IsOwnedCompleted = false;
			this.CanEditOwnedCompleted.Value = false;
			this.IsOwnedCompleted.Value = false;
		}

		this.notifyEditTargetChanged();
	}

	/// <summary>
	/// ユーザー操作による所持推定巻数の変更を編集用クローンへ反映し、値が変わった場合は手動変更として扱います。
	/// </summary>
	/// <param name="inputValue">NumberBox の入力値。未入力の場合は null。</param>
	private void onOwnedMaxVolumeChanged(double? inputValue)
	{
		if (this.isApplyingInternally || this.EditTarget.Value is not { } target)
		{
			return;
		}

		var ownedMaxVolume = VolumeInputConverter.ToSeriesVolume(inputValue);
		if (ownedMaxVolume != target.OwnedMaxVolume)
		{
			target.OwnedMaxVolume = ownedMaxVolume;
			target.IsOwnedMaxVolumeManuallyEdited = true;
		}

		this.notifyEditTargetChanged();
	}

	/// <summary>
	/// 全巻所持の変更を編集用クローンへ反映します。
	/// </summary>
	/// <param name="isOwnedCompleted">全巻所持かどうか。</param>
	private void onIsOwnedCompletedChanged(bool isOwnedCompleted)
	{
		if (this.isApplyingInternally || this.EditTarget.Value is not { } target || target.IsOwnedCompleted == isOwnedCompleted)
		{
			return;
		}

		target.IsOwnedCompleted = isOwnedCompleted;
		this.notifyEditTargetChanged();
	}

	/// <summary>
	/// 未完かどうかの変更を編集用クローンへ反映します。
	/// </summary>
	/// <param name="isIncomplete">未完かどうか。</param>
	private void onIsIncompleteChanged(bool isIncomplete)
	{
		if (this.isApplyingInternally || this.EditTarget.Value is not { } target || target.IsIncomplete == isIncomplete)
		{
			return;
		}

		target.IsIncomplete = isIncomplete;
		this.notifyEditTargetChanged();
	}

	/// <summary>
	/// 素材サイズ計算中の状態と、登録・一時保存・キャンセルの有効状態、削除操作の表示状態を更新します。
	/// 素材サイズの計算中は、登録・一時保存・キャンセルを無効にします。
	/// </summary>
	private void updateButtonStates()
	{
		var target = this.EditTarget.Value;

		this.CanDeleteSeries.Value = target is not null && (target.SeriesId != 0 || target.WorkId != 0);
		var isCalculating = target is not null && this.MaterialFiles.Any(static item => item.IsSizePending);

		this.IsMaterialSizeCalculating.Value = isCalculating;
		this.CancelCommandCanExecute.Value = !isCalculating;

		if (target is null || isCalculating)
		{
			this.RegisterSeriesCommandCanExecute.Value = false;
			this.SaveWorkSeriesCommandCanExecute.Value = false;
			return;
		}

		// 既存の正式作品は常に登録のみ。新規・登録待ちは素材の有無で登録と一時保存を切り替える
		var isExistingSeries = target.SeriesId != 0 && !target.IsWork;
		var hasMaterials = this.MaterialFiles.Count > 0;

		this.RegisterSeriesCommandCanExecute.Value = isExistingSeries || hasMaterials;
		this.SaveWorkSeriesCommandCanExecute.Value = !isExistingSeries && !hasMaterials;
	}

	/// <summary>
	/// 編集セッション固有の状態をクリアします。
	/// <c>MangaSeriesStore</c> や <c>TagStore</c> が所有する正本・共有インスタンスは破棄しません。
	/// </summary>
	private void clearSession()
	{
		this.OriginalSeries = null;
		this.MaterialFiles.Clear();
		this.MaterialSourceFolders.Clear();
		this.SelectedMaterialSourceFolder.Value = null;
		this.CanSelectMaterialSourceFolder.Value = false;
		this.IsBindingQueued.Value = false;
		this.MaterialTotalSizeBytes.Value = 0;
		this.MaterialSource = null;
		this.ThumbnailJpegBytes = null;

		this.isApplyingInternally = true;
		try
		{
			this.StartVolume.Value = 1;
			this.EndVolume.Value = null;
			this.OwnedMaxVolume.Value = null;
			this.IsOwnedCompleted.Value = false;
			this.CanEditOwnedCompleted.Value = false;
			this.IsIncomplete.Value = false;
		}
		finally
		{
			this.isApplyingInternally = false;
		}

		// 編集用クローン専用の Notifier のみ破棄する（タグマスタの共有 TagNotifier は破棄しない）
		this.EditTargetNotifier?.Dispose();
		this.EditTargetNotifier = null;
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.clearSession();
		this.disposableBag.Dispose();
	}
}
