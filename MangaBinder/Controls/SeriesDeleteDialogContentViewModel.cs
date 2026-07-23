using MangaBinder.Core.Series;
using MangaBinder.Series;
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
	/// TargetSeries.IsWork が false（正式作品）かつ素材が存在しない場合のみ true になります。
	/// </summary>
	public BindableReactiveProperty<bool> ShowMaterialFolderDeleteOption { get; }

	/// <summary>
	/// 素材が存在することを示す警告メッセージを表示するかどうかを取得します。
	/// TargetSeries.IsWork が false（正式作品）かつ素材が存在する場合のみ true になります。
	/// </summary>
	public BindableReactiveProperty<bool> ShowMaterialsWarning { get; }

	/// <summary>
	/// <see cref="SeriesDeleteDialogContentViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="targetSeries">削除対象作品。</param>
	/// <param name="hasMaterialFiles">素材ファイルが存在するかどうか。</param>
	public SeriesDeleteDialogContentViewModel(MangaSeries targetSeries, bool hasMaterialFiles = false)
	{
		ArgumentNullException.ThrowIfNull(targetSeries);

		this.TargetSeries = new BindableReactiveProperty<MangaSeries>(targetSeries)
			.AddTo(ref this.disposableBag);

		// 初期選択は「作品情報のみ削除する」
		this.SelectedDeleteMethod = new BindableReactiveProperty<SeriesDeleteMethod>(SeriesDeleteMethod.InfoOnly)
			.AddTo(ref this.disposableBag);

		// 正式作品（IsWork が false）かつ素材が存在しない場合のみ、素材フォルダ削除オプションを表示
		this.ShowMaterialFolderDeleteOption = new BindableReactiveProperty<bool>(!targetSeries.IsWork && !hasMaterialFiles)
			.AddTo(ref this.disposableBag);

		// 正式作品（IsWork が false）かつ素材が存在する場合のみ、素材警告メッセージを表示
		this.ShowMaterialsWarning = new BindableReactiveProperty<bool>(!targetSeries.IsWork && hasMaterialFiles)
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
