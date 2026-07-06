using MangaBinder.Controls;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本開始ページの ListView アイテム表示用 ViewModel です。BindingSeries をラップしています。
/// </summary>
public class StartPageSeriesCardViewModel
{
	/// <summary>
	/// 基になった BindingSeries です。
	/// </summary>
	public BindingSeries BindingSeries { get; }

	/// <summary>
	/// BindingSeries に含まれる MangaSeries です。
	/// </summary>
	public MangaSeries Series => this.BindingSeries.Series;

	/// <summary>
	/// 巻情報表示用の ViewModel です。
	/// </summary>
	public SeriesVolumeStatusViewModel VolumeStatus { get; }

	/// <summary>
	/// あらすじが存在するかどうかを示します。
	/// </summary>
	public bool HasSynopsis => this.BindingSeries.HasSynopsis;

	/// <summary>
	/// 製本開始キュー内での表示用タグテキスト。
	/// </summary>
	public string TagDisplayText => this.BindingSeries.TagDisplayText;

	/// <summary>
	/// 製本開始キューの進行状態を取得します。
	/// </summary>
	public BindingStartStatus Status => this.BindingSeries.Status;

	/// <summary>
	/// 現在の製本工程番号を取得します。
	/// </summary>
	public int CurrentStep => this.BindingSeries.CurrentStep;

	/// <summary>
	/// キューに追加した日時を取得します。
	/// </summary>
	public DateTime AddedAt => this.BindingSeries.AddedAt;

	/// <summary>
	/// 最終更新日時を取得します。
	/// </summary>
	public DateTime UpdatedAt => this.BindingSeries.UpdatedAt;

	/// <summary>
	/// <see cref="StartPageSeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingSeries">ラップする BindingSeries。</param>
	public StartPageSeriesCardViewModel(BindingSeries bindingSeries)
	{
		this.BindingSeries = bindingSeries;
		this.VolumeStatus = SeriesVolumeStatusViewModel.FromSeries(bindingSeries.Series);
	}
}
