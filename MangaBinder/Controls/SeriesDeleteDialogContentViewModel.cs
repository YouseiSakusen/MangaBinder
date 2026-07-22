using MangaBinder.Core.Series;
using R3;
using Reactive.Bindings.R3;

namespace MangaBinder.Controls;

/// <summary>
/// 作品削除確認ダイアログの ViewModel です。
/// </summary>
public class SeriesDeleteDialogContentViewModel : IDisposable
{
	private DisposableBag disposableBag = new();

	/// <summary>
	/// 削除対象作品を取得します。
	/// </summary>
	public BindableReactiveProperty<MangaSeries> TargetSeries { get; }

	/// <summary>
	/// 選択された削除方法を取得または設定します。
	/// </summary>
	public BindableReactiveProperty<SeriesDeleteMethod> SelectedDeleteMethod { get; }

	/// <summary>
	/// 素材フォルダ削除オプションを表示するかどうかを取得します。
	/// TargetSeries.IsWork が false（正式作品）の場合のみ true になります。
	/// </summary>
	public BindableReactiveProperty<bool> ShowMaterialFolderDeleteOption { get; }

	/// <summary>
	/// <see cref="SeriesDeleteDialogContentViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="targetSeries">削除対象作品。</param>
	public SeriesDeleteDialogContentViewModel(MangaSeries targetSeries)
	{
		ArgumentNullException.ThrowIfNull(targetSeries);

		this.TargetSeries = new BindableReactiveProperty<MangaSeries>(targetSeries)
			.AddTo(ref this.disposableBag);

		// 初期選択は「作品情報のみ削除する」
		this.SelectedDeleteMethod = new BindableReactiveProperty<SeriesDeleteMethod>(SeriesDeleteMethod.InfoOnly)
			.AddTo(ref this.disposableBag);

		// 正式作品（IsWork が false）の場合のみ、素材フォルダ削除オプションを表示
		this.ShowMaterialFolderDeleteOption = new BindableReactiveProperty<bool>(!targetSeries.IsWork)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// リソースを解放します。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}

/// <summary>
/// 作品削除方法の種類を表します。
/// </summary>
public enum SeriesDeleteMethod
{
	/// <summary>
	/// 作品情報のみ削除します。
	/// </summary>
	InfoOnly = 0,

	/// <summary>
	/// 作品情報と素材フォルダを削除します。
	/// </summary>
	InfoAndFolder = 1,
}
