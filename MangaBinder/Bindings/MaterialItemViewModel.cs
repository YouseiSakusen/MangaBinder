using ObservableCollections;
using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// BindingStore.Materials の MaterialItem を TreeView へ表示するための UI 用 ViewModel です。
/// MaterialItem の状態そのものをコピーせず、必ず同じ MaterialItem インスタンスを参照し、
/// UI表示に必要な状態だけを上乗せします。
/// </summary>
public class MaterialItemViewModel : IDisposable
{
	private DisposableBag disposableBag;
	private ISynchronizedView<MaterialItem, MaterialItemViewModel>? childrenView;

	/// <summary>
	/// この ViewModel の元となった素材を取得します。
	/// BindingStore.Materials 内に存在する MaterialItem と同一インスタンスを参照します。
	/// </summary>
	public MaterialItem Material { get; init; }

	/// <summary>
	/// 子素材の表示用 ViewModel 一覧を取得します。
	/// Material.Children から自動的に変換され、
	/// Children の Add / Remove / Clear 等の変更に自動追従します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<MaterialItemViewModel> Children { get; }

	/// <summary>
	/// この素材が TreeView で展開されているかどうかを取得または設定します。
	/// </summary>
	public BindableReactiveProperty<bool> IsExpanded { get; }

	/// <summary>
	/// ユーザー判断で一時的に選択可能にするオーバーライドが有効かどうかを取得または設定します。
	/// Material.IsSelectableByDefault == false な素材をユーザーが右クリック等で
	/// 一時的に選択可能にするための救済措置です。
	/// </summary>
	public BindableReactiveProperty<bool> IsSelectionOverrideEnabled { get; }

	/// <summary>
	/// この素材がチェック可能かどうかを取得します。
	/// Material.ItemType と Material.IsSelectableByDefault、IsSelectionOverrideEnabled から導出される UI 用状態です。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<bool> CanCheck { get; }

	/// <summary>
	/// この素材に対して「チェックを有効にする」操作を使用できるかを取得します。
	/// </summary>
	public bool CanEnableSelectionOverride
		=> this.Material.CanEnableSelectionOverride;

	/// <summary>
	/// この素材に対して「素材を削除」操作を使用できるかを取得します。
	/// </summary>
	public bool CanDeleteMaterial
		=> this.Material.CanDeleteMaterial;

	/// <summary>
	/// 選択不可と判定された素材を、ユーザー判断で一時的に選択可能にするコマンドです。
	/// 実行時に IsSelectionOverrideEnabled を true に設定します。
	/// </summary>
	public ReactiveCommand EnableSelectionOverrideCommand { get; }

	/// <summary>
	/// <see cref="MaterialItemViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="material">表示対象の素材。BindingStore.Materials 内に存在するインスタンスを指定してください。</param>
	public MaterialItemViewModel(MaterialItem material)
	{
		this.Material = material ?? throw new ArgumentNullException(nameof(material));

		// IsExpanded: UI状態、初期値は true（基本的に全展開で表示）
		this.IsExpanded = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);

		// IsSelectionOverrideEnabled: UI状態、初期値は false
		this.IsSelectionOverrideEnabled = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// CanCheck: Material.ItemType と Material.IsSelectableByDefault、IsSelectionOverrideEnabled から派生
		this.CanCheck = this.createCanCheckReactiveProperty()
			.AddTo(ref this.disposableBag);

		// EnableSelectionOverrideCommand: 実行時に IsSelectionOverrideEnabled を true に
		this.EnableSelectionOverrideCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.EnableSelectionOverrideCommand.Subscribe(_ =>
		{
			if (!this.Material.CanEnableSelectionOverride)
			{
				return;
			}

			this.IsSelectionOverrideEnabled.Value = true;
		})
		.AddTo(ref this.disposableBag);

		// Children: Material.Children を MaterialItemViewModel へ変換
		// CreateView で ISynchronizedView を生成し、ViewChanged で子ViewModel のライフサイクル管理
		var childrenView = this.Material.Children
			.CreateView(childMaterial => new MaterialItemViewModel(childMaterial));

		this.childrenView = childrenView;

		this.Children = childrenView
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// ViewChanged イベントで削除・置換・ソート時に子 ViewModel を Dispose
		childrenView.ViewChanged += this.onChildrenViewChanged;
	}

	/// <summary>
	/// Children の ViewChanged イベント ハンドラ。
	/// ISynchronizedView での削除・置換・ソート時に子 MaterialItemViewModel を適切に Dispose する。
	/// </summary>
	private void onChildrenViewChanged(in SynchronizedViewChangedEventArgs<MaterialItem, MaterialItemViewModel> e)
	{
		switch (e.Action)
		{
			case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
				// 削除された MaterialItemViewModel を Dispose
				if (e.IsSingleItem)
				{
					e.OldItem.View?.Dispose();
				}
				else
				{
					foreach (var childViewModel in e.OldViews)
					{
						childViewModel?.Dispose();
					}
				}
				break;

			case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
				// 置換前の MaterialItemViewModel を Dispose
				if (e.IsSingleItem)
				{
					e.OldItem.View?.Dispose();
				}
				else
				{
					foreach (var childViewModel in e.OldViews)
					{
						childViewModel?.Dispose();
					}
				}
				break;

			case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
				// Reset は Sort / Reverse / Clear の場合が考えられる
				// IsClear で判定し、Clear の場合だけ旧 MaterialItemViewModel を Dispose
				if (e.SortOperation.IsClear)
				{
					foreach (var childViewModel in e.OldViews)
					{
						childViewModel?.Dispose();
					}
				}
				// Sort / Reverse の場合は現在有効な MaterialItemViewModel を Dispose しない
				break;
		}
	}

	/// <summary>
	/// CanCheck の Reactive プロパティを生成します。
	/// Material.ItemType と Material.IsSelectableByDefault、IsSelectionOverrideEnabled から導出されます。
	/// </summary>
	private IReadOnlyBindableReactiveProperty<bool> createCanCheckReactiveProperty()
	{
		return this.IsSelectionOverrideEnabled
			.Select(isOverride =>
				this.Material.IsSelectableByDefault
				|| (this.Material.CanEnableSelectionOverride && isOverride))
			.ToReadOnlyBindableReactiveProperty(false);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// 子 ViewModel を破棄（ViewChanged イベント処理で既に行われているが、
		// Dispose 時に念のため確認）
		foreach (var childViewModel in this.Children)
		{
			childViewModel?.Dispose();
		}

		// childrenView への参照をクリア
		this.childrenView = null;

		// ReactiveProperty 等を破棄
		this.disposableBag.Dispose();
	}
}
