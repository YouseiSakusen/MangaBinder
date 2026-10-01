using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 圧縮ファイル比較Dialog専用の ViewModel です。
/// </summary>
public class ArchiveCompareDialogContentViewModel : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// 比較候補となるアーカイブの一覧を取得します。
	/// BindingStore が保持している正本を参照するだけで、コピーや再生成は行いません。
	/// </summary>
	public IReadOnlyList<MaterialItem> ArchiveCandidates { get; }

	/// <summary>
	/// 左側（比較元）に選択されたアーカイブを取得または設定します。
	/// 初期値は null です。
	/// </summary>
	public BindableReactiveProperty<MaterialItem?> LeftArchive { get; }

	/// <summary>
	/// 右側（比較先）に選択されたアーカイブを取得または設定します。
	/// 初期値は null です。
	/// </summary>
	public BindableReactiveProperty<MaterialItem?> RightArchive { get; }

	/// <summary>
	/// 比較処理を実行するコマンドです。
	/// LeftArchive と RightArchive が両方 null でなく、かつ異なるインスタンスの場合のみ実行可能です。
	/// </summary>
	public ReactiveCommand<Unit> CompareCommand { get; }

	/// <summary>
	/// 比較結果Treeを取得します。初期値は空のListです。
	/// </summary>
	public BindableReactiveProperty<IReadOnlyList<ArchiveCompareNode>> CompareResult { get; }

	/// <summary>
	/// 比較結果領域を表示するかどうかを取得または設定します。初期値は false です。
	/// </summary>
	public BindableReactiveProperty<bool> IsResultVisible { get; }

	/// <summary>
	/// <see cref="ArchiveCompareDialogContentViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="archiveCandidates">
	/// 比較候補となるアーカイブの一覧。
	/// BindingStore.Materials から抽出された MaterialItemType.Archive のアイテムリスト。
	/// </param>
	public ArchiveCompareDialogContentViewModel(IReadOnlyList<MaterialItem> archiveCandidates)
	{
		this.ArchiveCandidates = archiveCandidates;

		this.LeftArchive = new BindableReactiveProperty<MaterialItem?>(null)
			.AddTo(ref this.disposableBag);

		this.RightArchive = new BindableReactiveProperty<MaterialItem?>(null)
			.AddTo(ref this.disposableBag);

		// CanExecute を Observable.CombineLatest で導出
		// 条件：LeftArchive と RightArchive が両方 null でなく、異なるインスタンスである
		var canCompare = Observable.CombineLatest(
			this.LeftArchive,
			this.RightArchive,
			(left, right) =>
			{
				if (left is null || right is null)
					return false;

				// ReferenceEquals で同一インスタンスかどうかを判定
				// 同一インスタンスの場合は実行不可
				return !ReferenceEquals(left, right);
			});

		this.CompareCommand = new ReactiveCommand<Unit>(canCompare, initialCanExecute: false)
			.AddTo(ref this.disposableBag);

		this.CompareResult = new BindableReactiveProperty<IReadOnlyList<ArchiveCompareNode>>(new List<ArchiveCompareNode>())
			.AddTo(ref this.disposableBag);

		this.IsResultVisible = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
	}

	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
