using MangaBinder.Bindings;
using R3;

namespace MangaBinder.Series;

/// <summary>
/// 編集ページの ViewModel です。
/// </summary>
public class EditorPageViewModel : IDataInitializable, IDisposable
{
	private readonly MangaSeriesManager seriesManager;
	private readonly SeriesWorkspaceStore workspaceStore;
	private DisposableBag disposableBag;

	/// <summary>編集対象の Series を取得します。</summary>
	public BindableReactiveProperty<MangaSeries?> EditingSeries { get; }

	/// <summary>タイトルを取得または設定します。</summary>
	public BindableReactiveProperty<string?> Title { get; }

	/// <summary>作者を取得または設定します。</summary>
	public BindableReactiveProperty<string?> Author { get; }

	/// <summary>出版社を取得または設定します。</summary>
	public BindableReactiveProperty<string?> Publisher { get; }

	/// <summary>開始巻を取得または設定します。</summary>
	public BindableReactiveProperty<int> StartVolume { get; }

	/// <summary>完結巻を取得または設定します。</summary>
	public BindableReactiveProperty<int> EndVolume { get; }

	/// <summary>シリーズが完結しているかどうかを取得または設定します。</summary>
	public BindableReactiveProperty<bool> SeriesCompleted { get; }

	/// <summary>全巻所持しているかどうかを取得または設定します。</summary>
	public BindableReactiveProperty<bool> IsOwnedCompleted { get; }

	/// <summary>所持推定巻数を取得または設定します。</summary>
	public BindableReactiveProperty<int?> OwnedMaxVolume { get; }

	/// <summary>製本済み巻数を取得または設定します。</summary>
	public BindableReactiveProperty<int?> BoundEndVolume { get; }

	/// <summary>説明を取得または設定します。</summary>
	public BindableReactiveProperty<string?> Description { get; }

	/// <summary>メモを取得または設定します。</summary>
	public BindableReactiveProperty<string?> Memo { get; }

	/// <summary>全巻所持が編集可能かどうかを取得します。完結巻が 0 ではない場合のみ編集可能です。</summary>
	public ReadOnlyReactiveProperty<bool> CanEditOwnedCompleted { get; }

	/// <summary>タイトル入力欄へのフォーカス要求を取得します。</summary>
	public BindableReactiveProperty<int> TitleFocusRequest { get; }

	/// <summary>
	/// EditorPageViewModel の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="seriesManager">作品管理マネージャー。</param>
	/// <param name="workspaceStore">作業領域ストア。</param>
	public EditorPageViewModel(MangaSeriesManager seriesManager, SeriesWorkspaceStore workspaceStore)
	{
		this.seriesManager = seriesManager ?? throw new ArgumentNullException(nameof(seriesManager));
		this.workspaceStore = workspaceStore ?? throw new ArgumentNullException(nameof(workspaceStore));

		this.EditingSeries = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);

		this.Title = new BindableReactiveProperty<string?>(null)
			.AddTo(ref this.disposableBag);

		this.Author = new BindableReactiveProperty<string?>(null)
			.AddTo(ref this.disposableBag);

		this.Publisher = new BindableReactiveProperty<string?>(null)
			.AddTo(ref this.disposableBag);

		this.StartVolume = new BindableReactiveProperty<int>(1)
			.AddTo(ref this.disposableBag);

		this.EndVolume = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		this.SeriesCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.IsOwnedCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.OwnedMaxVolume = new BindableReactiveProperty<int?>(null)
			.AddTo(ref this.disposableBag);

		this.BoundEndVolume = new BindableReactiveProperty<int?>(null)
			.AddTo(ref this.disposableBag);

		this.Description = new BindableReactiveProperty<string?>(null)
			.AddTo(ref this.disposableBag);

		this.Memo = new BindableReactiveProperty<string?>(null)
			.AddTo(ref this.disposableBag);

		// CanEditOwnedCompleted: EndVolume が 0 ではない場合のみ true
		this.CanEditOwnedCompleted = this.EndVolume
			.Select(e => e != 0)
			.ToReadOnlyReactiveProperty()
			.AddTo(ref this.disposableBag);

