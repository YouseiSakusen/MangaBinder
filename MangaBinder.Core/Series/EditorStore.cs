using MangaBinder.Settings;
using ObservableCollections;
using R3;

namespace MangaBinder.Series;

/// <summary>
/// 作品編集画面の編集セッション状態を保持する Singleton ストアです。
/// 編集開始時に Manager が準備した状態を保持し、画面はこのストアを参照して表示します。
/// <see cref="EditTarget"/> に null が設定されると、ストア自身が編集セッション固有の状態をクリアします。
/// </summary>
public sealed class EditorStore : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// 編集用クローンの <see cref="MangaSeries"/> を取得します。
	/// null を設定すると、編集セッション固有の状態がすべてクリアされます。
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

	/// <summary>編集開始時点の素材合計サイズ（バイト）を取得します。</summary>
	public long MaterialTotalSizeBytes { get; private set; }

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

		// EditTarget が null になったら、編集セッション固有の状態を自身でクリアする
		this.EditTarget
			.Where(static editTarget => editTarget is null)
			.Subscribe(_ => this.clearSession())
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 編集セッションを初期化します。
	/// 前回のセッション状態をクリアしたうえで各状態を設定し、最後に <see cref="EditTarget"/> を設定します。
	/// 先に null クリアを行うため、初期化済みのデータが null クリアで消えることはありません。
	/// </summary>
	/// <param name="originalSeries">クローン元の作品。</param>
	/// <param name="editTarget">編集用クローン。</param>
	/// <param name="notifier">編集用クローン専用の Notifier。このストアが所有します。</param>
	/// <param name="materialFiles">初期ソート済みの素材一覧。</param>
	/// <param name="materialSourceFolders">登録先素材フォルダ候補。</param>
	/// <param name="selectedMaterialSourceFolder">初期選択の登録先素材フォルダ。</param>
	/// <param name="canSelectMaterialSourceFolder">登録先素材フォルダを変更できるかどうか。</param>
	/// <param name="isBindingQueued">製本待ち状態。</param>
	/// <param name="materialTotalSizeBytes">素材合計サイズ（バイト）。</param>
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
		this.MaterialTotalSizeBytes = materialTotalSizeBytes;
		this.MaterialSource = materialSource;

		// 全状態の設定後に EditTarget を設定する
		this.EditTarget.Value = editTarget;
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
		this.MaterialTotalSizeBytes = 0;
		this.MaterialSource = null;

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