		this.TitleFocusRequest = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		// ReactiveProperty の値が変更されたら、編集対象 MangaSeries に反映
		this.setupSynchronization();
	}

	/// <summary>
	/// 指定された作品の編集を開始します。
	/// 編集セッション管理は SeriesManager に委譲します。
	/// </summary>
	/// <param name="series">編集対象の作品。</param>
	public void StartEdit(MangaSeries series)
	{
		ArgumentNullException.ThrowIfNull(series);

		// 編集セッション開始を SeriesManager に依頼
		this.seriesManager.BeginEdit(series);

		// SeriesManager から編集対象を取得
		var editingSeries = this.seriesManager.GetEditingSeries();
		this.EditingSeries.Value = editingSeries;

		// ReactiveProperty に値を設定
		if (editingSeries != null)
		{
			this.Title.Value = editingSeries.Title;
			this.Author.Value = editingSeries.Author;
			this.Publisher.Value = editingSeries.Publisher;
			this.StartVolume.Value = editingSeries.StartVolume;
			this.EndVolume.Value = editingSeries.EndVolume;
			this.SeriesCompleted.Value = editingSeries.SeriesCompleted;
			this.IsOwnedCompleted.Value = editingSeries.IsOwnedCompleted;
			this.OwnedMaxVolume.Value = editingSeries.OwnedMaxVolume > 0 ? editingSeries.OwnedMaxVolume : null;
			this.BoundEndVolume.Value = editingSeries.BoundEndVolume > 0 ? editingSeries.BoundEndVolume : null;
			this.Description.Value = editingSeries.Description;
			this.Memo.Value = editingSeries.Memo;

			// タイトル入力欄へのフォーカスを要求
			this.TitleFocusRequest.Value++;
		}
	}

	/// <summary>
	/// ReactiveProperty の値を編集対象 MangaSeries に同期します。
	/// </summary>
	private void setupSynchronization()
	{
		// Title の変更を MangaSeries に反映
		this.Title
			.Subscribe(value =>
			{
				if (this.EditingSeries.Value != null)
					this.EditingSeries.Value.Title = value ?? string.Empty;
			})
			.AddTo(ref this.disposableBag);

		// Author の変更を MangaSeries に反映
		this.Author
			.Subscribe(value =>
			{
				if (this.EditingSeries.Value != null)
					this.EditingSeries.Value.Author = value ?? string.Empty;
			})
			.AddTo(ref this.disposableBag);

		// Publisher の変更を MangaSeries に反映
		this.Publisher
			.Subscribe(value =>
			{
				if (this.EditingSeries.Value != null)
					this.EditingSeries.Value.Publisher = value ?? string.Empty;
			})
			.AddTo(ref this.disposableBag);

		// StartVolume の変更を MangaSeries に反映
		this.StartVolume
			.Subscribe(value =>
			{
				if (this.EditingSeries.Value != null)
					this.EditingSeries.Value.StartVolume = value;
			})
			.AddTo(ref this.disposableBag);

		// EndVolume の変更を MangaSeries に反映
		this.EndVolume
			.Subscribe(value =>
			{
				if (this.EditingSeries.Value != null)
					this.EditingSeries.Value.EndVolume = value;
			})
			.AddTo(ref this.disposableBag);

		// SeriesCompleted の変更を MangaSeries に反映
		this.SeriesCompleted
			.Subscribe(value =>
			{
				if (this.EditingSeries.Value != null)
					this.EditingSeries.Value.SeriesCompleted = value;
			})
			.AddTo(ref this.disposableBag);

		// IsOwnedCompleted の変更を MangaSeries に反映
		this.IsOwnedCompleted
			.Subscribe(value =>
			{
				if (this.EditingSeries.Value != null)
					this.EditingSeries.Value.IsOwnedCompleted = value;
			})
			.AddTo(ref this.disposableBag);

		// OwnedMaxVolume の変更を MangaSeries に反映
		this.OwnedMaxVolume
			.Subscribe(value =>
			{
				if (this.EditingSeries.Value != null)
					this.EditingSeries.Value.OwnedMaxVolume = value ?? 0;
			})
			.AddTo(ref this.disposableBag);

		// BoundEndVolume の変更を MangaSeries に反映
		this.BoundEndVolume
			.Subscribe(value =>
			{
				if (this.EditingSeries.Value != null)
					this.EditingSeries.Value.BoundEndVolume = value ?? 0;
			})
			.AddTo(ref this.disposableBag);

		// Description の変更を MangaSeries に反映
		this.Description
			.Subscribe(value =>
			{
				if (this.EditingSeries.Value != null)
					this.EditingSeries.Value.Description = value ?? string.Empty;
			})
			.AddTo(ref this.disposableBag);

		// Memo の変更を MangaSeries に反映
		this.Memo
			.Subscribe(value =>
			{
				if (this.EditingSeries.Value != null)
					this.EditingSeries.Value.Memo = value ?? string.Empty;
			})
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// ナビゲーション完了後に呼ばれる初期データ読み込み処理。
	/// workspaceStore.EditTarget から編集対象を取得し、StartEdit を実行します。
	/// </summary>
	public ValueTask InitializeDataAsync()
	{
		var editTarget = this.workspaceStore.EditTarget;

		if (editTarget is not null)
		{
			this.StartEdit(editTarget);
		}

		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
